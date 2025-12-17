using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for node certificates.
/// </summary>
[Table("NodeCertificates")]
public sealed class NodeCertificateEntity
{
    [Key]
    [MaxLength(64)]
    public required string SerialNumber { get; set; }

    public int Version { get; set; } = 1;

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

    [Required]
    [MaxLength(64)]
    public required string ServerId { get; set; }

    /// <summary>
    /// Base64-encoded server public key.
    /// </summary>
    [Required]
    public required string ServerPublicKey { get; set; }

    /// <summary>
    /// JSON-serialized list of capabilities.
    /// </summary>
    public string? CapabilitiesJson { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Base64-encoded signature.
    /// </summary>
    [Required]
    public required string Signature { get; set; }

    public bool IsRevoked { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    [MaxLength(500)]
    public string? RevocationReason { get; set; }

    [MaxLength(128)]
    public string? RevokedBy { get; set; }
}
