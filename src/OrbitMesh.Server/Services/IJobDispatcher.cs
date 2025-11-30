using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Dispatches jobs to agents and manages the job queue.
/// </summary>
public interface IJobDispatcher
{
    /// <summary>
    /// Enqueues a job request for dispatch.
    /// </summary>
    /// <param name="request">The job request to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The enqueued job.</returns>
    Task<Job> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a job to an appropriate agent.
    /// </summary>
    /// <param name="job">The job to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dispatch result.</returns>
    Task<DispatchResult> DispatchAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a job.
    /// </summary>
    /// <param name="jobId">The job ID to cancel.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully cancelled, false otherwise.</returns>
    Task<bool> CancelJobAsync(string jobId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current queue depth (number of pending jobs).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of pending jobs.</returns>
    Task<int> GetQueueDepthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current dispatcher statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dispatcher statistics.</returns>
    Task<DispatcherStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a job dispatch operation.
/// </summary>
public sealed record DispatchResult
{
    /// <summary>
    /// Whether the dispatch was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The agent ID the job was dispatched to.
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    /// The failure reason if dispatch failed.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Timestamp when the dispatch occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful dispatch result.
    /// </summary>
    public static DispatchResult Success(string agentId) =>
        new() { IsSuccess = true, AgentId = agentId };

    /// <summary>
    /// Creates a failed dispatch result.
    /// </summary>
    public static DispatchResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}

/// <summary>
/// Statistics about the job dispatcher.
/// </summary>
public sealed record DispatcherStatistics
{
    /// <summary>
    /// Number of jobs waiting to be dispatched.
    /// </summary>
    public int PendingJobs { get; init; }

    /// <summary>
    /// Number of jobs currently running.
    /// </summary>
    public int RunningJobs { get; init; }

    /// <summary>
    /// Number of connected agents.
    /// </summary>
    public int ConnectedAgents { get; init; }

    /// <summary>
    /// Total jobs dispatched since startup.
    /// </summary>
    public long TotalDispatched { get; init; }

    /// <summary>
    /// Total jobs failed since startup.
    /// </summary>
    public long TotalFailed { get; init; }

    /// <summary>
    /// Average dispatch latency in milliseconds.
    /// </summary>
    public double AverageDispatchLatencyMs { get; init; }
}
