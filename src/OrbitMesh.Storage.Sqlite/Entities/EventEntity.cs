using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for Event (event sourcing).
/// </summary>
[Table("Events")]
public sealed class EventEntity
{
    /// <summary>
    /// Global position (auto-increment).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Position { get; set; }

    [Required]
    [MaxLength(64)]
    public required string EventId { get; set; }

    [Required]
    [MaxLength(256)]
    public required string StreamId { get; set; }

    [Required]
    [MaxLength(256)]
    public required string EventType { get; set; }

    /// <summary>
    /// Version within the stream.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Serialized event payload.
    /// </summary>
    [Required]
    public required byte[] Data { get; set; }

    /// <summary>
    /// Optional metadata.
    /// </summary>
    public byte[]? Metadata { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
