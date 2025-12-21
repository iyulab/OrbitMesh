using System.Text.Json;
using System.Text.RegularExpressions;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IDeploymentProfileStore.
/// </summary>
public sealed class SqliteDeploymentProfileStore : IDeploymentProfileStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;

    public SqliteDeploymentProfileStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DeploymentProfile> CreateAsync(DeploymentProfile profile, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = ToEntity(profile);
        context.DeploymentProfiles.Add(entity);
        await context.SaveChangesAsync(ct);

        return profile;
    }

    public async Task<DeploymentProfile?> GetAsync(string profileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == profileId, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<DeploymentProfile> UpdateAsync(DeploymentProfile profile, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentProfiles.FindAsync([profile.Id], ct)
            ?? throw new InvalidOperationException($"DeploymentProfile {profile.Id} not found");

        UpdateEntity(entity, profile);
        await context.SaveChangesAsync(ct);

        return profile;
    }

    public async Task<bool> DeleteAsync(string profileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentProfiles.FindAsync([profileId], ct);
        if (entity is null) return false;

        context.DeploymentProfiles.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DeploymentProfile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentProfiles
            .AsNoTracking()
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentProfile>> GetEnabledAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentProfiles
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentProfile>> GetWatchingAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentProfiles
            .AsNoTracking()
            .Where(p => p.IsEnabled && p.WatchForChanges)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentProfile>> GetByAgentPatternAsync(
        string agentId,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentProfiles
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);

        // Pattern matching in memory (wildcards to regex)
        var matched = entities
            .Where(e => MatchesPattern(agentId, e.TargetAgentPattern))
            .Select(ToModel)
            .ToList();

        return matched;
    }

    public async Task UpdateLastDeployedAsync(
        string profileId,
        DateTimeOffset deployedAt,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentProfiles.FindAsync([profileId], ct);
        if (entity is not null)
        {
            entity.LastDeployedAt = deployedAt;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<PagedResult<DeploymentProfile>> GetPagedAsync(
        DeploymentProfileQueryOptions options,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.DeploymentProfiles.AsNoTracking();

        // Apply filters
        if (options.IsEnabled.HasValue)
            query = query.Where(p => p.IsEnabled == options.IsEnabled.Value);

        if (options.WatchForChanges.HasValue)
            query = query.Where(p => p.WatchForChanges == options.WatchForChanges.Value);

        if (!string.IsNullOrEmpty(options.NameContains))
            query = query.Where(p => p.Name.Contains(options.NameContains));

        if (!string.IsNullOrEmpty(options.TargetAgentPattern))
            query = query.Where(p => p.TargetAgentPattern == options.TargetAgentPattern);

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Fetch all filtered results (sorting/paging done in memory to avoid SQLite DateTimeOffset issues)
        var allEntities = await query.ToListAsync(ct);

        // Apply sorting in memory to avoid SQLite DateTimeOffset issues
        IEnumerable<DeploymentProfileEntity> sorted = options.SortBy switch
        {
            DeploymentProfileSortField.Name => options.SortDescending
                ? allEntities.OrderByDescending(p => p.Name)
                : allEntities.OrderBy(p => p.Name),
            DeploymentProfileSortField.LastDeployedAt => options.SortDescending
                ? allEntities.OrderByDescending(p => p.LastDeployedAt)
                : allEntities.OrderBy(p => p.LastDeployedAt),
            _ => options.SortDescending
                ? allEntities.OrderByDescending(p => p.CreatedAt)
                : allEntities.OrderBy(p => p.CreatedAt)
        };

        // Apply pagination in memory
        var entities = sorted
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToList();

        return PagedResult.Create(
            entities.Select(ToModel).ToList(),
            totalCount,
            options.Page,
            options.PageSize);
    }

    // ─────────────────────────────────────────────────────────────
    // Helper methods
    // ─────────────────────────────────────────────────────────────

    private static bool MatchesPattern(string agentId, string pattern)
    {
        // Convert wildcard pattern to regex: * → .*, ? → .
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(agentId, regexPattern, RegexOptions.IgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers
    // ─────────────────────────────────────────────────────────────

    private static DeploymentProfileEntity ToEntity(DeploymentProfile profile) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            SourcePath = profile.SourcePath,
            TargetAgentPattern = profile.TargetAgentPattern,
            TargetPath = profile.TargetPath,
            WatchForChanges = profile.WatchForChanges,
            DebounceMs = profile.DebounceMs,
            IncludePatternsJson = profile.IncludePatterns is null
                ? null
                : JsonSerializer.Serialize(profile.IncludePatterns),
            ExcludePatternsJson = profile.ExcludePatterns is null
                ? null
                : JsonSerializer.Serialize(profile.ExcludePatterns),
            DeleteOrphans = profile.DeleteOrphans,
            PreDeployScriptData = profile.PreDeployScript is null
                ? null
                : MessagePackSerializer.Serialize(profile.PreDeployScript),
            PostDeployScriptData = profile.PostDeployScript is null
                ? null
                : MessagePackSerializer.Serialize(profile.PostDeployScript),
            TransferMode = (int)profile.TransferMode,
            IsEnabled = profile.IsEnabled,
            CreatedAt = profile.CreatedAt,
            LastDeployedAt = profile.LastDeployedAt
        };

    private static void UpdateEntity(DeploymentProfileEntity entity, DeploymentProfile profile)
    {
        entity.Name = profile.Name;
        entity.Description = profile.Description;
        entity.SourcePath = profile.SourcePath;
        entity.TargetAgentPattern = profile.TargetAgentPattern;
        entity.TargetPath = profile.TargetPath;
        entity.WatchForChanges = profile.WatchForChanges;
        entity.DebounceMs = profile.DebounceMs;
        entity.IncludePatternsJson = profile.IncludePatterns is null
            ? null
            : JsonSerializer.Serialize(profile.IncludePatterns);
        entity.ExcludePatternsJson = profile.ExcludePatterns is null
            ? null
            : JsonSerializer.Serialize(profile.ExcludePatterns);
        entity.DeleteOrphans = profile.DeleteOrphans;
        entity.PreDeployScriptData = profile.PreDeployScript is null
            ? null
            : MessagePackSerializer.Serialize(profile.PreDeployScript);
        entity.PostDeployScriptData = profile.PostDeployScript is null
            ? null
            : MessagePackSerializer.Serialize(profile.PostDeployScript);
        entity.TransferMode = (int)profile.TransferMode;
        entity.IsEnabled = profile.IsEnabled;
        entity.LastDeployedAt = profile.LastDeployedAt;
    }

    private static DeploymentProfile ToModel(DeploymentProfileEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            SourcePath = entity.SourcePath,
            TargetAgentPattern = entity.TargetAgentPattern,
            TargetPath = entity.TargetPath,
            WatchForChanges = entity.WatchForChanges,
            DebounceMs = entity.DebounceMs,
            IncludePatterns = entity.IncludePatternsJson is null
                ? null
                : JsonSerializer.Deserialize<List<string>>(entity.IncludePatternsJson),
            ExcludePatterns = entity.ExcludePatternsJson is null
                ? null
                : JsonSerializer.Deserialize<List<string>>(entity.ExcludePatternsJson),
            DeleteOrphans = entity.DeleteOrphans,
            PreDeployScript = entity.PreDeployScriptData is null
                ? null
                : MessagePackSerializer.Deserialize<DeploymentScript>(entity.PreDeployScriptData),
            PostDeployScript = entity.PostDeployScriptData is null
                ? null
                : MessagePackSerializer.Deserialize<DeploymentScript>(entity.PostDeployScriptData),
            TransferMode = (FileTransferMode)entity.TransferMode,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            LastDeployedAt = entity.LastDeployedAt
        };
}
