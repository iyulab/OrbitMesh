using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// In-memory implementation of bootstrap token service.
/// Maintains a single, reusable bootstrap token.
/// For production, use SqliteBootstrapTokenStore for persistence.
/// </summary>
public sealed class InMemoryBootstrapTokenService : IBootstrapTokenService
{
    private readonly object _lock = new();
    private readonly ILogger<InMemoryBootstrapTokenService> _logger;
    private BootstrapToken? _token;

    public InMemoryBootstrapTokenService(ILogger<InMemoryBootstrapTokenService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<BootstrapToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_token is null)
            {
                // Create initial token
                _token = CreateNewToken();
                _logger.LogInformation("Bootstrap token initialized. TokenId: {TokenId}", _token.Id);
            }

            // Return without the token value (security)
            return Task.FromResult(_token with { Token = null, TokenHash = null });
        }
    }

    /// <inheritdoc />
    public Task<BootstrapToken> RegenerateAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var tokenValue = GenerateSecureToken();
            var tokenHash = HashToken(tokenValue);

            if (_token is null)
            {
                _token = new BootstrapToken
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Token = tokenValue,
                    TokenHash = tokenHash,
                    IsEnabled = true,
                    AutoApprove = true,
                    CreatedAt = now,
                    LastRegeneratedAt = now
                };
            }
            else
            {
                _token = _token with
                {
                    Token = tokenValue,
                    TokenHash = tokenHash,
                    LastRegeneratedAt = now
                };
            }

            _logger.LogInformation("Bootstrap token regenerated. TokenId: {TokenId}", _token.Id);

            // Return with the token value (only time it's exposed)
            return Task.FromResult(_token);
        }
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            EnsureTokenExists();
            _token = _token! with { IsEnabled = enabled };

            _logger.LogInformation(
                "Bootstrap token {Action}. TokenId: {TokenId}",
                enabled ? "enabled" : "disabled",
                _token.Id);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetAutoApproveAsync(bool autoApprove, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            EnsureTokenExists();
            _token = _token! with { AutoApprove = autoApprove };

            _logger.LogInformation(
                "Bootstrap token auto-approve set to {AutoApprove}. TokenId: {TokenId}",
                autoApprove,
                _token.Id);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BootstrapTokenValidation?> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_token is null)
            {
                _logger.LogWarning("Bootstrap token validation failed: No token configured");
                return Task.FromResult<BootstrapTokenValidation?>(null);
            }

            if (!_token.IsEnabled)
            {
                _logger.LogWarning("Bootstrap token validation failed: Token is disabled");
                return Task.FromResult<BootstrapTokenValidation?>(null);
            }

            var tokenHash = HashToken(token);
            if (_token.TokenHash != tokenHash)
            {
                _logger.LogWarning("Bootstrap token validation failed: Invalid token");
                return Task.FromResult<BootstrapTokenValidation?>(null);
            }

            _logger.LogInformation("Bootstrap token validated successfully. TokenId: {TokenId}", _token.Id);

            return Task.FromResult<BootstrapTokenValidation?>(new BootstrapTokenValidation
            {
                TokenId = _token.Id,
                AutoApprove = _token.AutoApprove
            });
        }
    }

    private void EnsureTokenExists()
    {
        if (_token is null)
        {
            _token = CreateNewToken();
            _logger.LogInformation("Bootstrap token initialized. TokenId: {TokenId}", _token.Id);
        }
    }

    private static BootstrapToken CreateNewToken()
    {
        var now = DateTimeOffset.UtcNow;
        var tokenValue = GenerateSecureToken();
        var tokenHash = HashToken(tokenValue);

        return new BootstrapToken
        {
            Id = Guid.NewGuid().ToString("N"),
            Token = tokenValue,
            TokenHash = tokenHash,
            IsEnabled = true,
            AutoApprove = true,
            CreatedAt = now,
            LastRegeneratedAt = now
        };
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
