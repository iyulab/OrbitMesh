using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IAgentStore.
/// </summary>
public sealed class SqliteAgentStore : IAgentStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteAgentStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<AgentInfo> UpsertAsync(AgentInfo agent, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Agents.FindAsync([agent.Id], ct);

        if (existing is null)
        {
            var entity = ToEntity(agent);
            context.Agents.Add(entity);
        }
        else
        {
            UpdateEntity(existing, agent);
        }

        await context.SaveChangesAsync(ct);
        return agent;
    }

    public async Task<AgentInfo?> GetAsync(string agentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<AgentInfo?> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ConnectionId == connectionId, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<bool> RemoveAsync(string agentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Agents.FindAsync([agentId], ct);
        if (entity is null) return false;

        context.Agents.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AgentInfo>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Agents
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<AgentInfo>> GetByStatusAsync(AgentStatus status, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Agents
            .AsNoTracking()
            .Where(a => a.Status == (int)status)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<AgentInfo>> GetByGroupAsync(string group, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Agents
            .AsNoTracking()
            .Where(a => a.Group == group)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<AgentInfo>> GetByCapabilityAsync(string capability, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Note: This does a string search in JSON. For better performance,
        // consider a separate Capabilities table in a future optimization.
        var entities = await context.Agents
            .AsNoTracking()
            .Where(a => a.CapabilitiesJson != null && a.CapabilitiesJson.Contains(capability))
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        // Filter in memory for exact capability match
        var filtered = entities
            .Select(ToModel)
            .Where(a => a.HasCapability(capability))
            .ToList();

        return filtered;
    }

    public async Task<PagedResult<AgentInfo>> GetPagedAsync(AgentQueryOptions options, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Agents.AsNoTracking();

        // Apply filters
        if (options.Status.HasValue)
            query = query.Where(a => a.Status == (int)options.Status.Value);

        if (!string.IsNullOrEmpty(options.Group))
            query = query.Where(a => a.Group == options.Group);

        if (!string.IsNullOrEmpty(options.SearchTerm))
        {
            query = query.Where(a =>
                a.Name.Contains(options.SearchTerm) ||
                (a.Hostname != null && a.Hostname.Contains(options.SearchTerm)));
        }

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Apply sorting
        query = options.SortBy switch
        {
            AgentSortField.RegisteredAt => options.SortDescending
                ? query.OrderByDescending(a => a.RegisteredAt)
                : query.OrderBy(a => a.RegisteredAt),
            AgentSortField.LastHeartbeat => options.SortDescending
                ? query.OrderByDescending(a => a.LastHeartbeat)
                : query.OrderBy(a => a.LastHeartbeat),
            AgentSortField.Status => options.SortDescending
                ? query.OrderByDescending(a => a.Status)
                : query.OrderBy(a => a.Status),
            AgentSortField.Group => options.SortDescending
                ? query.OrderByDescending(a => a.Group)
                : query.OrderBy(a => a.Group),
            _ => options.SortDescending
                ? query.OrderByDescending(a => a.Name)
                : query.OrderBy(a => a.Name)
        };

        // Apply pagination
        var entities = await query
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(ct);

        // Filter by capability in memory if needed
        IReadOnlyList<AgentInfo> items = entities.Select(ToModel).ToList();
        if (!string.IsNullOrEmpty(options.Capability))
        {
            items = items.Where(a => a.HasCapability(options.Capability)).ToList();
        }

        return PagedResult.Create(items, totalCount, options.Page, options.PageSize);
    }

    public async Task UpdateHeartbeatAsync(string agentId, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        await context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastHeartbeat, timestamp), ct);
    }

    public async Task UpdateStatusAsync(string agentId, AgentStatus status, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        await context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, (int)status), ct);
    }

    public async Task<IReadOnlyList<AgentInfo>> GetStaleAgentsAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow - timeout;

        var entities = await context.Agents
            .AsNoTracking()
            .Where(a => a.LastHeartbeat < cutoff || a.LastHeartbeat == null)
            .Where(a => a.Status == (int)AgentStatus.Ready || a.Status == (int)AgentStatus.Running)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<Dictionary<AgentStatus, int>> GetStatusCountsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var counts = await context.Agents
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => (AgentStatus)x.Status, x => x.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers
    // ─────────────────────────────────────────────────────────────

    private static AgentEntity ToEntity(AgentInfo agent) =>
        new()
        {
            Id = agent.Id,
            Name = agent.Name,
            Status = (int)agent.Status,
            Group = agent.Group,
            Hostname = agent.Hostname,
            Version = agent.Version,
            ConnectionId = agent.ConnectionId,
            TagsJson = agent.Tags.Count > 0 ? JsonSerializer.Serialize(agent.Tags, JsonOptions) : null,
            CapabilitiesJson = agent.Capabilities.Count > 0 ? JsonSerializer.Serialize(agent.Capabilities, JsonOptions) : null,
            MetadataJson = agent.Metadata?.Count > 0 ? JsonSerializer.Serialize(agent.Metadata, JsonOptions) : null,
            RegisteredAt = agent.RegisteredAt,
            LastHeartbeat = agent.LastHeartbeat
        };

    private static void UpdateEntity(AgentEntity entity, AgentInfo agent)
    {
        entity.Name = agent.Name;
        entity.Status = (int)agent.Status;
        entity.Group = agent.Group;
        entity.Hostname = agent.Hostname;
        entity.Version = agent.Version;
        entity.ConnectionId = agent.ConnectionId;
        entity.TagsJson = agent.Tags.Count > 0 ? JsonSerializer.Serialize(agent.Tags, JsonOptions) : null;
        entity.CapabilitiesJson = agent.Capabilities.Count > 0 ? JsonSerializer.Serialize(agent.Capabilities, JsonOptions) : null;
        entity.MetadataJson = agent.Metadata?.Count > 0 ? JsonSerializer.Serialize(agent.Metadata, JsonOptions) : null;
        entity.LastHeartbeat = agent.LastHeartbeat;
    }

    private static AgentInfo ToModel(AgentEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Status = (AgentStatus)entity.Status,
            Group = entity.Group,
            Hostname = entity.Hostname,
            Version = entity.Version,
            ConnectionId = entity.ConnectionId,
            Tags = string.IsNullOrEmpty(entity.TagsJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.TagsJson, JsonOptions) ?? [],
            Capabilities = string.IsNullOrEmpty(entity.CapabilitiesJson)
                ? []
                : JsonSerializer.Deserialize<List<AgentCapability>>(entity.CapabilitiesJson, JsonOptions) ?? [],
            Metadata = string.IsNullOrEmpty(entity.MetadataJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson, JsonOptions),
            RegisteredAt = entity.RegisteredAt,
            LastHeartbeat = entity.LastHeartbeat
        };
}
