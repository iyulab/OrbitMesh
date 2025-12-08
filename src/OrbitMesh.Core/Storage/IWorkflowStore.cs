namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for workflow definitions and instances.
/// </summary>
public interface IWorkflowStore
{
    // ─────────────────────────────────────────────────────────────
    // Workflow Definitions
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a workflow definition.
    /// </summary>
    Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinition definition,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow definition by ID.
    /// </summary>
    Task<WorkflowDefinition?> GetDefinitionAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow definition by name and optional version.
    /// </summary>
    Task<WorkflowDefinition?> GetDefinitionByNameAsync(
        string name,
        string? version = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all workflow definitions.
    /// </summary>
    Task<IReadOnlyList<WorkflowDefinition>> GetAllDefinitionsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets active workflow definitions.
    /// </summary>
    Task<IReadOnlyList<WorkflowDefinition>> GetActiveDefinitionsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a workflow definition.
    /// </summary>
    Task<bool> DeleteDefinitionAsync(string workflowId, CancellationToken ct = default);

    // ─────────────────────────────────────────────────────────────
    // Workflow Instances
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a workflow instance.
    /// </summary>
    Task<WorkflowInstance> CreateInstanceAsync(
        WorkflowInstance instance,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow instance by ID.
    /// </summary>
    Task<WorkflowInstance?> GetInstanceAsync(
        string instanceId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a workflow instance.
    /// </summary>
    Task<WorkflowInstance> UpdateInstanceAsync(
        WorkflowInstance instance,
        CancellationToken ct = default);

    /// <summary>
    /// Gets workflow instances with pagination.
    /// </summary>
    Task<PagedResult<WorkflowInstance>> GetInstancesPagedAsync(
        WorkflowInstanceQueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Gets running workflow instances.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> GetRunningInstancesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a workflow instance.
    /// </summary>
    Task<bool> DeleteInstanceAsync(string instanceId, CancellationToken ct = default);
}

/// <summary>
/// Represents a workflow definition.
/// </summary>
public sealed record WorkflowDefinition
{
    /// <summary>
    /// Unique identifier for the workflow definition.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the workflow.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Version of the workflow definition.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Whether this workflow is active and can be triggered.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// The workflow definition content (YAML or JSON).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Content format (yaml or json).
    /// </summary>
    public string ContentFormat { get; init; } = "yaml";

    /// <summary>
    /// Trigger configuration.
    /// </summary>
    public WorkflowTrigger? Trigger { get; init; }

    /// <summary>
    /// Target agents/groups for this workflow.
    /// </summary>
    public WorkflowTarget? Target { get; init; }

    /// <summary>
    /// When the workflow was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the workflow was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Workflow trigger configuration.
/// </summary>
public sealed record WorkflowTrigger
{
    /// <summary>
    /// Trigger type (manual, file-watch, schedule, webhook).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Trigger-specific configuration.
    /// </summary>
    public Dictionary<string, object>? Config { get; init; }
}

/// <summary>
/// Workflow target configuration.
/// </summary>
public sealed record WorkflowTarget
{
    /// <summary>
    /// Target agent IDs (null or empty means all agents).
    /// </summary>
    public IReadOnlyList<string>? AgentIds { get; init; }

    /// <summary>
    /// Target group names.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; init; }

    /// <summary>
    /// Required capabilities.
    /// </summary>
    public IReadOnlyList<string>? Capabilities { get; init; }
}

/// <summary>
/// Represents a running or completed workflow instance.
/// </summary>
public sealed record WorkflowInstance
{
    /// <summary>
    /// Unique instance ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Reference to the workflow definition.
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Workflow name (denormalized for convenience).
    /// </summary>
    public required string WorkflowName { get; init; }

    /// <summary>
    /// Current status.
    /// </summary>
    public WorkflowInstanceStatus Status { get; init; } = WorkflowInstanceStatus.Pending;

    /// <summary>
    /// Input parameters for this instance.
    /// </summary>
    public Dictionary<string, object>? Input { get; init; }

    /// <summary>
    /// Output/result from the workflow.
    /// </summary>
    public Dictionary<string, object>? Output { get; init; }

    /// <summary>
    /// Error information if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Current step being executed.
    /// </summary>
    public string? CurrentStep { get; init; }

    /// <summary>
    /// Executed steps with their results.
    /// </summary>
    public IReadOnlyList<WorkflowStepResult>? StepResults { get; init; }

    /// <summary>
    /// When the instance was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// What triggered this instance.
    /// </summary>
    public string? TriggeredBy { get; init; }
}

/// <summary>
/// Result of a workflow step execution.
/// </summary>
public sealed record WorkflowStepResult
{
    public required string StepName { get; init; }
    public required string Status { get; init; }
    public string? AgentId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public Dictionary<string, object>? Output { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Status of a workflow instance.
/// </summary>
public enum WorkflowInstanceStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Compensating
}

/// <summary>
/// Query options for workflow instances.
/// </summary>
public sealed record WorkflowInstanceQueryOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? WorkflowId { get; init; }
    public WorkflowInstanceStatus? Status { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public DateTimeOffset? CreatedBefore { get; init; }
    public bool SortDescending { get; init; } = true;
}
