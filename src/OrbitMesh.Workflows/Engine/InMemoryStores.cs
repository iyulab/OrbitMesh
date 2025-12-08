using System.Collections.Concurrent;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Engine;

/// <summary>
/// In-memory implementation of workflow instance store.
/// Suitable for development and testing.
/// </summary>
public sealed class InMemoryWorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();

    /// <inheritdoc />
    public Task SaveAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowInstance?> GetAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }

    /// <inheritdoc />
    public Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstance>> QueryAsync(
        WorkflowInstanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = _instances.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query.WorkflowId))
        {
            results = results.Where(i => i.WorkflowId == query.WorkflowId);
        }

        if (query.Status.HasValue)
        {
            results = results.Where(i => i.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.CorrelationId))
        {
            results = results.Where(i => i.CorrelationId == query.CorrelationId);
        }

        if (!string.IsNullOrEmpty(query.ParentInstanceId))
        {
            results = results.Where(i => i.ParentInstanceId == query.ParentInstanceId);
        }

        var list = results
            .OrderByDescending(i => i.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowInstance>>(list);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstance>> GetWaitingForEventAsync(
        string eventType,
        string? correlationKey = null,
        CancellationToken cancellationToken = default)
    {
        var results = _instances.Values
            .Where(i => i.Status == WorkflowStatus.Paused)
            .Where(i => i.StepInstances?.Values.Any(s =>
                s.Status == StepStatus.WaitingForEvent) ?? false)
            .ToList();

        // Additional filtering by correlation key would require storing event wait metadata
        // For now, return all paused instances waiting for events

        return Task.FromResult<IReadOnlyList<WorkflowInstance>>(results);
    }

    /// <summary>
    /// Clears all instances. Useful for testing.
    /// </summary>
    public void Clear() => _instances.Clear();
}

/// <summary>
/// In-memory implementation of workflow registry.
/// </summary>
public sealed class InMemoryWorkflowRegistry : IWorkflowRegistry
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _workflows = new();

    /// <inheritdoc />
    public Task RegisterAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        var key = GetKey(workflow.Id, workflow.Version);
        _workflows[key] = workflow;

        // Also store as latest version
        _workflows[$"{workflow.Id}:latest"] = workflow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> GetAsync(string workflowId, string? version = null, CancellationToken cancellationToken = default)
    {
        var key = version != null ? GetKey(workflowId, version) : $"{workflowId}:latest";
        _workflows.TryGetValue(key, out var workflow);
        return Task.FromResult(workflow);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Filter out the :latest entries to avoid duplicates
        var workflows = _workflows.Values
            .GroupBy(w => w.Id)
            .Select(g => g.OrderByDescending(w => w.Version).First())
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowDefinition>>(workflows);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string workflowId, string? version = null, CancellationToken cancellationToken = default)
    {
        if (version != null)
        {
            var key = GetKey(workflowId, version);
            _workflows.TryRemove(key, out _);
        }
        else
        {
            // Remove all versions
            var keysToRemove = _workflows.Keys.Where(k => k.StartsWith($"{workflowId}:", StringComparison.Ordinal)).ToList();
            foreach (var key in keysToRemove)
            {
                _workflows.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all workflows. Useful for testing.
    /// </summary>
    public void Clear() => _workflows.Clear();

    private static string GetKey(string workflowId, string version) => $"{workflowId}:{version}";
}
