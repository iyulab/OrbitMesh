using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Node.LocalScheduler;

/// <summary>
/// Configuration for a scheduled local task.
/// </summary>
public sealed class LocalTaskConfig
{
    /// <summary>
    /// Unique identifier for this task.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the task.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Interval between task executions.
    /// </summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Cron expression for scheduling (e.g., "0 */5 * * *" for every 5 minutes).
    /// If both Interval and CronExpression are set, Interval takes precedence.
    /// </summary>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Whether this task runs independently of server connection.
    /// </summary>
    public bool RunOffline { get; init; } = true;

    /// <summary>
    /// Whether this task is currently enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum execution time before task is considered timed out.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to run immediately on startup.
    /// </summary>
    public bool RunOnStartup { get; init; }
}

/// <summary>
/// Result of a local task execution.
/// </summary>
public sealed class LocalTaskResultEventArgs : EventArgs
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Whether the task succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Task output data.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// When the task was executed.
    /// </summary>
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Delegate for local task execution.
/// </summary>
public delegate Task<object?> LocalTaskHandler(LocalTaskContext context, CancellationToken cancellationToken);

/// <summary>
/// Context provided to local task handlers.
/// </summary>
public sealed class LocalTaskContext
{
    /// <summary>
    /// Task configuration.
    /// </summary>
    public required LocalTaskConfig Config { get; init; }

    /// <summary>
    /// Whether the agent is currently connected to the server.
    /// </summary>
    public required bool IsServerConnected { get; init; }

    /// <summary>
    /// Last execution time (null if first execution).
    /// </summary>
    public DateTimeOffset? LastExecutionTime { get; init; }

    /// <summary>
    /// Logger for the task.
    /// </summary>
    public required ILogger Logger { get; init; }
}

/// <summary>
/// Service for running scheduled tasks locally, independent of server connection.
/// Tasks continue to run even when the server is unavailable.
/// </summary>
public sealed class LocalSchedulerService : IHostedService, IDisposable
{
    private readonly ILogger<LocalSchedulerService> _logger;
    private readonly IMeshAgent? _agent;
    private readonly ConcurrentDictionary<string, TaskState> _tasks = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when a task completes.
    /// </summary>
    public event EventHandler<LocalTaskResultEventArgs>? TaskCompleted;

    /// <summary>
    /// Creates a new local scheduler service.
    /// </summary>
    public LocalSchedulerService(
        ILogger<LocalSchedulerService> logger,
        IMeshAgent? agent = null)
    {
        _logger = logger;
        _agent = agent;
    }

    /// <summary>
    /// Registers a task with the scheduler.
    /// </summary>
    public void RegisterTask(LocalTaskConfig config, LocalTaskHandler handler)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tasks.ContainsKey(config.Id))
        {
            throw new InvalidOperationException($"Task '{config.Id}' is already registered");
        }

        var state = new TaskState(config, handler);
        if (!_tasks.TryAdd(config.Id, state))
        {
            throw new InvalidOperationException($"Failed to register task '{config.Id}'");
        }

        _logger.LogInformation(
            "Registered local task: {TaskId} ({TaskName}), Interval: {Interval}, RunOffline: {RunOffline}",
            config.Id, config.Name, config.Interval, config.RunOffline);
    }

    /// <summary>
    /// Unregisters a task from the scheduler.
    /// </summary>
    public bool UnregisterTask(string taskId)
    {
        if (_tasks.TryRemove(taskId, out var state))
        {
            state.CancellationSource.Cancel();
            state.CancellationSource.Dispose();
            _logger.LogInformation("Unregistered local task: {TaskId}", taskId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Triggers immediate execution of a task.
    /// </summary>
    public async Task<LocalTaskResultEventArgs> TriggerTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var state))
        {
            return new LocalTaskResultEventArgs
            {
                TaskId = taskId,
                Success = false,
                Error = $"Task '{taskId}' not found"
            };
        }

        return await ExecuteTaskAsync(state, cancellationToken);
    }

    /// <summary>
    /// Gets the status of all registered tasks.
    /// </summary>
    public IReadOnlyDictionary<string, LocalTaskStatus> GetTaskStatuses()
    {
        return _tasks.ToDictionary(
            kvp => kvp.Key,
            kvp => new LocalTaskStatus
            {
                TaskId = kvp.Key,
                TaskName = kvp.Value.Config.Name,
                Enabled = kvp.Value.Config.Enabled,
                LastExecutionTime = kvp.Value.LastExecutionTime,
                LastResult = kvp.Value.LastResult,
                NextExecutionTime = kvp.Value.NextExecutionTime
            });
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting local scheduler service with {TaskCount} tasks", _tasks.Count);

        foreach (var state in _tasks.Values)
        {
            if (state.Config.Enabled)
            {
                StartTaskLoop(state);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping local scheduler service");

        await _shutdownCts.CancelAsync();

        // Wait for all running tasks to complete (with timeout)
        var runningTasks = _tasks.Values
            .Where(s => s.IsRunning)
            .Select(s => s.CurrentTask)
            .Where(t => t != null)
            .Cast<Task>()
            .ToList();

        if (runningTasks.Count > 0)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await Task.WhenAll(runningTasks).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Some tasks did not complete within timeout");
            }
        }
    }

    private void StartTaskLoop(TaskState state)
    {
        _ = RunTaskLoopAsync(state);
    }

    private async Task RunTaskLoopAsync(TaskState state)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _shutdownCts.Token, state.CancellationSource.Token);

        var token = linkedCts.Token;

        // Run on startup if configured
        if (state.Config.RunOnStartup)
        {
            _logger.LogDebug("Running task {TaskId} on startup", state.Config.Id);
            await ExecuteTaskAsync(state, token);
        }

        // Main scheduling loop
        while (!token.IsCancellationRequested)
        {
            try
            {
                var delay = CalculateNextDelay(state);
                state.NextExecutionTime = DateTimeOffset.UtcNow.Add(delay);

                _logger.LogDebug(
                    "Task {TaskId} scheduled to run in {Delay}",
                    state.Config.Id, delay);

                await Task.Delay(delay, token);

                // Check if we should run (offline mode check)
                if (ShouldRunTask(state))
                {
                    await ExecuteTaskAsync(state, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task loop for {TaskId}", state.Config.Id);
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(30), token)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private bool ShouldRunTask(TaskState state)
    {
        // Always run if RunOffline is true
        if (state.Config.RunOffline)
        {
            return true;
        }

        // Otherwise, only run if connected to server
        return _agent?.IsConnected ?? false;
    }

    private TimeSpan CalculateNextDelay(TaskState state)
    {
        // Use interval if specified
        if (state.Config.Interval.HasValue)
        {
            return state.Config.Interval.Value;
        }

        // TODO: Parse cron expression and calculate next run time
        // For now, default to 1 hour if no interval specified
        if (!string.IsNullOrEmpty(state.Config.CronExpression))
        {
            _logger.LogWarning(
                "Cron expressions not yet implemented for task {TaskId}, using 1 hour interval",
                state.Config.Id);
        }

        return TimeSpan.FromHours(1);
    }

    private async Task<LocalTaskResultEventArgs> ExecuteTaskAsync(TaskState state, CancellationToken cancellationToken)
    {
        if (state.IsRunning)
        {
            _logger.LogWarning("Task {TaskId} is already running, skipping", state.Config.Id);
            return new LocalTaskResultEventArgs
            {
                TaskId = state.Config.Id,
                Success = false,
                Error = "Task is already running"
            };
        }

        state.IsRunning = true;
        var startTime = DateTimeOffset.UtcNow;
        var success = false;

        _logger.LogInformation("Executing local task: {TaskId} ({TaskName})", state.Config.Id, state.Config.Name);

        LocalTaskResultEventArgs result;

        try
        {
            using var timeoutCts = new CancellationTokenSource(state.Config.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            var context = new LocalTaskContext
            {
                Config = state.Config,
                IsServerConnected = _agent?.IsConnected ?? false,
                LastExecutionTime = state.LastExecutionTime,
                Logger = _logger
            };

            state.CurrentTask = state.Handler(context, linkedCts.Token);
            var data = await state.CurrentTask;

            var duration = DateTimeOffset.UtcNow - startTime;
            success = true;

            result = new LocalTaskResultEventArgs
            {
                TaskId = state.Config.Id,
                Success = true,
                Data = data,
                Duration = duration,
                ExecutedAt = startTime
            };

            _logger.LogInformation(
                "Task {TaskId} completed successfully in {Duration}ms",
                state.Config.Id, duration.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = new LocalTaskResultEventArgs
            {
                TaskId = state.Config.Id,
                Success = false,
                Error = "Task was cancelled",
                Duration = DateTimeOffset.UtcNow - startTime,
                ExecutedAt = startTime
            };
        }
        catch (Exception ex)
        {
            result = new LocalTaskResultEventArgs
            {
                TaskId = state.Config.Id,
                Success = false,
                Error = ex.Message,
                Duration = DateTimeOffset.UtcNow - startTime,
                ExecutedAt = startTime
            };

            _logger.LogError(ex, "Task {TaskId} failed", state.Config.Id);
        }
        finally
        {
            state.IsRunning = false;
            state.CurrentTask = null;
            state.LastExecutionTime = startTime;
            state.LastResult = success;
        }

        // Raise event
        try
        {
            TaskCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TaskCompleted event handler");
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        foreach (var state in _tasks.Values)
        {
            state.CancellationSource.Dispose();
        }

        _tasks.Clear();
    }

    private sealed class TaskState
    {
        public LocalTaskConfig Config { get; }
        public LocalTaskHandler Handler { get; }
        public CancellationTokenSource CancellationSource { get; } = new();
        public DateTimeOffset? LastExecutionTime { get; set; }
        public DateTimeOffset? NextExecutionTime { get; set; }
        public bool? LastResult { get; set; }
        public bool IsRunning { get; set; }
        public Task<object?>? CurrentTask { get; set; }

        public TaskState(LocalTaskConfig config, LocalTaskHandler handler)
        {
            Config = config;
            Handler = handler;
        }
    }

}

/// <summary>
/// Status information for a local task.
/// </summary>
public sealed class LocalTaskStatus
{
    /// <summary>Task identifier.</summary>
    public required string TaskId { get; init; }
    /// <summary>Task name.</summary>
    public required string TaskName { get; init; }
    /// <summary>Whether the task is enabled.</summary>
    public required bool Enabled { get; init; }
    /// <summary>Last execution time.</summary>
    public DateTimeOffset? LastExecutionTime { get; init; }
    /// <summary>Last result (true = success).</summary>
    public bool? LastResult { get; init; }
    /// <summary>Next scheduled execution time.</summary>
    public DateTimeOffset? NextExecutionTime { get; init; }
}
