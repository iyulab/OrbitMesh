using MessagePack;
using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IJobStore.
/// </summary>
public sealed class SqliteJobStore : IJobStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;

    public SqliteJobStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = ToEntity(job);
        context.Jobs.Add(entity);
        await context.SaveChangesAsync(ct);

        return job;
    }

    public async Task<Job?> GetAsync(string jobId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<Job> UpdateAsync(Job job, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Jobs.FindAsync([job.Id], ct)
            ?? throw new InvalidOperationException($"Job {job.Id} not found");

        UpdateEntity(entity, job);
        await context.SaveChangesAsync(ct);

        return job;
    }

    public async Task<bool> DeleteAsync(string jobId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.Jobs.FindAsync([jobId], ct);
        if (entity is null) return false;

        context.Jobs.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == (int)status)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<Job>> GetByAgentAsync(string agentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedAgentId == agentId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<PagedResult<Job>> GetPagedAsync(JobQueryOptions options, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Jobs.AsNoTracking();

        // Apply filters
        if (options.Status.HasValue)
            query = query.Where(j => j.Status == (int)options.Status.Value);

        if (!string.IsNullOrEmpty(options.AgentId))
            query = query.Where(j => j.AssignedAgentId == options.AgentId);

        if (!string.IsNullOrEmpty(options.Command))
            query = query.Where(j => j.Command.Contains(options.Command));

        if (options.CreatedAfter.HasValue)
            query = query.Where(j => j.CreatedAt >= options.CreatedAfter.Value);

        if (options.CreatedBefore.HasValue)
            query = query.Where(j => j.CreatedAt <= options.CreatedBefore.Value);

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Apply sorting
        query = options.SortBy switch
        {
            JobSortField.AssignedAt => options.SortDescending
                ? query.OrderByDescending(j => j.AssignedAt)
                : query.OrderBy(j => j.AssignedAt),
            JobSortField.CompletedAt => options.SortDescending
                ? query.OrderByDescending(j => j.CompletedAt)
                : query.OrderBy(j => j.CompletedAt),
            JobSortField.Priority => options.SortDescending
                ? query.OrderByDescending(j => j.Priority)
                : query.OrderBy(j => j.Priority),
            JobSortField.Status => options.SortDescending
                ? query.OrderByDescending(j => j.Status)
                : query.OrderBy(j => j.Status),
            _ => options.SortDescending
                ? query.OrderByDescending(j => j.CreatedAt)
                : query.OrderBy(j => j.CreatedAt)
        };

        // Apply pagination
        var entities = await query
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(ct);

        return PagedResult.Create(
            entities.Select(ToModel).ToList(),
            totalCount,
            options.Page,
            options.PageSize);
    }

    public async Task<IReadOnlyList<Job>> GetPendingJobsAsync(int limit = 100, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == (int)JobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<Job>> GetTimedOutJobsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var entities = await context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == (int)JobStatus.Running
                && j.TimeoutTicks.HasValue
                && j.StartedAt.HasValue)
            .ToListAsync(ct);

        // Filter in memory for timeout check (SQLite limitations)
        var timedOut = entities
            .Where(j => now - j.StartedAt!.Value > TimeSpan.FromTicks(j.TimeoutTicks!.Value))
            .Select(ToModel)
            .ToList();

        return timedOut;
    }

    public async Task<Dictionary<JobStatus, int>> GetStatusCountsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var counts = await context.Jobs
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => (JobStatus)x.Status, x => x.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers
    // ─────────────────────────────────────────────────────────────

    private static JobEntity ToEntity(Job job) =>
        new()
        {
            Id = job.Id,
            Command = job.Request.Command,
            Status = (int)job.Status,
            AssignedAgentId = job.AssignedAgentId,
            Priority = job.Request.Priority,
            ExecutionPattern = (int)job.Request.Pattern,
            RequestData = MessagePackSerializer.Serialize(job.Request),
            ResultData = job.Result is null ? null : MessagePackSerializer.Serialize(job.Result),
            ProgressData = job.LastProgress is null ? null : MessagePackSerializer.Serialize(job.LastProgress),
            Error = job.Error,
            ErrorCode = job.ErrorCode,
            RetryCount = job.RetryCount,
            TimeoutCount = job.TimeoutCount,
            TimeoutTicks = job.Request.Timeout?.Ticks,
            CancellationReason = job.CancellationReason,
            CreatedAt = job.CreatedAt,
            AssignedAt = job.AssignedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };

    private static void UpdateEntity(JobEntity entity, Job job)
    {
        entity.Status = (int)job.Status;
        entity.AssignedAgentId = job.AssignedAgentId;
        entity.ResultData = job.Result is null ? null : MessagePackSerializer.Serialize(job.Result);
        entity.ProgressData = job.LastProgress is null ? null : MessagePackSerializer.Serialize(job.LastProgress);
        entity.Error = job.Error;
        entity.ErrorCode = job.ErrorCode;
        entity.RetryCount = job.RetryCount;
        entity.TimeoutCount = job.TimeoutCount;
        entity.CancellationReason = job.CancellationReason;
        entity.AssignedAt = job.AssignedAt;
        entity.StartedAt = job.StartedAt;
        entity.CompletedAt = job.CompletedAt;
    }

    private static Job ToModel(JobEntity entity) =>
        new()
        {
            Id = entity.Id,
            Request = MessagePackSerializer.Deserialize<JobRequest>(entity.RequestData),
            Status = (JobStatus)entity.Status,
            AssignedAgentId = entity.AssignedAgentId,
            CreatedAt = entity.CreatedAt,
            AssignedAt = entity.AssignedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            Result = entity.ResultData is null
                ? null
                : MessagePackSerializer.Deserialize<JobResult>(entity.ResultData),
            LastProgress = entity.ProgressData is null
                ? null
                : MessagePackSerializer.Deserialize<JobProgress>(entity.ProgressData),
            Error = entity.Error,
            ErrorCode = entity.ErrorCode,
            RetryCount = entity.RetryCount,
            TimeoutCount = entity.TimeoutCount,
            CancellationReason = entity.CancellationReason
        };
}
