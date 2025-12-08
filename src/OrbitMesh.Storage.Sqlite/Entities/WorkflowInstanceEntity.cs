using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for WorkflowInstance.
/// </summary>
[Table("WorkflowInstances")]
public sealed class WorkflowInstanceEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(64)]
    public required string WorkflowId { get; set; }

    [Required]
    [MaxLength(256)]
    public required string WorkflowName { get; set; }

    public int Status { get; set; }

    /// <summary>
    /// Serialized input parameters (JSON).
    /// </summary>
    public string? InputJson { get; set; }

    /// <summary>
    /// Serialized output (JSON).
    /// </summary>
    public string? OutputJson { get; set; }

    [MaxLength(2000)]
    public string? Error { get; set; }

    [MaxLength(256)]
    public string? CurrentStep { get; set; }

    /// <summary>
    /// Serialized step results (JSON array).
    /// </summary>
    public string? StepResultsJson { get; set; }

    [MaxLength(256)]
    public string? TriggeredBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(WorkflowId))]
    public WorkflowDefinitionEntity? WorkflowDefinition { get; set; }
}
