using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for DeploymentProfile.
/// </summary>
[Table("DeploymentProfiles")]
public sealed class DeploymentProfileEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(256)]
    public required string Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string SourcePath { get; set; }

    [Required]
    [MaxLength(256)]
    public required string TargetAgentPattern { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string TargetPath { get; set; }

    public bool WatchForChanges { get; set; } = true;

    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// JSON serialized include patterns.
    /// </summary>
    public string? IncludePatternsJson { get; set; }

    /// <summary>
    /// JSON serialized exclude patterns.
    /// </summary>
    public string? ExcludePatternsJson { get; set; }

    public bool DeleteOrphans { get; set; }

    /// <summary>
    /// MessagePack serialized PreDeployScript.
    /// </summary>
    public byte[]? PreDeployScriptData { get; set; }

    /// <summary>
    /// MessagePack serialized PostDeployScript.
    /// </summary>
    public byte[]? PostDeployScriptData { get; set; }

    public int TransferMode { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastDeployedAt { get; set; }

    // Navigation
    public ICollection<DeploymentExecutionEntity> Executions { get; } = [];
}
