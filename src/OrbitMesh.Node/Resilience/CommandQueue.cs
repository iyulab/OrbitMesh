using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Node.Resilience;

/// <summary>
/// A queued command awaiting execution.
/// </summary>
public sealed record QueuedCommand
{
    /// <summary>
    /// The method name to invoke on the server.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The arguments to pass to the method.
    /// </summary>
    public required object?[] Arguments { get; init; }

    /// <summary>
    /// When the command was queued.
    /// </summary>
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of retry attempts for this command.
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// Event args for command queue events.
/// </summary>
public sealed class CommandQueueEventArgs : EventArgs
{
    /// <summary>
    /// The queued command.
    /// </summary>
    public required QueuedCommand Command { get; init; }
}

/// <summary>
/// Manages a queue of commands to be executed when the connection is restored.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue accurately describes the purpose")]
public sealed class CommandQueue : IDisposable
{
    private readonly ConcurrentQueue<QueuedCommand> _queue = new();
    private readonly ResilienceOptions _options;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private int _count;
    private bool _disposed;

    /// <summary>
    /// Gets the number of queued commands.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets whether the queue is empty.
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Event raised when a command is enqueued.
    /// </summary>
    public event EventHandler<CommandQueueEventArgs>? CommandEnqueued;

    /// <summary>
    /// Event raised when the queue is being drained.
    /// </summary>
    public event EventHandler? QueueDraining;

    /// <summary>
    /// Creates a new CommandQueue.
    /// </summary>
    /// <param name="options">Resilience options.</param>
    /// <param name="logger">Logger instance.</param>
    public CommandQueue(ResilienceOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enqueues a command to be executed when the connection is restored.
    /// </summary>
    /// <param name="methodName">The method to invoke.</param>
    /// <param name="args">The arguments to pass.</param>
    /// <returns>True if the command was queued; false if the queue is full.</returns>
    public bool Enqueue(string methodName, params object?[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // Check queue limit
            if (_count >= _options.MaxQueuedCommands)
            {
                _logger.LogWarning(
                    "Command queue is full ({Count}/{Max}). Command '{Method}' was not queued.",
                    _count, _options.MaxQueuedCommands, methodName);
                return false;
            }

            var command = new QueuedCommand
            {
                MethodName = methodName,
                Arguments = args,
                QueuedAt = DateTimeOffset.UtcNow
            };

            _queue.Enqueue(command);
            Interlocked.Increment(ref _count);

            _logger.LogDebug(
                "Command queued: {Method}. Queue size: {Count}",
                methodName, _count);

            CommandEnqueued?.Invoke(this, new CommandQueueEventArgs { Command = command });
            return true;
        }
    }

    /// <summary>
    /// Dequeues all commands, filtering out expired ones.
    /// </summary>
    /// <returns>Enumerable of valid queued commands.</returns>
    public IEnumerable<QueuedCommand> DequeueAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = DateTimeOffset.UtcNow;
        var validCommands = new List<QueuedCommand>();

        QueueDraining?.Invoke(this, EventArgs.Empty);

        while (_queue.TryDequeue(out var command))
        {
            Interlocked.Decrement(ref _count);

            var age = now - command.QueuedAt;
            if (age > _options.MaxCommandAge)
            {
                _logger.LogWarning(
                    "Discarding expired command '{Method}' (age: {Age})",
                    command.MethodName, age);
                continue;
            }

            validCommands.Add(command);
        }

        _logger.LogInformation(
            "Dequeued {Count} commands for execution",
            validCommands.Count);

        return validCommands;
    }

    /// <summary>
    /// Clears all queued commands.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (_queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }

        _logger.LogDebug("Command queue cleared");
    }

    /// <summary>
    /// Removes expired commands from the queue.
    /// </summary>
    /// <returns>Number of commands removed.</returns>
    public int PruneExpired()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = DateTimeOffset.UtcNow;
        var validCommands = new List<QueuedCommand>();
        var expiredCount = 0;

        while (_queue.TryDequeue(out var command))
        {
            Interlocked.Decrement(ref _count);

            var age = now - command.QueuedAt;
            if (age > _options.MaxCommandAge)
            {
                expiredCount++;
                _logger.LogDebug(
                    "Pruned expired command '{Method}' (age: {Age})",
                    command.MethodName, age);
            }
            else
            {
                validCommands.Add(command);
            }
        }

        // Re-enqueue valid commands
        foreach (var command in validCommands)
        {
            _queue.Enqueue(command);
            Interlocked.Increment(ref _count);
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("Pruned {Count} expired commands", expiredCount);
        }

        return expiredCount;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }
}
