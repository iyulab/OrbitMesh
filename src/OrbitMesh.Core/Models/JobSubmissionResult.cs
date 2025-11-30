using MessagePack;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Result of job submission.
/// </summary>
[MessagePackObject]
public sealed record JobSubmissionResult
{
    /// <summary>
    /// The ID of the submitted job.
    /// </summary>
    [Key(0)]
    public required string JobId { get; init; }

    /// <summary>
    /// Whether the submission was successful.
    /// </summary>
    [Key(1)]
    public required bool Success { get; init; }

    /// <summary>
    /// The current status of the job.
    /// </summary>
    [Key(2)]
    public JobStatus Status { get; init; } = JobStatus.Pending;

    /// <summary>
    /// Error message if submission failed.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful submission result.
    /// </summary>
    public static JobSubmissionResult Succeeded(string jobId, JobStatus status = JobStatus.Pending) =>
        new() { JobId = jobId, Success = true, Status = status };

    /// <summary>
    /// Creates a failed submission result.
    /// </summary>
    public static JobSubmissionResult Failed(string error) =>
        new() { JobId = string.Empty, Success = false, Error = error };
}
