using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite-backed implementation of bootstrap token service.
/// Uses IDbContextFactory for proper scoping with SignalR hubs.
/// </summary>
public sealed class SqliteBootstrapTokenStore : IBootstrapTokenService
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private readonly ILogger<SqliteBootstrapTokenStore> _logger;

    public SqliteBootstrapTokenStore(
        IDbContextFactory<OrbitMeshDbContext> contextFactory,
        ILogger<SqliteBootstrapTokenStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BootstrapToken> CreateAsync(
        CreateBootstrapTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var tokenValue = GenerateSecureToken();
        var tokenHash = HashToken(tokenValue);

        var now = DateTimeOffset.UtcNow;
        var entity = new BootstrapTokenEntity
        {
            Id = tokenId,
            TokenHash = tokenHash,
            Description = request.Description,
            CreatedAt = now,
            ExpiresAt = now.AddHours(request.ExpirationHours),
            IsConsumed = false,
            PreApprovedCapabilitiesJson = request.PreApprovedCapabilities.Count > 0
                ? JsonSerializer.Serialize(request.PreApprovedCapabilities)
                : null,
            AutoApprove = request.AutoApprove
        };

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.BootstrapTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap token created. TokenId: {TokenId}, ExpiresAt: {ExpiresAt}, AutoApprove: {AutoApprove}",
            tokenId,
            entity.ExpiresAt,
            entity.AutoApprove);

        return new BootstrapToken
        {
            Id = tokenId,
            Token = tokenValue, // Only returned on creation
            TokenHash = tokenHash,
            Description = request.Description,
            CreatedAt = now,
            ExpiresAt = entity.ExpiresAt,
            IsConsumed = false,
            PreApprovedCapabilities = request.PreApprovedCapabilities,
            AutoApprove = request.AutoApprove
        };
    }

    /// <inheritdoc />
    public async Task<BootstrapTokenValidation?> ValidateAndConsumeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Find token by hash using a transaction for atomicity
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var entity = await dbContext.BootstrapTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

            if (entity is null)
            {
                _logger.LogWarning("Bootstrap token validation failed: Token not found");
                return null;
            }

            if (entity.IsConsumed)
            {
                _logger.LogWarning(
                    "Bootstrap token validation failed: Token already consumed. TokenId: {TokenId}",
                    entity.Id);
                return null;
            }

            if (entity.ExpiresAt < now)
            {
                _logger.LogWarning(
                    "Bootstrap token validation failed: Token expired. TokenId: {TokenId}",
                    entity.Id);
                return null;
            }

            // Consume the token
            entity.IsConsumed = true;
            entity.ConsumedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Bootstrap token validated and consumed. TokenId: {TokenId}",
                entity.Id);

            var capabilities = string.IsNullOrEmpty(entity.PreApprovedCapabilitiesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.PreApprovedCapabilitiesJson) ?? [];

            return new BootstrapTokenValidation
            {
                TokenId = entity.Id,
                PreApprovedCapabilities = capabilities,
                AutoApprove = entity.AutoApprove
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error validating bootstrap token");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BootstrapToken>> GetActiveTokensAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite DateTimeOffset comparison requires client-side evaluation
        var entities = await dbContext.BootstrapTokens
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var filtered = entities
            .Where(t => !t.IsConsumed && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return filtered.Select(e => new BootstrapToken
        {
            Id = e.Id,
            Token = null, // Never expose
            TokenHash = null, // Don't expose hash
            Description = e.Description,
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt,
            IsConsumed = e.IsConsumed,
            ConsumedAt = e.ConsumedAt,
            ConsumedByNodeId = e.ConsumedByNodeId,
            PreApprovedCapabilities = string.IsNullOrEmpty(e.PreApprovedCapabilitiesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(e.PreApprovedCapabilitiesJson) ?? [],
            AutoApprove = e.AutoApprove
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        if (entity.IsConsumed)
        {
            _logger.LogWarning(
                "Cannot revoke bootstrap token: Already consumed. TokenId: {TokenId}",
                tokenId);
            return false;
        }

        dbContext.BootstrapTokens.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bootstrap token revoked. TokenId: {TokenId}", tokenId);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite DateTimeOffset comparison requires client-side evaluation
        var allTokens = await dbContext.BootstrapTokens
            .ToListAsync(cancellationToken);

        var expiredTokens = allTokens
            .Where(t => t.ExpiresAt < now || t.IsConsumed)
            .ToList();

        if (expiredTokens.Count == 0)
        {
            return 0;
        }

        dbContext.BootstrapTokens.RemoveRange(expiredTokens);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} expired/consumed bootstrap tokens", expiredTokens.Count);
        return expiredTokens.Count;
    }

    /// <summary>
    /// Marks a consumed token with the node ID that used it.
    /// </summary>
    public async Task MarkConsumedByNodeAsync(string tokenId, string nodeId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.BootstrapTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (entity is not null)
        {
            entity.ConsumedByNodeId = nodeId;
            await dbContext.SaveChangesAsync(cancellationToken);
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
