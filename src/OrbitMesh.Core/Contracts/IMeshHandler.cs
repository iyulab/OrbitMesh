using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// Base interface for all mesh command handlers.
/// </summary>
public interface IMeshHandler
{
    /// <summary>
    /// The command name this handler responds to.
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Handles the command and returns serialized result data.
    /// This is the default entry point for command execution.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The serialized result data or null.</returns>
    Task<byte[]?> HandleAsync(CommandContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for fire-and-forget execution pattern.
/// No return value expected.
/// </summary>
public interface IFireAndForgetHandler : IMeshHandler
{
    /// <summary>
    /// Handles the command without returning a result.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    new Task HandleAsync(CommandContext context, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    async Task<byte[]?> IMeshHandler.HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        await HandleAsync(context, cancellationToken);
        return null;
    }
}

/// <summary>
/// Handler for request-response execution pattern.
/// Returns a single result.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IRequestResponseHandler<TResult> : IMeshHandler
{
    /// <summary>
    /// Handles the command and returns a result.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the command execution.</returns>
    new Task<TResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    async Task<byte[]?> IMeshHandler.HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var result = await HandleAsync(context, cancellationToken);
        return result is null ? null : MessagePack.MessagePackSerializer.Serialize(result);
    }
}

/// <summary>
/// Handler for streaming execution pattern.
/// Returns an async enumerable of results.
/// </summary>
/// <typeparam name="TResult">The result item type.</typeparam>
public interface IStreamingHandler<TResult> : IMeshHandler
{
    /// <summary>
    /// Handles the command and streams results.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of result items.</returns>
    new IAsyncEnumerable<TResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    /// <remarks>
    /// Streaming handlers should be called via HandleAsync for streaming.
    /// This method collects all items into a list for non-streaming contexts.
    /// </remarks>
    async Task<byte[]?> IMeshHandler.HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var items = new List<TResult>();
        await foreach (var item in HandleAsync(context, cancellationToken))
        {
            items.Add(item);
        }
        return MessagePack.MessagePackSerializer.Serialize(items);
    }
}

/// <summary>
/// Handler for long-running job execution pattern.
/// Reports progress and returns a final result.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ILongRunningHandler<TResult> : IMeshHandler
{
    /// <summary>
    /// Handles the command with progress reporting.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="progressReporter">Reporter for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result of the command execution.</returns>
    Task<TResult> HandleAsync(
        CommandContext context,
        IProgress<JobProgress> progressReporter,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    /// <remarks>
    /// Long-running handlers should be called with a progress reporter.
    /// This method uses a no-op progress reporter.
    /// </remarks>
    async Task<byte[]?> IMeshHandler.HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var result = await HandleAsync(context, new Progress<JobProgress>(), cancellationToken);
        return result is null ? null : MessagePack.MessagePackSerializer.Serialize(result);
    }
}
