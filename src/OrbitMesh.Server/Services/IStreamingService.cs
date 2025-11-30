using System.Threading.Channels;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Service for managing streaming data flows from agents to clients.
/// Provides pub-sub mechanism for real-time data delivery.
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Publishes a stream item from an agent.
    /// </summary>
    /// <param name="item">The stream item to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(StreamItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subscription to a job's stream.
    /// Returns a ChannelReader for consuming stream items.
    /// </summary>
    /// <param name="jobId">The job ID to subscribe to.</param>
    /// <param name="fromSequence">Optional sequence number to start from (for replay).</param>
    /// <returns>A channel reader for consuming stream items.</returns>
    ChannelReader<StreamItem> Subscribe(string jobId, long fromSequence = 0);

    /// <summary>
    /// Creates an async enumerable subscription to a job's stream.
    /// </summary>
    /// <param name="jobId">The job ID to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of stream items.</returns>
    IAsyncEnumerable<StreamItem> SubscribeAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a stream.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stream state or null if not found.</returns>
    Task<StreamState?> GetStreamStateAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets buffered items for a stream (for late subscribers).
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="fromSequence">Start sequence number.</param>
    /// <param name="maxItems">Maximum items to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of buffered stream items.</returns>
    Task<IReadOnlyList<StreamItem>> GetBufferedItemsAsync(
        string jobId,
        long fromSequence = 0,
        int maxItems = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a stream as complete.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompleteStreamAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts a stream due to an error.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="errorMessage">The error message that caused the abort.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AbortStreamAsync(string jobId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up resources for a completed stream.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupStreamAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of active subscribers for a stream.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <returns>Number of active subscribers.</returns>
    int GetSubscriberCount(string jobId);

    /// <summary>
    /// Gets all active stream job IDs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active stream job IDs.</returns>
    Task<IReadOnlyList<string>> GetActiveStreamsAsync(CancellationToken cancellationToken = default);
}
