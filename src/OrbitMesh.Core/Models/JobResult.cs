using MessagePack;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents the result of a job execution.
/// </summary>
[MessagePackObject]
public sealed record JobResult
{
    /// <summary>
    /// The job ID this result belongs to.
    /// </summary>
    [Key(0)]
    public required string JobId { get; init; }

    /// <summary>
    /// The agent ID that executed the job.
    /// </summary>
    [Key(1)]
    public required string AgentId { get; init; }

    /// <summary>
    /// Final status of the job.
    /// </summary>
    [Key(2)]
    public JobStatus Status { get; init; }

    /// <summary>
    /// Serialized result data (null for failures).
    /// </summary>
    [Key(3)]
    public byte[]? Data { get; init; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    [Key(4)]
    public string? Error { get; init; }

    /// <summary>
    /// Error code for programmatic error handling.
    /// </summary>
    [Key(5)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Stack trace for debugging (only in development).
    /// </summary>
    [Key(6)]
    public string? StackTrace { get; init; }

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    [Key(7)]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed.
    /// </summary>
    [Key(8)]
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    [IgnoreMember]
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Whether the job completed successfully.
    /// </summary>
    [IgnoreMember]
    public bool IsSuccess => Status == JobStatus.Completed;

    /// <summary>
    /// Custom metadata from the execution.
    /// </summary>
    [Key(9)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static JobResult Success(string jobId, string agentId, byte[]? data = null) =>
        new()
        {
            JobId = jobId,
            AgentId = agentId,
            Status = JobStatus.Completed,
            Data = data,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static JobResult Failure(string jobId, string agentId, string error, string? errorCode = null) =>
        new()
        {
            JobId = jobId,
            AgentId = agentId,
            Status = JobStatus.Failed,
            Error = error,
            ErrorCode = errorCode,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
}
