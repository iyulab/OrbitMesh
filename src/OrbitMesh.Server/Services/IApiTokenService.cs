namespace OrbitMesh.Server.Services;

/// <summary>
/// Service for managing API tokens.
/// </summary>
public interface IApiTokenService
{
    /// <summary>
    /// Creates a new API token.
    /// </summary>
    /// <param name="request">The token creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created token with the plain text token value (only returned once).</returns>
    Task<ApiToken> CreateTokenAsync(CreateTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tokens (without the actual token values).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tokens.</returns>
    Task<IReadOnlyList<ApiToken>> GetAllTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a token and returns the token info if valid.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="requiredScope">Optional required scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token info if valid, null otherwise.</returns>
    Task<ApiToken?> ValidateTokenAsync(string token, string? requiredScope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a token by ID.
    /// </summary>
    /// <param name="tokenId">The token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token was revoked, false if not found.</returns>
    Task<bool> RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last used timestamp for a token.
    /// </summary>
    /// <param name="tokenId">The token ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateLastUsedAsync(string tokenId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new API token.
/// </summary>
public sealed record CreateTokenRequest
{
    /// <summary>
    /// Display name for the token.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Scopes granted to this token.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// Optional expiration in days from now.
    /// </summary>
    public int? ExpiresInDays { get; init; }
}

/// <summary>
/// API token information.
/// </summary>
public sealed record ApiToken
{
    /// <summary>
    /// Unique token ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the token.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The actual token value (only set on creation).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Hash of the token for validation.
    /// </summary>
    public string? TokenHash { get; init; }

    /// <summary>
    /// Scopes granted to this token.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// When the token was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the token expires (null for never).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// When the token was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
