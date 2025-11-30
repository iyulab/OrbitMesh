using MessagePack;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a job in the mesh with full lifecycle state tracking.
/// This is the internal representation used by the job manager.
/// </summary>
[MessagePackObject]
public sealed record Job
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// The original job request.
    /// </summary>
    [Key(1)]
    public required JobRequest Request { get; init; }

    /// <summary>
    /// Current status of the job.
    /// </summary>
    [Key(2)]
    public JobStatus Status { get; init; } = JobStatus.Pending;

    /// <summary>
    /// The agent ID assigned to execute this job.
    /// </summary>
    [Key(3)]
    public string? AssignedAgentId { get; init; }

    /// <summary>
    /// Timestamp when the job was created/enqueued.
    /// </summary>
    [Key(4)]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when the job was assigned to an agent.
    /// </summary>
    [Key(5)]
    public DateTimeOffset? AssignedAt { get; init; }

    /// <summary>
    /// Timestamp when the job execution started (agent ACK).
    /// </summary>
    [Key(6)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the job completed (success or failure).
    /// </summary>
    [Key(7)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// The job result if completed.
    /// </summary>
    [Key(8)]
    public JobResult? Result { get; init; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    [Key(9)]
    public string? Error { get; init; }

    /// <summary>
    /// Error code for programmatic error handling.
    /// </summary>
    [Key(10)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Number of times this job has been retried.
    /// </summary>
    [Key(11)]
    public int RetryCount { get; init; } = 0;

    /// <summary>
    /// Last progress report received.
    /// </summary>
    [Key(12)]
    public JobProgress? LastProgress { get; init; }

    /// <summary>
    /// Cancellation reason if cancelled.
    /// </summary>
    [Key(13)]
    public string? CancellationReason { get; init; }

    /// <summary>
    /// Number of times this job has timed out.
    /// </summary>
    [Key(14)]
    public int TimeoutCount { get; init; } = 0;

    /// <summary>
    /// Whether the job can be retried based on max retries setting.
    /// </summary>
    [IgnoreMember]
    public bool CanRetry => RetryCount < Request.MaxRetries;

    /// <summary>
    /// Whether the job has timed out based on timeout setting.
    /// </summary>
    [IgnoreMember]
    public bool IsTimedOut =>
        Status == JobStatus.Running &&
        Request.Timeout.HasValue &&
        StartedAt.HasValue &&
        DateTimeOffset.UtcNow - StartedAt.Value > Request.Timeout.Value;

    /// <summary>
    /// Creates a new job from a job request.
    /// </summary>
    public static Job FromRequest(JobRequest request) =>
        new()
        {
            Id = request.Id,
            Request = request,
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
