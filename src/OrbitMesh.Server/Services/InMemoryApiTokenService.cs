using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OrbitMesh.Server.Services;

/// <summary>
/// In-memory implementation of the API token service.
/// </summary>
public sealed class InMemoryApiTokenService : IApiTokenService
{
    private readonly ConcurrentDictionary<string, ApiToken> _tokens = new();

    /// <inheritdoc />
    public Task<ApiToken> CreateTokenAsync(CreateTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var tokenValue = GenerateSecureToken();
        var tokenHash = HashToken(tokenValue);

        var token = new ApiToken
        {
            Id = tokenId,
            Name = request.Name,
            Token = tokenValue,
            TokenHash = tokenHash,
            Scopes = request.Scopes,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.ExpiresInDays.HasValue
                ? DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays.Value)
                : null,
        };

        // Store without the plain text token
        var storedToken = token with { Token = null };
        _tokens[tokenId] = storedToken;

        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApiToken>> GetAllTokensAsync(CancellationToken cancellationToken = default)
    {
        var tokens = _tokens.Values
            .Select(t => t with { TokenHash = null })
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<ApiToken>>(tokens);
    }

    /// <inheritdoc />
    public Task<ApiToken?> ValidateTokenAsync(string token, string? requiredScope = null, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);

        var foundToken = _tokens.Values.FirstOrDefault(t => t.TokenHash == tokenHash);

        if (foundToken is null)
        {
            return Task.FromResult<ApiToken?>(null);
        }

        // Check expiration
        if (foundToken.ExpiresAt.HasValue && foundToken.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return Task.FromResult<ApiToken?>(null);
        }

        // Check scope
        if (!string.IsNullOrEmpty(requiredScope) && !foundToken.Scopes.Contains(requiredScope))
        {
            return Task.FromResult<ApiToken?>(null);
        }

        return Task.FromResult<ApiToken?>(foundToken with { TokenHash = null });
    }

    /// <inheritdoc />
    public Task<bool> RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens.TryRemove(tokenId, out _));
    }

    /// <inheritdoc />
    public Task UpdateLastUsedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(tokenId, out var token))
        {
            _tokens[tokenId] = token with { LastUsedAt = DateTimeOffset.UtcNow };
        }

        return Task.CompletedTask;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"orbit_{Convert.ToBase64String(bytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=')}";
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
