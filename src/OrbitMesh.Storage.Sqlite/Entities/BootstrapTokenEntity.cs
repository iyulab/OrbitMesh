using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for the single bootstrap token.
/// There is only one bootstrap token per server.
/// </summary>
[Table("BootstrapToken")]
public sealed class BootstrapTokenEntity
{
    [Key]
    [MaxLength(64)]
    public required string Id { get; set; }

    /// <summary>
    /// SHA256 hash of the token for validation.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string TokenHash { get; set; }

    /// <summary>
    /// Whether the token is enabled for enrollment.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether to auto-approve enrollments using this token.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// When the token was first created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the token was last regenerated.
    /// </summary>
    public DateTimeOffset? LastRegeneratedAt { get; set; }
}
