using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Database entity for bootstrap tokens.
/// </summary>
[Table("BootstrapTokens")]
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

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsConsumed { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    [MaxLength(64)]
    public string? ConsumedByNodeId { get; set; }

    /// <summary>
    /// JSON-serialized list of pre-approved capabilities.
    /// </summary>
    public string? PreApprovedCapabilitiesJson { get; set; }

    public bool AutoApprove { get; set; }
}
