using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for DeploymentExecution.
/// </summary>
[Table("DeploymentExecutions")]
public sealed class DeploymentExecutionEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(64)]
    public required string ProfileId { get; set; }

    public int Status { get; set; }

    public int Trigger { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int TotalAgents { get; set; }

    public int SuccessfulAgents { get; set; }

    public int FailedAgents { get; set; }

    /// <summary>
    /// MessagePack serialized AgentDeploymentResult list.
    /// </summary>
    public byte[]? AgentResultsData { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public long BytesTransferred { get; set; }

    public int FilesTransferred { get; set; }

    // Navigation
    [ForeignKey(nameof(ProfileId))]
    public DeploymentProfileEntity? Profile { get; set; }
}
