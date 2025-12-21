using MessagePack;
using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IDeploymentExecutionStore.
/// </summary>
public sealed class SqliteDeploymentExecutionStore : IDeploymentExecutionStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;

    public SqliteDeploymentExecutionStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DeploymentExecution> CreateAsync(DeploymentExecution execution, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = ToEntity(execution);
        context.DeploymentExecutions.Add(entity);
        await context.SaveChangesAsync(ct);

        return execution;
    }

    public async Task<DeploymentExecution?> GetAsync(string executionId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<DeploymentExecution> UpdateAsync(DeploymentExecution execution, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentExecutions.FindAsync([execution.Id], ct)
            ?? throw new InvalidOperationException($"DeploymentExecution {execution.Id} not found");

        UpdateEntity(entity, execution);
        await context.SaveChangesAsync(ct);

        return execution;
    }

    public async Task<bool> DeleteAsync(string executionId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeploymentExecutions.FindAsync([executionId], ct);
        if (entity is null) return false;

        context.DeploymentExecutions.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DeploymentExecution>> GetByProfileAsync(
        string profileId,
        int limit = 50,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentExecutions
            .AsNoTracking()
            .Where(e => e.ProfileId == profileId)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .Select(ToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<DeploymentExecution>> GetByStatusAsync(
        DeploymentStatus status,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentExecutions
            .AsNoTracking()
            .Where(e => e.Status == (int)status)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(e => e.StartedAt)
            .Select(ToModel)
            .ToList();
    }

    public async Task<DeploymentExecution?> GetLatestByProfileAsync(
        string profileId,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentExecutions
            .AsNoTracking()
            .Where(e => e.ProfileId == profileId)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        var entity = entities
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefault();

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<DeploymentExecution>> GetInProgressAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.DeploymentExecutions
            .AsNoTracking()
            .Where(e => e.Status == (int)DeploymentStatus.Pending
                || e.Status == (int)DeploymentStatus.InProgress)
            .ToListAsync(ct);

        // Order in memory to avoid SQLite DateTimeOffset issues
        return entities
            .OrderByDescending(e => e.StartedAt)
            .Select(ToModel)
            .ToList();
    }

    public async Task<PagedResult<DeploymentExecution>> GetPagedAsync(
        DeploymentExecutionQueryOptions options,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.DeploymentExecutions.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrEmpty(options.ProfileId))
            query = query.Where(e => e.ProfileId == options.ProfileId);

        if (options.Status.HasValue)
            query = query.Where(e => e.Status == (int)options.Status.Value);

        if (options.Trigger.HasValue)
            query = query.Where(e => e.Trigger == (int)options.Trigger.Value);

        if (options.StartedAfter.HasValue)
            query = query.Where(e => e.StartedAt >= options.StartedAfter.Value);

        if (options.StartedBefore.HasValue)
            query = query.Where(e => e.StartedAt <= options.StartedBefore.Value);

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Fetch all filtered results (paging done in memory to avoid SQLite DateTimeOffset issues)
        var allEntities = await query.ToListAsync(ct);

        // Apply sorting in memory to avoid SQLite DateTimeOffset issues
        IEnumerable<DeploymentExecutionEntity> sorted = options.SortBy switch
        {
            DeploymentExecutionSortField.CompletedAt => options.SortDescending
                ? allEntities.OrderByDescending(e => e.CompletedAt)
                : allEntities.OrderBy(e => e.CompletedAt),
            DeploymentExecutionSortField.Status => options.SortDescending
                ? allEntities.OrderByDescending(e => e.Status)
                : allEntities.OrderBy(e => e.Status),
            _ => options.SortDescending
                ? allEntities.OrderByDescending(e => e.StartedAt)
                : allEntities.OrderBy(e => e.StartedAt)
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

    public async Task<Dictionary<DeploymentStatus, int>> GetStatusCountsAsync(
        string? profileId = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.DeploymentExecutions.AsQueryable();

        if (!string.IsNullOrEmpty(profileId))
            query = query.Where(e => e.ProfileId == profileId);

        var counts = await query
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => (DeploymentStatus)x.Status, x => x.Count);
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var count = await context.DeploymentExecutions
            .Where(e => e.CompletedAt.HasValue && e.CompletedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        return count;
    }

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers
    // ─────────────────────────────────────────────────────────────

    private static DeploymentExecutionEntity ToEntity(DeploymentExecution execution) =>
        new()
        {
            Id = execution.Id,
            ProfileId = execution.ProfileId,
            Status = (int)execution.Status,
            Trigger = (int)execution.Trigger,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            TotalAgents = execution.TotalAgents,
            SuccessfulAgents = execution.SuccessfulAgents,
            FailedAgents = execution.FailedAgents,
            AgentResultsData = execution.AgentResults is null
                ? null
                : MessagePackSerializer.Serialize(execution.AgentResults),
            ErrorMessage = execution.ErrorMessage,
            BytesTransferred = execution.BytesTransferred,
            FilesTransferred = execution.FilesTransferred
        };

    private static void UpdateEntity(DeploymentExecutionEntity entity, DeploymentExecution execution)
    {
        entity.Status = (int)execution.Status;
        entity.CompletedAt = execution.CompletedAt;
        entity.TotalAgents = execution.TotalAgents;
        entity.SuccessfulAgents = execution.SuccessfulAgents;
        entity.FailedAgents = execution.FailedAgents;
        entity.AgentResultsData = execution.AgentResults is null
            ? null
            : MessagePackSerializer.Serialize(execution.AgentResults);
        entity.ErrorMessage = execution.ErrorMessage;
        entity.BytesTransferred = execution.BytesTransferred;
        entity.FilesTransferred = execution.FilesTransferred;
    }

    private static DeploymentExecution ToModel(DeploymentExecutionEntity entity) =>
        new()
        {
            Id = entity.Id,
            ProfileId = entity.ProfileId,
            Status = (DeploymentStatus)entity.Status,
            Trigger = (DeploymentTrigger)entity.Trigger,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            TotalAgents = entity.TotalAgents,
            SuccessfulAgents = entity.SuccessfulAgents,
            FailedAgents = entity.FailedAgents,
            AgentResults = entity.AgentResultsData is null
                ? null
                : MessagePackSerializer.Deserialize<List<AgentDeploymentResult>>(entity.AgentResultsData),
            ErrorMessage = entity.ErrorMessage,
            BytesTransferred = entity.BytesTransferred,
            FilesTransferred = entity.FilesTransferred
        };
}
