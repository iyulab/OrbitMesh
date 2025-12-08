using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for Agent.
/// </summary>
[Table("Agents")]
public sealed class AgentEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    [Required]
    [MaxLength(256)]
    public required string Name { get; set; }

    public int Status { get; set; }

    [MaxLength(100)]
    public string? Group { get; set; }

    [MaxLength(256)]
    public string? Hostname { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    [MaxLength(128)]
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Serialized Tags (JSON array).
    /// </summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// Serialized Capabilities (JSON array).
    /// </summary>
    public string? CapabilitiesJson { get; set; }

    /// <summary>
    /// Serialized Metadata (JSON object).
    /// </summary>
    public string? MetadataJson { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastHeartbeat { get; set; }

    // Navigation
    public ICollection<JobEntity> Jobs { get; } = [];
}
