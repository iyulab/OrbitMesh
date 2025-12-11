using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Service for tracking and reporting job progress.
/// </summary>
public interface IProgressService
{
    /// <summary>
    /// Reports progress for a job.
    /// </summary>
    /// <param name="progress">The progress update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest progress for a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest progress or null if not found.</returns>
    Task<JobProgress?> GetProgressAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the progress history for a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of progress updates in chronological order.</returns>
    Task<IReadOnlyList<JobProgress>> GetProgressHistoryAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active job progress.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of latest progress for all tracked jobs.</returns>
    Task<IReadOnlyList<JobProgress>> GetAllProgressAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears progress data for a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearProgressAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to progress updates for a specific job.
    /// </summary>
    /// <param name="jobId">The job ID to subscribe to.</param>
    /// <param name="callback">Callback invoked when progress is reported.</param>
    /// <returns>A disposable subscription that stops updates when disposed.</returns>
    IDisposable Subscribe(string jobId, Func<JobProgress, Task> callback);
}
