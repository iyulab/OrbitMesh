using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Manages job lifecycle, queuing, and state tracking.
/// </summary>
public interface IJobManager
{
    #region Job Lifecycle

    /// <summary>
    /// Enqueues a new job request.
    /// If a job with the same idempotency key exists, returns the existing job.
    /// </summary>
    /// <param name="request">The job request to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or existing job.</returns>
    Task<Job> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a job by ID.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job if found, null otherwise.</returns>
    Task<Job?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a job to an agent.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="agentId">The agent ID to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully assigned, false otherwise.</returns>
    Task<bool> AssignAsync(string jobId, string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges that an agent has received and started processing a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="agentId">The agent ID acknowledging the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully acknowledged, false otherwise.</returns>
    Task<bool> AcknowledgeAsync(string jobId, string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a job as completed with a result.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="result">The job result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully completed, false otherwise.</returns>
    Task<bool> CompleteAsync(string jobId, JobResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a job as failed.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully failed, false otherwise.</returns>
    Task<bool> FailAsync(string jobId, string errorMessage, string? errorCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully cancelled, false otherwise.</returns>
    Task<bool> CancelAsync(string jobId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues a failed job for retry.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully requeued, false if max retries exceeded.</returns>
    Task<bool> RequeueAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues a timed out job for retry.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="maxTimeoutRetries">Maximum timeout retries allowed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully requeued, false if max timeout retries exceeded.</returns>
    Task<bool> RequeueForTimeoutAsync(string jobId, int maxTimeoutRetries, CancellationToken cancellationToken = default);

    #endregion

    #region Progress Tracking

    /// <summary>
    /// Updates the progress of a running job.
    /// </summary>
    /// <param name="progress">The progress update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProgressAsync(JobProgress progress, CancellationToken cancellationToken = default);

    #endregion

    #region Queue Operations

    /// <summary>
    /// Dequeues the next pending job with highest priority.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next job or null if queue is empty.</returns>
    Task<Job?> DequeueNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next pending job that matches the specified capabilities.
    /// </summary>
    /// <param name="capabilities">Available capabilities to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next matching job or null if no match found.</returns>
    Task<Job?> DequeueNextAsync(IReadOnlyList<string> capabilities, CancellationToken cancellationToken = default);

    #endregion

    #region Query Operations

    /// <summary>
    /// Gets all pending jobs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending jobs.</returns>
    Task<IReadOnlyList<Job>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs assigned to a specific agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of jobs for the agent.</returns>
    Task<IReadOnlyList<Job>> GetByAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs with a specific status.
    /// </summary>
    /// <param name="status">The job status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of jobs with the status.</returns>
    Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs that have timed out.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of timed out jobs.</returns>
    Task<IReadOnlyList<Job>> GetTimedOutJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets jobs with optional status and agent filters.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="agentId">Optional agent ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching jobs.</returns>
    Task<IReadOnlyList<Job>> GetJobsAsync(JobStatus? status = null, string? agentId = null, CancellationToken cancellationToken = default);

    #endregion
}
