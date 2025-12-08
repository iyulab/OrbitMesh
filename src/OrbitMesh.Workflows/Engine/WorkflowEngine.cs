using Microsoft.Extensions.Logging;
using OrbitMesh.Workflows.Execution;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Engine;

/// <summary>
/// Default implementation of the workflow execution engine.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly IWorkflowRegistry _registry;
    private readonly IStepExecutorFactory _executorFactory;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<WorkflowEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the WorkflowEngine.
    /// </summary>
    public WorkflowEngine(
        IWorkflowInstanceStore instanceStore,
        IWorkflowRegistry registry,
        IStepExecutorFactory executorFactory,
        IExpressionEvaluator expressionEvaluator,
        ILogger<WorkflowEngine> logger)
    {
        _instanceStore = instanceStore;
        _registry = registry;
        _executorFactory = executorFactory;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> StartAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object?>? input = null,
        string? triggerId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow {WorkflowId} v{Version}", workflow.Id, workflow.Version);

        // Create initial instance
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowId = workflow.Id,
            WorkflowVersion = workflow.Version,
            Status = WorkflowStatus.Pending,
            TriggerId = triggerId,
            TriggerType = triggerId != null ? "trigger" : "manual",
            Input = input,
            Variables = InitializeVariables(workflow, input),
            StepInstances = InitializeStepInstances(workflow),
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _instanceStore.SaveAsync(instance, cancellationToken);

        // Execute the workflow
        instance = await ExecuteWorkflowAsync(instance, workflow, cancellationToken);

        return instance;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> ResumeAsync(
        string instanceId,
        object? signal = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await _instanceStore.GetAsync(instanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {instanceId} not found");

        if (instance.Status != WorkflowStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot resume workflow in status {instance.Status}");
        }

        var workflow = await _registry.GetAsync(instance.WorkflowId, instance.WorkflowVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow definition {instance.WorkflowId} not found");

        _logger.LogInformation("Resuming workflow instance {InstanceId}", instanceId);

        // Apply signal to waiting step if provided
        if (signal != null)
        {
            instance = ApplySignalToWaitingStep(instance, signal);
        }

        instance = await ExecuteWorkflowAsync(instance, workflow, cancellationToken);

        return instance;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> CancelAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await _instanceStore.GetAsync(instanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {instanceId} not found");

        if (instance.IsTerminal)
        {
            throw new InvalidOperationException($"Cannot cancel workflow in terminal status {instance.Status}");
        }

        _logger.LogInformation("Cancelling workflow instance {InstanceId}: {Reason}", instanceId, reason);

        instance = instance with
        {
            Status = WorkflowStatus.Cancelled,
            CompletedAt = DateTimeOffset.UtcNow,
            Error = reason ?? "Cancelled by user"
        };

        await _instanceStore.UpdateAsync(instance, cancellationToken);

        return instance;
    }

    /// <inheritdoc />
    public Task<WorkflowInstance?> GetInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        return _instanceStore.GetAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ProcessApprovalAsync(
        string instanceId,
        string stepId,
        bool approved,
        string approver,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await _instanceStore.GetAsync(instanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {instanceId} not found");

        var stepInstance = instance.StepInstances?.GetValueOrDefault(stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in workflow instance");

        if (stepInstance.Status != StepStatus.WaitingForApproval)
        {
            throw new InvalidOperationException($"Step {stepId} is not waiting for approval");
        }

        _logger.LogInformation("Processing approval for step {StepId} in workflow {InstanceId}: approved={Approved}",
            stepId, instanceId, approved);

        // Update step status based on approval
        var updatedStepInstance = stepInstance with
        {
            Status = approved ? StepStatus.Completed : StepStatus.Failed,
            CompletedAt = DateTimeOffset.UtcNow,
            Output = new { approved, approver, comment }
        };

        var updatedStepInstances = new Dictionary<string, StepInstance>(instance.StepInstances!)
        {
            [stepId] = updatedStepInstance
        };

        instance = instance with
        {
            StepInstances = updatedStepInstances
        };

        // Resume workflow execution
        await ResumeAsync(instanceId, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SendEventAsync(
        string eventType,
        string? correlationKey = null,
        object? eventData = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending event {EventType} with correlation {CorrelationKey}", eventType, correlationKey);

        var waitingInstances = await _instanceStore.GetWaitingForEventAsync(eventType, correlationKey, cancellationToken);
        var resumedCount = 0;

        foreach (var instance in waitingInstances)
        {
            try
            {
                await ResumeAsync(instance.Id, eventData, cancellationToken);
                resumedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume workflow {InstanceId} on event {EventType}", instance.Id, eventType);
            }
        }

        return resumedCount;
    }

    private async Task<WorkflowInstance> ExecuteWorkflowAsync(
        WorkflowInstance instance,
        WorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        // Update status to running
        instance = instance with
        {
            Status = WorkflowStatus.Running,
            StartedAt = instance.StartedAt ?? DateTimeOffset.UtcNow
        };

        await _instanceStore.UpdateAsync(instance, cancellationToken);

        try
        {
            // Execute steps in dependency order
            instance = await ExecuteStepsAsync(instance, workflow, cancellationToken);

            // Check if all steps completed
            if (AllStepsCompleted(instance))
            {
                instance = instance with
                {
                    Status = WorkflowStatus.Completed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Output = CollectOutput(instance)
                };
            }
            else if (HasPausedSteps(instance))
            {
                instance = instance with
                {
                    Status = WorkflowStatus.Paused
                };
            }
            else if (HasFailedSteps(instance) && workflow.ErrorHandling?.ContinueOnError != true)
            {
                instance = instance with
                {
                    Status = WorkflowStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Error = GetFirstError(instance)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed for instance {InstanceId}", instance.Id);

            instance = instance with
            {
                Status = WorkflowStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                Error = ex.Message,
                ErrorCode = ex.GetType().Name
            };
        }

        await _instanceStore.UpdateAsync(instance, cancellationToken);

        return instance;
    }

    private async Task<WorkflowInstance> ExecuteStepsAsync(
        WorkflowInstance instance,
        WorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        var executionOrder = ComputeExecutionOrder(workflow.Steps);

        foreach (var stepId in executionOrder)
        {
            var step = workflow.Steps.First(s => s.Id == stepId);
            var stepInstance = instance.StepInstances![stepId];

            // Skip if already completed, skipped, or failed
            if (stepInstance.Status is StepStatus.Completed or StepStatus.Skipped or StepStatus.Failed)
            {
                continue;
            }

            // Check dependencies
            if (!AreDependenciesSatisfied(step, instance))
            {
                continue;
            }

            // Evaluate condition
            if (!string.IsNullOrEmpty(step.Condition))
            {
                var conditionResult = await _expressionEvaluator.EvaluateBoolAsync(
                    step.Condition,
                    instance.Variables ?? new Dictionary<string, object?>(),
                    cancellationToken);

                if (!conditionResult)
                {
                    stepInstance = stepInstance with
                    {
                        Status = StepStatus.Skipped,
                        CompletedAt = DateTimeOffset.UtcNow
                    };

                    instance = UpdateStepInstance(instance, stepId, stepInstance);
                    continue;
                }
            }

            // Execute the step
            var executor = _executorFactory.Create(step.Type);

            stepInstance = stepInstance with
            {
                Status = StepStatus.Running,
                StartedAt = DateTimeOffset.UtcNow
            };

            instance = UpdateStepInstance(instance, stepId, stepInstance);
            await _instanceStore.UpdateAsync(instance, cancellationToken);

            try
            {
                var context = new StepExecutionContext
                {
                    WorkflowInstance = instance,
                    Step = step,
                    StepInstance = stepInstance,
                    Variables = instance.Variables ?? new Dictionary<string, object?>()
                };

                var result = await executor.ExecuteAsync(context, cancellationToken);

                stepInstance = stepInstance with
                {
                    Status = result.Status,
                    CompletedAt = result.Status is StepStatus.Completed or StepStatus.Failed ? DateTimeOffset.UtcNow : null,
                    Output = result.Output,
                    Error = result.Error,
                    JobId = result.JobId,
                    SubWorkflowInstanceId = result.SubWorkflowInstanceId,
                    Branches = result.Branches
                };

                // Store output in variables if configured
                if (!string.IsNullOrEmpty(step.OutputVariable) && result.Output != null)
                {
                    instance.Variables![step.OutputVariable] = result.Output;
                }

                instance = UpdateStepInstance(instance, stepId, stepInstance);

                // If step is waiting for event or approval, pause workflow
                if (stepInstance.Status is StepStatus.WaitingForEvent or StepStatus.WaitingForApproval)
                {
                    await _instanceStore.UpdateAsync(instance, cancellationToken);
                    return instance;
                }

                // If step failed and no continue on error, stop execution
                if (stepInstance.Status == StepStatus.Failed && !step.ContinueOnError)
                {
                    return instance;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepId} failed in workflow {InstanceId}", stepId, instance.Id);

                stepInstance = stepInstance with
                {
                    Status = StepStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Error = ex.Message
                };

                instance = UpdateStepInstance(instance, stepId, stepInstance);

                if (!step.ContinueOnError)
                {
                    return instance;
                }
            }
        }

        return instance;
    }

    private static Dictionary<string, object?> InitializeVariables(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object?>? input)
    {
        var variables = new Dictionary<string, object?>();

        // Copy workflow-level variables
        if (workflow.Variables != null)
        {
            foreach (var (key, value) in workflow.Variables)
            {
                variables[key] = value;
            }
        }

        // Overlay input variables
        if (input != null)
        {
            foreach (var (key, value) in input)
            {
                variables[key] = value;
            }
        }

        return variables;
    }

    private static Dictionary<string, StepInstance> InitializeStepInstances(WorkflowDefinition workflow)
    {
        var instances = new Dictionary<string, StepInstance>();

        foreach (var step in workflow.Steps)
        {
            instances[step.Id] = new StepInstance
            {
                StepId = step.Id,
                Status = StepStatus.Pending
            };
        }

        return instances;
    }

    private static List<string> ComputeExecutionOrder(IReadOnlyList<WorkflowStep> steps)
    {
        // Topological sort based on dependencies
        var order = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(WorkflowStep step)
        {
            if (visited.Contains(step.Id))
            {
                return;
            }

            if (visiting.Contains(step.Id))
            {
                throw new InvalidOperationException($"Circular dependency detected at step {step.Id}");
            }

            visiting.Add(step.Id);

            if (step.DependsOn != null)
            {
                foreach (var depId in step.DependsOn)
                {
                    var depStep = steps.FirstOrDefault(s => s.Id == depId)
                        ?? throw new InvalidOperationException($"Dependency {depId} not found for step {step.Id}");
                    Visit(depStep);
                }
            }

            visiting.Remove(step.Id);
            visited.Add(step.Id);
            order.Add(step.Id);
        }

        foreach (var step in steps)
        {
            Visit(step);
        }

        return order;
    }

    private static bool AreDependenciesSatisfied(WorkflowStep step, WorkflowInstance instance)
    {
        if (step.DependsOn == null || step.DependsOn.Count == 0)
        {
            return true;
        }

        return step.DependsOn.All(depId =>
        {
            var depInstance = instance.StepInstances?.GetValueOrDefault(depId);
            return depInstance?.Status is StepStatus.Completed or StepStatus.Skipped;
        });
    }

    private static WorkflowInstance UpdateStepInstance(
        WorkflowInstance instance,
        string stepId,
        StepInstance stepInstance)
    {
        var updatedSteps = new Dictionary<string, StepInstance>(instance.StepInstances!)
        {
            [stepId] = stepInstance
        };

        return instance with { StepInstances = updatedSteps };
    }

    private static WorkflowInstance ApplySignalToWaitingStep(WorkflowInstance instance, object signal)
    {
        var waitingStep = instance.StepInstances?
            .FirstOrDefault(kvp => kvp.Value.Status == StepStatus.WaitingForEvent);

        if (waitingStep?.Value != null)
        {
            var updatedStep = waitingStep.Value.Value with
            {
                Output = signal,
                Status = StepStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow
            };

            return UpdateStepInstance(instance, waitingStep.Value.Key, updatedStep);
        }

        return instance;
    }

    private static bool AllStepsCompleted(WorkflowInstance instance)
    {
        return instance.StepInstances?.Values.All(s =>
            s.Status is StepStatus.Completed or StepStatus.Skipped) ?? true;
    }

    private static bool HasPausedSteps(WorkflowInstance instance)
    {
        return instance.StepInstances?.Values.Any(s =>
            s.Status is StepStatus.WaitingForEvent or StepStatus.WaitingForApproval) ?? false;
    }

    private static bool HasFailedSteps(WorkflowInstance instance)
    {
        return instance.StepInstances?.Values.Any(s => s.Status == StepStatus.Failed) ?? false;
    }

    private static string? GetFirstError(WorkflowInstance instance)
    {
        return instance.StepInstances?.Values
            .FirstOrDefault(s => s.Status == StepStatus.Failed)?.Error;
    }

    private static Dictionary<string, object?>? CollectOutput(WorkflowInstance instance)
    {
        var output = new Dictionary<string, object?>();

        if (instance.StepInstances != null)
        {
            foreach (var (stepId, stepInstance) in instance.StepInstances)
            {
                if (stepInstance.Output != null)
                {
                    output[stepId] = stepInstance.Output;
                }
            }
        }

        return output.Count > 0 ? output : null;
    }
}
