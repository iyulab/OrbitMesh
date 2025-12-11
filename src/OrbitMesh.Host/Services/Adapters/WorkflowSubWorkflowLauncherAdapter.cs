using Microsoft.Extensions.Logging;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Execution;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Host.Services.Adapters;

/// <summary>
/// Adapter that enables workflow sub-workflow steps to launch child workflows
/// through the server's workflow engine.
/// </summary>
public sealed class WorkflowSubWorkflowLauncherAdapter : ISubWorkflowLauncher
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly ILogger<WorkflowSubWorkflowLauncherAdapter> _logger;

    public WorkflowSubWorkflowLauncherAdapter(
        IWorkflowEngine workflowEngine,
        IWorkflowRegistry workflowRegistry,
        ILogger<WorkflowSubWorkflowLauncherAdapter> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowRegistry = workflowRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SubWorkflowResult> LaunchAsync(
        string workflowId,
        string? version,
        IReadOnlyDictionary<string, object?>? input,
        string parentInstanceId,
        string parentStepId,
        bool waitForCompletion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Launching sub-workflow. WorkflowId: {WorkflowId}, Version: {Version}, ParentInstanceId: {ParentInstanceId}",
                workflowId, version ?? "latest", parentInstanceId);

            // Get workflow definition
            var definition = await _workflowRegistry.GetAsync(workflowId, version, cancellationToken);

            if (definition is null)
            {
                _logger.LogWarning("Sub-workflow not found. WorkflowId: {WorkflowId}, Version: {Version}",
                    workflowId, version ?? "latest");

                return new SubWorkflowResult
                {
                    Success = false,
                    Error = $"Workflow '{workflowId}' (version: {version ?? "latest"}) not found"
                };
            }

            // Start sub-workflow
            var instance = await _workflowEngine.StartAsync(
                definition,
                input,
                triggerId: null,
                correlationId: parentInstanceId,
                cancellationToken);

            _logger.LogInformation(
                "Sub-workflow started. WorkflowId: {WorkflowId}, InstanceId: {InstanceId}, ParentInstanceId: {ParentInstanceId}",
                workflowId, instance.Id, parentInstanceId);

            if (!waitForCompletion)
            {
                // Fire-and-forget mode
                return new SubWorkflowResult
                {
                    Success = true,
                    SubWorkflowInstanceId = instance.Id
                };
            }

            // Wait for completion
            var completedInstance = await WaitForWorkflowCompletionAsync(instance.Id, cancellationToken);

            if (completedInstance is null)
            {
                return new SubWorkflowResult
                {
                    Success = false,
                    SubWorkflowInstanceId = instance.Id,
                    Error = "Sub-workflow completion check failed"
                };
            }

            var success = completedInstance.Status == WorkflowStatus.Completed;

            return new SubWorkflowResult
            {
                Success = success,
                SubWorkflowInstanceId = instance.Id,
                Output = success ? completedInstance.Output : null,
                Error = completedInstance.Status == WorkflowStatus.Failed
                    ? completedInstance.Error
                    : null
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sub-workflow launch was cancelled. WorkflowId: {WorkflowId}", workflowId);
            return new SubWorkflowResult
            {
                Success = false,
                Error = "Sub-workflow launch was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sub-workflow launch failed. WorkflowId: {WorkflowId}", workflowId);
            return new SubWorkflowResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Polls for workflow completion until the workflow reaches a terminal state.
    /// </summary>
    private async Task<WorkflowInstance?> WaitForWorkflowCompletionAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        // Poll interval for workflow completion
        const int pollIntervalMs = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var instance = await _workflowEngine.GetInstanceAsync(instanceId, cancellationToken);

            if (instance is null)
            {
                return null;
            }

            // Check if workflow reached terminal state
            if (instance.Status is WorkflowStatus.Completed
                or WorkflowStatus.Failed
                or WorkflowStatus.Cancelled)
            {
                return instance;
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
        }

        return null;
    }
}
