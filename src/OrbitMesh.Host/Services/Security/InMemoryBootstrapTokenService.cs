using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// In-memory implementation of bootstrap token service.
/// For production, replace with a persistent store.
/// </summary>
public sealed class InMemoryBootstrapTokenService : IBootstrapTokenService
{
    private readonly ConcurrentDictionary<string, BootstrapToken> _tokens = new();
    private readonly ILogger<InMemoryBootstrapTokenService> _logger;

    public InMemoryBootstrapTokenService(ILogger<InMemoryBootstrapTokenService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<BootstrapToken> CreateAsync(
        CreateBootstrapTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var tokenValue = GenerateSecureToken();
        var tokenHash = HashToken(tokenValue);

        var now = DateTimeOffset.UtcNow;
        var token = new BootstrapToken
        {
            Id = tokenId,
            Token = tokenValue,
            TokenHash = tokenHash,
            Description = request.Description,
            CreatedAt = now,
            ExpiresAt = now.AddHours(request.ExpirationHours),
            IsConsumed = false,
            PreApprovedCapabilities = request.PreApprovedCapabilities,
            AutoApprove = request.AutoApprove
        };

        // Store without plain text token
        var storedToken = token with { Token = null };
        _tokens[tokenId] = storedToken;

        _logger.LogInformation(
            "Bootstrap token created. TokenId: {TokenId}, ExpiresAt: {ExpiresAt}, AutoApprove: {AutoApprove}",
            tokenId,
            token.ExpiresAt,
            token.AutoApprove);

        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task<BootstrapTokenValidation?> ValidateAndConsumeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var now = DateTimeOffset.UtcNow;

        // Find token by hash
        var foundToken = _tokens.Values.FirstOrDefault(t => t.TokenHash == tokenHash);

        if (foundToken is null)
        {
            _logger.LogWarning("Bootstrap token validation failed: Token not found");
            return Task.FromResult<BootstrapTokenValidation?>(null);
        }

        // Check if already consumed
        if (foundToken.IsConsumed)
        {
            _logger.LogWarning(
                "Bootstrap token validation failed: Token already consumed. TokenId: {TokenId}",
                foundToken.Id);
            return Task.FromResult<BootstrapTokenValidation?>(null);
        }

        // Check expiration
        if (foundToken.ExpiresAt < now)
        {
            _logger.LogWarning(
                "Bootstrap token validation failed: Token expired. TokenId: {TokenId}",
                foundToken.Id);
            return Task.FromResult<BootstrapTokenValidation?>(null);
        }

        // Consume the token (mark as used)
        var consumedToken = foundToken with
        {
            IsConsumed = true,
            ConsumedAt = now
        };

        if (!_tokens.TryUpdate(foundToken.Id, consumedToken, foundToken))
        {
            // Another thread consumed the token
            _logger.LogWarning(
                "Bootstrap token validation failed: Race condition, token already consumed. TokenId: {TokenId}",
                foundToken.Id);
            return Task.FromResult<BootstrapTokenValidation?>(null);
        }

        _logger.LogInformation(
            "Bootstrap token validated and consumed. TokenId: {TokenId}",
            foundToken.Id);

        return Task.FromResult<BootstrapTokenValidation?>(new BootstrapTokenValidation
        {
            TokenId = foundToken.Id,
            PreApprovedCapabilities = foundToken.PreApprovedCapabilities,
            AutoApprove = foundToken.AutoApprove
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BootstrapToken>> GetActiveTokensAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var activeTokens = _tokens.Values
            .Where(t => !t.IsConsumed && t.ExpiresAt > now)
            .Select(t => t with { TokenHash = null }) // Don't expose hash
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<BootstrapToken>>(activeTokens);
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(tokenId, out var token))
        {
            if (token.IsConsumed)
            {
                _logger.LogWarning(
                    "Cannot revoke bootstrap token: Already consumed. TokenId: {TokenId}",
                    tokenId);
                return Task.FromResult(false);
            }

            if (_tokens.TryRemove(tokenId, out _))
            {
                _logger.LogInformation("Bootstrap token revoked. TokenId: {TokenId}", tokenId);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIds = _tokens
            .Where(kvp => kvp.Value.ExpiresAt < now || kvp.Value.IsConsumed)
            .Select(kvp => kvp.Key)
            .ToList();

        var cleaned = 0;
        foreach (var id in expiredIds)
        {
            if (_tokens.TryRemove(id, out _))
            {
                cleaned++;
            }
        }

        if (cleaned > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired/consumed bootstrap tokens", cleaned);
        }

        return Task.FromResult(cleaned);
    }

    /// <summary>
    /// Marks a consumed token with the node ID that used it.
    /// </summary>
    public Task MarkConsumedByNodeAsync(string tokenId, string nodeId)
    {
        if (_tokens.TryGetValue(tokenId, out var token))
        {
            var updated = token with { ConsumedByNodeId = nodeId };
            _tokens.TryUpdate(tokenId, updated, token);
        }

        return Task.CompletedTask;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"orbit_boot_{Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=')}";
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
