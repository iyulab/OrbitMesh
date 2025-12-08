using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for Job.
/// </summary>
[Table("Jobs")]
public sealed class JobEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(256)]
    public required string Command { get; set; }

    public int Status { get; set; }

    [MaxLength(64)]
    public string? AssignedAgentId { get; set; }

    public int Priority { get; set; }

    public int ExecutionPattern { get; set; }

    /// <summary>
    /// Serialized JobRequest (MessagePack).
    /// </summary>
    public required byte[] RequestData { get; set; }

    /// <summary>
    /// Serialized JobResult (MessagePack).
    /// </summary>
    public byte[]? ResultData { get; set; }

    /// <summary>
    /// Serialized JobProgress (MessagePack).
    /// </summary>
    public byte[]? ProgressData { get; set; }

    [MaxLength(2000)]
    public string? Error { get; set; }

    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    public int RetryCount { get; set; }

    public int TimeoutCount { get; set; }

    public long? TimeoutTicks { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AssignedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    public AgentEntity? AssignedAgent { get; set; }
}
