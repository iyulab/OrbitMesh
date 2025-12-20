using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite-backed implementation of bootstrap token service.
/// Maintains a single, reusable bootstrap token with persistence.
/// </summary>
public sealed class SqliteBootstrapTokenStore : IBootstrapTokenService, IDisposable
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private readonly ILogger<SqliteBootstrapTokenStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public SqliteBootstrapTokenStore(
        IDbContextFactory<OrbitMeshDbContext> contextFactory,
        ILogger<SqliteBootstrapTokenStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _lock.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public async Task<BootstrapToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapToken
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            // Create initial token
            return await CreateInitialTokenAsync(cancellationToken);
        }

        return new BootstrapToken
        {
            Id = entity.Id,
            Token = null, // Never expose
            TokenHash = null, // Don't expose hash
            IsEnabled = entity.IsEnabled,
            AutoApprove = entity.AutoApprove,
            CreatedAt = entity.CreatedAt,
            LastRegeneratedAt = entity.LastRegeneratedAt
        };
    }

    /// <inheritdoc />
    public async Task<BootstrapToken> RegenerateAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var tokenValue = GenerateSecureToken();
            var tokenHash = HashToken(tokenValue);

            var entity = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                entity = new BootstrapTokenEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TokenHash = tokenHash,
                    IsEnabled = true,
                    AutoApprove = true,
                    CreatedAt = now,
                    LastRegeneratedAt = now
                };
                dbContext.BootstrapToken.Add(entity);
            }
            else
            {
                entity.TokenHash = tokenHash;
                entity.LastRegeneratedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Bootstrap token regenerated. TokenId: {TokenId}", entity.Id);

            return new BootstrapToken
            {
                Id = entity.Id,
                Token = tokenValue, // Only returned on regeneration
                TokenHash = null,
                IsEnabled = entity.IsEnabled,
                AutoApprove = entity.AutoApprove,
                CreatedAt = entity.CreatedAt,
                LastRegeneratedAt = entity.LastRegeneratedAt
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            // Create initial token first
            await CreateInitialTokenAsync(cancellationToken);
            entity = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);
        }

        entity!.IsEnabled = enabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap token {Action}. TokenId: {TokenId}",
            enabled ? "enabled" : "disabled",
            entity.Id);
    }

    /// <inheritdoc />
    public async Task SetAutoApproveAsync(bool autoApprove, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            // Create initial token first
            await CreateInitialTokenAsync(cancellationToken);
            entity = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);
        }

        entity!.AutoApprove = autoApprove;
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap token auto-approve set to {AutoApprove}. TokenId: {TokenId}",
            autoApprove,
            entity.Id);
    }

    /// <inheritdoc />
    public async Task<BootstrapTokenValidation?> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapToken
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            _logger.LogWarning("Bootstrap token validation failed: No token configured");
            return null;
        }

        if (!entity.IsEnabled)
        {
            _logger.LogWarning("Bootstrap token validation failed: Token is disabled");
            return null;
        }

        if (entity.TokenHash != tokenHash)
        {
            _logger.LogWarning("Bootstrap token validation failed: Invalid token");
            return null;
        }

        _logger.LogInformation("Bootstrap token validated successfully. TokenId: {TokenId}", entity.Id);

        return new BootstrapTokenValidation
        {
            TokenId = entity.Id,
            AutoApprove = entity.AutoApprove
        };
    }

    private async Task<BootstrapToken> CreateInitialTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Double-check after acquiring lock
            var existing = await dbContext.BootstrapToken.FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return new BootstrapToken
                {
                    Id = existing.Id,
                    Token = null,
                    TokenHash = null,
                    IsEnabled = existing.IsEnabled,
                    AutoApprove = existing.AutoApprove,
                    CreatedAt = existing.CreatedAt,
                    LastRegeneratedAt = existing.LastRegeneratedAt
                };
            }

            var now = DateTimeOffset.UtcNow;
            var tokenValue = GenerateSecureToken();
            var tokenHash = HashToken(tokenValue);

            var entity = new BootstrapTokenEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TokenHash = tokenHash,
                IsEnabled = true,
                AutoApprove = true,
                CreatedAt = now,
                LastRegeneratedAt = now
            };

            dbContext.BootstrapToken.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Bootstrap token initialized. TokenId: {TokenId}", entity.Id);

            // Note: We don't return the token value here since this is called from GetTokenAsync
            // which shouldn't expose the token value. Use RegenerateAsync to get the token value.
            return new BootstrapToken
            {
                Id = entity.Id,
                Token = null,
                TokenHash = null,
                IsEnabled = entity.IsEnabled,
                AutoApprove = entity.AutoApprove,
                CreatedAt = entity.CreatedAt,
                LastRegeneratedAt = entity.LastRegeneratedAt
            };
        }
        finally
        {
            _lock.Release();
        }
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
