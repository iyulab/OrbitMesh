using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for WorkflowDefinition.
/// </summary>
[Table("WorkflowDefinitions")]
public sealed class WorkflowDefinitionEntity
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
    [MaxLength(50)]
    public required string Version { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Workflow definition content (YAML or JSON).
    /// </summary>
    [Required]
    public required string Content { get; set; }

    [MaxLength(10)]
    public string ContentFormat { get; set; } = "yaml";

    /// <summary>
    /// Serialized trigger configuration (JSON).
    /// </summary>
    public string? TriggerJson { get; set; }

    /// <summary>
    /// Serialized target configuration (JSON).
    /// </summary>
    public string? TargetJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<WorkflowInstanceEntity> Instances { get; } = [];
}
