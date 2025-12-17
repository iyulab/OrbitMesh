using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for server key information.
/// </summary>
[Table("ServerKeyInfo")]
public sealed class ServerKeyInfoEntity
{
    [Key]
    [MaxLength(64)]
    public required string ServerId { get; set; }

    /// <summary>
    /// Base64-encoded Ed25519 public key.
    /// </summary>
    [Required]
    public required string PublicKey { get; set; }

    /// <summary>
    /// Base64-encoded Ed25519 private key (encrypted in production).
    /// </summary>
    [Required]
    public required string PrivateKey { get; set; }

    [MaxLength(32)]
    public string Algorithm { get; set; } = "Ed25519";

    public DateTimeOffset GeneratedAt { get; set; }

    public bool IsActive { get; set; } = true;
}
