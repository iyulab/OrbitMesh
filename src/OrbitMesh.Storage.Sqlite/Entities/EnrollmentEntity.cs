using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for pending node enrollments.
/// </summary>
[Table("Enrollments")]
public sealed class EnrollmentEntity
{
    [Key]
    [MaxLength(64)]
    public required string EnrollmentId { get; set; }

    [Required]
    [MaxLength(64)]
    public required string NodeId { get; set; }

    [Required]
    [MaxLength(256)]
    public required string NodeName { get; set; }

    /// <summary>
    /// Base64-encoded Ed25519 public key.
    /// </summary>
    [Required]
    public required string PublicKey { get; set; }

    /// <summary>
    /// JSON-serialized list of requested capabilities.
    /// </summary>
    public string? RequestedCapabilitiesJson { get; set; }

    /// <summary>
    /// JSON-serialized metadata dictionary.
    /// </summary>
    public string? MetadataJson { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Enrollment status (0=Pending, 1=Approved, 2=Rejected, 3=Expired, 4=Blocked, 5=Failed).
    /// </summary>
    public int Status { get; set; }

    [MaxLength(64)]
    public string? BootstrapTokenId { get; set; }
}
