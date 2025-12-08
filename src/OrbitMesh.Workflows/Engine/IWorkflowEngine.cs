using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Engine;

/// <summary>
/// Core workflow execution engine interface.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Starts a new workflow instance from a definition.
    /// </summary>
    /// <param name="workflow">The workflow definition to execute.</param>
    /// <param name="input">Optional input variables.</param>
    /// <param name="triggerId">Optional trigger ID that started this workflow.</param>
    /// <param name="correlationId">Optional correlation ID for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created workflow instance.</returns>
    Task<WorkflowInstance> StartAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object?>? input = null,
        string? triggerId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused workflow instance.
    /// </summary>
    /// <param name="instanceId">The instance ID to resume.</param>
    /// <param name="signal">Optional signal data for WaitForEvent steps.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated workflow instance.</returns>
    Task<WorkflowInstance> ResumeAsync(
        string instanceId,
        object? signal = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running workflow instance.
    /// </summary>
    /// <param name="instanceId">The instance ID to cancel.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled workflow instance.</returns>
    Task<WorkflowInstance> CancelAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance by ID.
    /// </summary>
    /// <param name="instanceId">The instance ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workflow instance or null if not found.</returns>
    Task<WorkflowInstance?> GetInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes approval for an approval step.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="stepId">The approval step ID.</param>
    /// <param name="approved">Whether the step is approved.</param>
    /// <param name="approver">The approver identifier.</param>
    /// <param name="comment">Optional comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessApprovalAsync(
        string instanceId,
        string stepId,
        bool approved,
        string approver,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an event to waiting workflow instances.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="correlationKey">Optional correlation key.</param>
    /// <param name="eventData">Event payload data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of workflows that received the event.</returns>
    Task<int> SendEventAsync(
        string eventType,
        string? correlationKey = null,
        object? eventData = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Workflow instance store interface.
/// </summary>
public interface IWorkflowInstanceStore
{
    /// <summary>
    /// Saves a workflow instance.
    /// </summary>
    Task SaveAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance by ID.
    /// </summary>
    Task<WorkflowInstance?> GetAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a workflow instance.
    /// </summary>
    Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries workflow instances.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> QueryAsync(
        WorkflowInstanceQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets instances waiting for a specific event.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> GetWaitingForEventAsync(
        string eventType,
        string? correlationKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for workflow instances.
/// </summary>
public sealed record WorkflowInstanceQuery
{
    /// <summary>
    /// Filter by workflow definition ID.
    /// </summary>
    public string? WorkflowId { get; init; }

    /// <summary>
    /// Filter by status.
    /// </summary>
    public WorkflowStatus? Status { get; init; }

    /// <summary>
    /// Filter by correlation ID.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Filter by parent instance ID.
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// Skip count for pagination.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Take count for pagination.
    /// </summary>
    public int Take { get; init; } = 100;
}

/// <summary>
/// Workflow definition registry interface.
/// </summary>
public interface IWorkflowRegistry
{
    /// <summary>
    /// Registers a workflow definition.
    /// </summary>
    Task RegisterAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow definition by ID.
    /// </summary>
    Task<WorkflowDefinition?> GetAsync(string workflowId, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registered workflows.
    /// </summary>
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a workflow definition.
    /// </summary>
    Task RemoveAsync(string workflowId, string? version = null, CancellationToken cancellationToken = default);
}
