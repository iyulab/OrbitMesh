using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Host.Services.Workflows;

/// <summary>
/// Service for managing and processing workflow triggers.
/// </summary>
public interface IWorkflowTriggerService
{
    /// <summary>
    /// Registers triggers for a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition containing triggers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RegisterTriggersAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters triggers for a workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnregisterTriggersAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an incoming event and triggers any matching workflows.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="eventData">The event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of triggered workflow instance IDs.</returns>
    Task<IReadOnlyList<string>> ProcessEventAsync(
        string eventType,
        object? eventData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a webhook and triggers matching workflows.
    /// </summary>
    /// <param name="path">The webhook path.</param>
    /// <param name="method">HTTP method.</param>
    /// <param name="body">Request body.</param>
    /// <param name="headers">Request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of triggered workflow instance IDs.</returns>
    Task<IReadOnlyList<string>> ProcessWebhookAsync(
        string path,
        string method,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="input">Input variables.</param>
    /// <param name="initiatedBy">User/service that initiated the trigger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The triggered workflow instance ID.</returns>
    Task<string> TriggerManuallyAsync(
        string workflowId,
        IReadOnlyDictionary<string, object?>? input,
        string? initiatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered triggers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of workflow ID to triggers.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowTrigger>>> GetRegisteredTriggersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a specific trigger.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="triggerId">The trigger ID.</param>
    /// <param name="enabled">Whether to enable or disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetTriggerEnabledAsync(
        string workflowId,
        string triggerId,
        bool enabled,
        CancellationToken cancellationToken = default);
}
