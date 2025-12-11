using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Queue for jobs that have failed permanently and cannot be retried automatically.
/// Provides mechanisms for manual inspection, retry, and cleanup.
/// </summary>
public interface IDeadLetterService
{
    /// <summary>
    /// Adds a failed job to the dead letter queue.
    /// </summary>
    /// <param name="job">The failed job.</param>
    /// <param name="reason">The reason for adding to DLQ.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created dead letter entry.</returns>
    Task<DeadLetterEntry> EnqueueAsync(Job job, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dead letter entry by its ID.
    /// </summary>
    /// <param name="entryId">The entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry or null if not found.</returns>
    Task<DeadLetterEntry?> GetAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dead letter entry by the original job ID.
    /// </summary>
    /// <param name="jobId">The original job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry or null if not found.</returns>
    Task<DeadLetterEntry?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all dead letter entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All entries in FIFO order.</returns>
    Task<IReadOnlyList<DeadLetterEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entry for retry.
    /// </summary>
    /// <param name="entryId">The entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if marked successfully, false if not found.</returns>
    Task<bool> MarkForRetryAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entries that are marked for retry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Entries pending retry.</returns>
    Task<IReadOnlyList<DeadLetterEntry>> GetPendingRetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entry from the dead letter queue.
    /// </summary>
    /// <param name="entryId">The entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if removed, false if not found.</returns>
    Task<bool> RemoveAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all entries from the dead letter queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries removed.</returns>
    Task<int> PurgeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current count of entries in the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry count.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Entry in the dead letter queue containing a failed job and metadata.
/// </summary>
public sealed record DeadLetterEntry
{
    /// <summary>
    /// Unique identifier for this DLQ entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The failed job.
    /// </summary>
    public required Job Job { get; init; }

    /// <summary>
    /// Reason for adding to the dead letter queue.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// When the entry was added to the DLQ.
    /// </summary>
    public required DateTimeOffset EnqueuedAt { get; init; }

    /// <summary>
    /// Whether this entry has been marked for retry.
    /// </summary>
    public bool RetryRequested { get; init; }

    /// <summary>
    /// When retry was requested, if applicable.
    /// </summary>
    public DateTimeOffset? RetryRequestedAt { get; init; }

    /// <summary>
    /// Number of times this entry has been retried from DLQ.
    /// </summary>
    public int RetryAttempts { get; init; }
}
