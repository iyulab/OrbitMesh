using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for blocked nodes.
/// </summary>
[Table("BlockedNodes")]
public sealed class BlockedNodeEntity
{
    [Key]
    [MaxLength(64)]
    public required string NodeId { get; set; }

    public DateTimeOffset BlockedAt { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Reason { get; set; }

    [Required]
    [MaxLength(128)]
    public required string BlockedBy { get; set; }
}
