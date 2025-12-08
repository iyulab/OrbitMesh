using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IWorkflowStore.
/// </summary>
public sealed class SqliteWorkflowStore : IWorkflowStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteWorkflowStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ─────────────────────────────────────────────────────────────
    // Workflow Definitions
    // ─────────────────────────────────────────────────────────────

    public async Task<WorkflowDefinition> SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.WorkflowDefinitions.FindAsync([definition.Id], ct);

        if (existing is null)
        {
            var entity = ToEntity(definition);
            context.WorkflowDefinitions.Add(entity);
        }
        else
        {
            UpdateEntity(existing, definition);
        }

        await context.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(string workflowId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

        return entity is null ? null : ToDefinitionModel(entity);
    }

    public async Task<WorkflowDefinition?> GetDefinitionByNameAsync(string name, string? version = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.WorkflowDefinitions
            .AsNoTracking()
            .Where(w => w.Name == name);

        if (!string.IsNullOrEmpty(version))
        {
            query = query.Where(w => w.Version == version);
        }
        else
        {
            // Get the latest version
            query = query.OrderByDescending(w => w.Version);
        }

        var entity = await query.FirstOrDefaultAsync(ct);
        return entity is null ? null : ToDefinitionModel(entity);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllDefinitionsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.WorkflowDefinitions
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ThenByDescending(w => w.Version)
            .ToListAsync(ct);

        return entities.Select(ToDefinitionModel).ToList();
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetActiveDefinitionsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.WorkflowDefinitions
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

        return entities.Select(ToDefinitionModel).ToList();
    }

    public async Task<bool> DeleteDefinitionAsync(string workflowId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.WorkflowDefinitions.FindAsync([workflowId], ct);
        if (entity is null) return false;

        context.WorkflowDefinitions.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Workflow Instances
    // ─────────────────────────────────────────────────────────────

    public async Task<WorkflowInstance> CreateInstanceAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = ToInstanceEntity(instance);
        context.WorkflowInstances.Add(entity);
        await context.SaveChangesAsync(ct);

        return instance;
    }

    public async Task<WorkflowInstance?> GetInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        return entity is null ? null : ToInstanceModel(entity);
    }

    public async Task<WorkflowInstance> UpdateInstanceAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.WorkflowInstances.FindAsync([instance.Id], ct)
            ?? throw new InvalidOperationException($"WorkflowInstance {instance.Id} not found");

        UpdateInstanceEntity(entity, instance);
        await context.SaveChangesAsync(ct);

        return instance;
    }

    public async Task<PagedResult<WorkflowInstance>> GetInstancesPagedAsync(WorkflowInstanceQueryOptions options, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.WorkflowInstances.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrEmpty(options.WorkflowId))
            query = query.Where(i => i.WorkflowId == options.WorkflowId);

        if (options.Status.HasValue)
            query = query.Where(i => i.Status == (int)options.Status.Value);

        if (options.CreatedAfter.HasValue)
            query = query.Where(i => i.CreatedAt >= options.CreatedAfter.Value);

        if (options.CreatedBefore.HasValue)
            query = query.Where(i => i.CreatedAt <= options.CreatedBefore.Value);

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Apply sorting
        query = options.SortDescending
            ? query.OrderByDescending(i => i.CreatedAt)
            : query.OrderBy(i => i.CreatedAt);

        // Apply pagination
        var entities = await query
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(ct);

        return PagedResult.Create(
            entities.Select(ToInstanceModel).ToList(),
            totalCount,
            options.Page,
            options.PageSize);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetRunningInstancesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.Status == (int)WorkflowInstanceStatus.Running)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToInstanceModel).ToList();
    }

    public async Task<bool> DeleteInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.WorkflowInstances.FindAsync([instanceId], ct);
        if (entity is null) return false;

        context.WorkflowInstances.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers - Definitions
    // ─────────────────────────────────────────────────────────────

    private static WorkflowDefinitionEntity ToEntity(WorkflowDefinition definition) =>
        new()
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            Version = definition.Version,
            IsActive = definition.IsActive,
            Content = definition.Content,
            ContentFormat = definition.ContentFormat,
            TriggerJson = definition.Trigger is null ? null : JsonSerializer.Serialize(definition.Trigger, JsonOptions),
            TargetJson = definition.Target is null ? null : JsonSerializer.Serialize(definition.Target, JsonOptions),
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt
        };

    private static void UpdateEntity(WorkflowDefinitionEntity entity, WorkflowDefinition definition)
    {
        entity.Name = definition.Name;
        entity.Description = definition.Description;
        entity.Version = definition.Version;
        entity.IsActive = definition.IsActive;
        entity.Content = definition.Content;
        entity.ContentFormat = definition.ContentFormat;
        entity.TriggerJson = definition.Trigger is null ? null : JsonSerializer.Serialize(definition.Trigger, JsonOptions);
        entity.TargetJson = definition.Target is null ? null : JsonSerializer.Serialize(definition.Target, JsonOptions);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static WorkflowDefinition ToDefinitionModel(WorkflowDefinitionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Version = entity.Version,
            IsActive = entity.IsActive,
            Content = entity.Content,
            ContentFormat = entity.ContentFormat,
            Trigger = string.IsNullOrEmpty(entity.TriggerJson)
                ? null
                : JsonSerializer.Deserialize<WorkflowTrigger>(entity.TriggerJson, JsonOptions),
            Target = string.IsNullOrEmpty(entity.TargetJson)
                ? null
                : JsonSerializer.Deserialize<WorkflowTarget>(entity.TargetJson, JsonOptions),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

    // ─────────────────────────────────────────────────────────────
    // Mapping helpers - Instances
    // ─────────────────────────────────────────────────────────────

    private static WorkflowInstanceEntity ToInstanceEntity(WorkflowInstance instance) =>
        new()
        {
            Id = instance.Id,
            WorkflowId = instance.WorkflowId,
            WorkflowName = instance.WorkflowName,
            Status = (int)instance.Status,
            InputJson = instance.Input is null ? null : JsonSerializer.Serialize(instance.Input, JsonOptions),
            OutputJson = instance.Output is null ? null : JsonSerializer.Serialize(instance.Output, JsonOptions),
            Error = instance.Error,
            CurrentStep = instance.CurrentStep,
            StepResultsJson = instance.StepResults is null ? null : JsonSerializer.Serialize(instance.StepResults, JsonOptions),
            TriggeredBy = instance.TriggeredBy,
            CreatedAt = instance.CreatedAt,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt
        };

    private static void UpdateInstanceEntity(WorkflowInstanceEntity entity, WorkflowInstance instance)
    {
        entity.Status = (int)instance.Status;
        entity.OutputJson = instance.Output is null ? null : JsonSerializer.Serialize(instance.Output, JsonOptions);
        entity.Error = instance.Error;
        entity.CurrentStep = instance.CurrentStep;
        entity.StepResultsJson = instance.StepResults is null ? null : JsonSerializer.Serialize(instance.StepResults, JsonOptions);
        entity.StartedAt = instance.StartedAt;
        entity.CompletedAt = instance.CompletedAt;
    }

    private static WorkflowInstance ToInstanceModel(WorkflowInstanceEntity entity) =>
        new()
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            WorkflowName = entity.WorkflowName,
            Status = (WorkflowInstanceStatus)entity.Status,
            Input = string.IsNullOrEmpty(entity.InputJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.InputJson, JsonOptions),
            Output = string.IsNullOrEmpty(entity.OutputJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.OutputJson, JsonOptions),
            Error = entity.Error,
            CurrentStep = entity.CurrentStep,
            StepResults = string.IsNullOrEmpty(entity.StepResultsJson)
                ? null
                : JsonSerializer.Deserialize<List<WorkflowStepResult>>(entity.StepResultsJson, JsonOptions),
            TriggeredBy = entity.TriggeredBy,
            CreatedAt = entity.CreatedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt
        };
}
