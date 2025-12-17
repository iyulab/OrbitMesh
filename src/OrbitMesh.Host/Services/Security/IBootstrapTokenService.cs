namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Service for managing one-time use bootstrap tokens for node enrollment.
/// Bootstrap tokens are used for initial node connection and are invalidated after first use.
/// </summary>
public interface IBootstrapTokenService
{
    /// <summary>
    /// Creates a new one-time use bootstrap token.
    /// </summary>
    /// <param name="request">Token creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created token (token value only returned once).</returns>
    Task<BootstrapToken> CreateAsync(
        CreateBootstrapTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a bootstrap token and consumes it (marks as used).
    /// Token can only be used once - subsequent calls will return null.
    /// </summary>
    /// <param name="token">The token value to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result if valid, null if invalid or already used.</returns>
    Task<BootstrapTokenValidation?> ValidateAndConsumeAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (not consumed, not expired) bootstrap tokens.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active tokens (without token values).</returns>
    Task<IReadOnlyList<BootstrapToken>> GetActiveTokensAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an unused bootstrap token.
    /// </summary>
    /// <param name="tokenId">Token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if revoked, false if not found or already consumed.</returns>
    Task<bool> RevokeAsync(
        string tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired tokens from storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tokens cleaned up.</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new bootstrap token.
/// </summary>
public sealed record CreateBootstrapTokenRequest
{
    /// <summary>
    /// Optional description for the token (e.g., "Node for production server").
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Token expiration in hours. Default is 24 hours.
    /// </summary>
    public int ExpirationHours { get; init; } = 24;

    /// <summary>
    /// Optional capabilities to pre-approve for the enrolling node.
    /// </summary>
    public IReadOnlyList<string> PreApprovedCapabilities { get; init; } = [];

    /// <summary>
    /// If true, automatically approve the node upon enrollment.
    /// </summary>
    public bool AutoApprove { get; init; }
}

/// <summary>
/// Bootstrap token information.
/// </summary>
public sealed record BootstrapToken
{
    /// <summary>
    /// Unique token ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The actual token value (only set on creation, never returned again).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Hash of the token for validation.
    /// </summary>
    public string? TokenHash { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When the token was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Whether the token has been consumed (used).
    /// </summary>
    public bool IsConsumed { get; init; }

    /// <summary>
    /// When the token was consumed.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; init; }

    /// <summary>
    /// Node ID that consumed the token.
    /// </summary>
    public string? ConsumedByNodeId { get; init; }

    /// <summary>
    /// Pre-approved capabilities for this token.
    /// </summary>
    public IReadOnlyList<string> PreApprovedCapabilities { get; init; } = [];

    /// <summary>
    /// If true, automatically approve enrollment.
    /// </summary>
    public bool AutoApprove { get; init; }
}

/// <summary>
/// Result of bootstrap token validation.
/// </summary>
public sealed record BootstrapTokenValidation
{
    /// <summary>
    /// Token ID.
    /// </summary>
    public required string TokenId { get; init; }

    /// <summary>
    /// Pre-approved capabilities from the token.
    /// </summary>
    public IReadOnlyList<string> PreApprovedCapabilities { get; init; } = [];

    /// <summary>
    /// Allowed capabilities for this token (alias for PreApprovedCapabilities).
    /// </summary>
    public IReadOnlyList<string> AllowedCapabilities => PreApprovedCapabilities;

    /// <summary>
    /// If true, automatically approve the enrolling node.
    /// </summary>
    public bool AutoApprove { get; init; }
}
