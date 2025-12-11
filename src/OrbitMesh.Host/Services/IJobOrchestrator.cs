using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Orchestrates job lifecycle from submission to completion.
/// Integrates all job-related services for coordinated execution.
/// </summary>
public interface IJobOrchestrator
{
    /// <summary>
    /// Submits a job for execution.
    /// </summary>
    /// <param name="request">The job request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The submission result with job ID.</returns>
    Task<JobSubmissionResult> SubmitJobAsync(JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a job result from an agent.
    /// </summary>
    /// <param name="result">The job result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleResultAsync(JobResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles progress update from an agent.
    /// </summary>
    /// <param name="progress">The progress update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleProgressAsync(JobProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    /// <param name="jobId">The job ID to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if cancellation was initiated, false otherwise.</returns>
    Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a job by ID.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job or null if not found.</returns>
    Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets jobs with optional filtering.
    /// </summary>
    /// <param name="status">Filter by status.</param>
    /// <param name="agentId">Filter by assigned agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching jobs.</returns>
    Task<IReadOnlyList<Job>> GetJobsAsync(
        JobStatus? status = null,
        string? agentId = null,
        CancellationToken cancellationToken = default);
}
