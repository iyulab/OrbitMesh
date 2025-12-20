namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Service for managing the single bootstrap token for node enrollment.
/// The bootstrap token is a permanent, reusable token that can be enabled/disabled
/// and regenerated as needed. Unlike API tokens, it doesn't expire.
/// </summary>
public interface IBootstrapTokenService
{
    /// <summary>
    /// Gets the current bootstrap token configuration.
    /// Creates one if it doesn't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bootstrap token (token value only returned if includeToken is true).</returns>
    Task<BootstrapToken> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerates the bootstrap token value.
    /// The old token becomes invalid immediately.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new bootstrap token with the token value.</returns>
    Task<BootstrapToken> RegenerateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets whether the bootstrap token is enabled.
    /// When disabled, nodes cannot use the token to enroll.
    /// </summary>
    /// <param name="enabled">Whether to enable the token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets whether nodes enrolling with this token are automatically approved.
    /// </summary>
    /// <param name="autoApprove">Whether to auto-approve enrollments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAutoApproveAsync(bool autoApprove, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a bootstrap token.
    /// Unlike the old design, the token is NOT consumed - it can be reused.
    /// </summary>
    /// <param name="token">The token value to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result if valid, null if invalid or disabled.</returns>
    Task<BootstrapTokenValidation?> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bootstrap token information.
/// There is only one bootstrap token per server.
/// </summary>
public sealed record BootstrapToken
{
    /// <summary>
    /// Unique token ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The actual token value (only returned on regeneration).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Hash of the token for validation (internal use).
    /// </summary>
    public string? TokenHash { get; init; }

    /// <summary>
    /// Whether the token is enabled.
    /// When disabled, nodes cannot enroll using this token.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// If true, automatically approve enrollment.
    /// </summary>
    public bool AutoApprove { get; init; }

    /// <summary>
    /// When the token was first created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the token was last regenerated.
    /// </summary>
    public DateTimeOffset? LastRegeneratedAt { get; init; }
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
    /// If true, automatically approve the enrolling node.
    /// </summary>
    public bool AutoApprove { get; init; }
}
