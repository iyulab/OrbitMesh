using Microsoft.Extensions.Logging;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Execution;

/// <summary>
/// Default implementation of step executor factory.
/// </summary>
public sealed class StepExecutorFactory : IStepExecutorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<StepType, Func<IStepExecutor>> _executors;

    /// <summary>
    /// Initializes a new instance of the StepExecutorFactory.
    /// </summary>
    public StepExecutorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _executors = new Dictionary<StepType, Func<IStepExecutor>>
        {
            [StepType.Job] = () => GetService<JobStepExecutor>(),
            [StepType.Delay] = () => GetService<DelayStepExecutor>(),
            [StepType.Transform] = () => GetService<TransformStepExecutor>(),
            [StepType.WaitForEvent] = () => GetService<WaitForEventStepExecutor>(),
            [StepType.Approval] = () => GetService<ApprovalStepExecutor>(),
            [StepType.Parallel] = () => GetService<ParallelStepExecutor>(),
            [StepType.Conditional] = () => GetService<ConditionalStepExecutor>(),
            [StepType.ForEach] = () => GetService<ForEachStepExecutor>(),
            [StepType.SubWorkflow] = () => GetService<SubWorkflowStepExecutor>(),
            [StepType.Notify] = () => GetService<NotifyStepExecutor>()
        };
    }

    /// <inheritdoc />
    public IStepExecutor Create(StepType stepType)
    {
        if (_executors.TryGetValue(stepType, out var factory))
        {
            return factory();
        }

        throw new NotSupportedException($"Step type {stepType} is not supported");
    }

    private T GetService<T>() where T : class
    {
        return (T)(_serviceProvider.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered"));
    }
}

/// <summary>
/// Executor for Job steps - dispatches jobs to agents.
/// </summary>
public sealed class JobStepExecutor : IStepExecutor
{
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<JobStepExecutor> _logger;

    /// <inheritdoc />
    public StepType StepType => StepType.Job;

    /// <summary>
    /// Initializes a new instance of JobStepExecutor.
    /// </summary>
    public JobStepExecutor(
        IJobDispatcher jobDispatcher,
        IExpressionEvaluator expressionEvaluator,
        ILogger<JobStepExecutor> logger)
    {
        _jobDispatcher = jobDispatcher;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as JobStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Job step");

        _logger.LogDebug("Executing job step {StepId}: command={Command}, pattern={Pattern}",
            context.Step.Id, config.Command, config.Pattern);

        // Interpolate payload if it contains expressions
        var payload = config.Payload;
        if (payload is string payloadStr && payloadStr.Contains("${", StringComparison.Ordinal))
        {
            var interpolated = await _expressionEvaluator.InterpolateAsync(payloadStr, context.Variables, cancellationToken);
            payload = interpolated;
        }

        var result = await _jobDispatcher.DispatchAsync(
            config.Command,
            config.Pattern,
            payload,
            config.Priority,
            config.RequiredTags,
            context.Step.Timeout,
            cancellationToken);

        if (result.Success)
        {
            return StepExecutionResult.Completed(result.JobResult) with
            {
                JobId = result.JobId
            };
        }

        return StepExecutionResult.Failed(result.Error ?? "Job execution failed") with
        {
            JobId = result.JobId
        };
    }
}

/// <summary>
/// Executor for Delay steps.
/// </summary>
public sealed class DelayStepExecutor : IStepExecutor
{
    /// <inheritdoc />
    public StepType StepType => StepType.Delay;

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as DelayStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Delay step");

        await Task.Delay(config.Duration, cancellationToken);

        return StepExecutionResult.Completed();
    }
}

/// <summary>
/// Executor for Transform steps.
/// </summary>
public sealed class TransformStepExecutor : IStepExecutor
{
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <inheritdoc />
    public StepType StepType => StepType.Transform;

    /// <summary>
    /// Initializes a new instance of TransformStepExecutor.
    /// </summary>
    public TransformStepExecutor(IExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as TransformStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Transform step");

        var result = await _expressionEvaluator.EvaluateAsync(config.Expression, context.Variables, cancellationToken);

        return StepExecutionResult.Completed(result);
    }
}

/// <summary>
/// Executor for WaitForEvent steps.
/// </summary>
public sealed class WaitForEventStepExecutor : IStepExecutor
{
    /// <inheritdoc />
    public StepType StepType => StepType.WaitForEvent;

    /// <inheritdoc />
    public Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        // Simply return waiting status - the engine will handle the actual waiting
        return Task.FromResult(StepExecutionResult.WaitingForEvent());
    }
}

/// <summary>
/// Executor for Approval steps.
/// </summary>
public sealed class ApprovalStepExecutor : IStepExecutor
{
    private readonly IApprovalNotifier? _approvalNotifier;

    /// <inheritdoc />
    public StepType StepType => StepType.Approval;

    /// <summary>
    /// Initializes a new instance of ApprovalStepExecutor.
    /// </summary>
    public ApprovalStepExecutor(IApprovalNotifier? approvalNotifier = null)
    {
        _approvalNotifier = approvalNotifier;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as ApprovalStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Approval step");

        // Notify approvers if notifier is configured
        if (_approvalNotifier != null)
        {
            await _approvalNotifier.NotifyApproversAsync(
                context.WorkflowInstance.Id,
                context.Step.Id,
                config.Approvers,
                config.Message,
                cancellationToken);
        }

        return StepExecutionResult.WaitingForApproval();
    }
}

/// <summary>
/// Executor for Parallel steps.
/// </summary>
public sealed class ParallelStepExecutor : IStepExecutor
{
    private readonly IStepExecutorFactory _executorFactory;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <inheritdoc />
    public StepType StepType => StepType.Parallel;

    /// <summary>
    /// Initializes a new instance of ParallelStepExecutor.
    /// </summary>
    public ParallelStepExecutor(IStepExecutorFactory executorFactory, IExpressionEvaluator expressionEvaluator)
    {
        _executorFactory = executorFactory;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as ParallelStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Parallel step");

        var branches = new List<BranchInstance>();
        var tasks = new List<Task<(int Index, StepExecutionResult Result)>>();

        // Execute branches in parallel with optional concurrency limit
        var semaphore = config.MaxConcurrency.HasValue
            ? new SemaphoreSlim(config.MaxConcurrency.Value)
            : null;

        for (var i = 0; i < config.Branches.Count; i++)
        {
            var index = i;
            var branch = config.Branches[i];

            tasks.Add(ExecuteBranchAsync(index, branch, context, semaphore, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);

        var hasFailure = false;
        foreach (var (index, result) in results)
        {
            var branchInstance = new BranchInstance
            {
                Index = index,
                Status = result.Status,
                Output = result.Output,
                Error = result.Error
            };

            branches.Add(branchInstance);

            if (result.Status == StepStatus.Failed)
            {
                hasFailure = true;
                if (config.FailFast)
                {
                    break;
                }
            }
        }

        semaphore?.Dispose();

        if (hasFailure)
        {
            return new StepExecutionResult
            {
                Status = StepStatus.Failed,
                Error = "One or more parallel branches failed",
                Branches = branches.AsReadOnly()
            };
        }

        return new StepExecutionResult
        {
            Status = StepStatus.Completed,
            Output = branches.Select(b => b.Output).ToList(),
            Branches = branches.AsReadOnly()
        };
    }

    private async Task<(int Index, StepExecutionResult Result)> ExecuteBranchAsync(
        int index,
        WorkflowStep branch,
        StepExecutionContext parentContext,
        SemaphoreSlim? semaphore,
        CancellationToken cancellationToken)
    {
        if (semaphore != null)
        {
            await semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            var executor = _executorFactory.Create(branch.Type);
            var branchContext = new StepExecutionContext
            {
                WorkflowInstance = parentContext.WorkflowInstance,
                Step = branch,
                StepInstance = new StepInstance { StepId = branch.Id, Status = StepStatus.Running },
                Variables = parentContext.Variables
            };

            var result = await executor.ExecuteAsync(branchContext, cancellationToken);
            return (index, result);
        }
        finally
        {
            semaphore?.Release();
        }
    }
}

/// <summary>
/// Executor for Conditional steps.
/// </summary>
public sealed class ConditionalStepExecutor : IStepExecutor
{
    private readonly IStepExecutorFactory _executorFactory;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <inheritdoc />
    public StepType StepType => StepType.Conditional;

    /// <summary>
    /// Initializes a new instance of ConditionalStepExecutor.
    /// </summary>
    public ConditionalStepExecutor(IStepExecutorFactory executorFactory, IExpressionEvaluator expressionEvaluator)
    {
        _executorFactory = executorFactory;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as ConditionalStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Conditional step");

        var conditionResult = await _expressionEvaluator.EvaluateBoolAsync(
            config.Expression,
            context.Variables,
            cancellationToken);

        var stepsToExecute = conditionResult ? config.ThenBranch : config.ElseBranch;

        if (stepsToExecute == null || stepsToExecute.Count == 0)
        {
            return StepExecutionResult.Completed(new { condition = conditionResult, branch = "none" });
        }

        // Execute the selected branch steps sequentially
        var outputs = new List<object?>();

        foreach (var step in stepsToExecute)
        {
            var executor = _executorFactory.Create(step.Type);
            var stepContext = new StepExecutionContext
            {
                WorkflowInstance = context.WorkflowInstance,
                Step = step,
                StepInstance = new StepInstance { StepId = step.Id, Status = StepStatus.Running },
                Variables = context.Variables
            };

            var result = await executor.ExecuteAsync(stepContext, cancellationToken);

            if (result.Status == StepStatus.Failed)
            {
                return result;
            }

            outputs.Add(result.Output);
        }

        return StepExecutionResult.Completed(new
        {
            condition = conditionResult,
            branch = conditionResult ? "then" : "else",
            outputs
        });
    }
}

/// <summary>
/// Executor for ForEach steps.
/// </summary>
public sealed class ForEachStepExecutor : IStepExecutor
{
    private readonly IStepExecutorFactory _executorFactory;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <inheritdoc />
    public StepType StepType => StepType.ForEach;

    /// <summary>
    /// Initializes a new instance of ForEachStepExecutor.
    /// </summary>
    public ForEachStepExecutor(IStepExecutorFactory executorFactory, IExpressionEvaluator expressionEvaluator)
    {
        _executorFactory = executorFactory;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as ForEachStepConfig
            ?? throw new InvalidOperationException("Invalid config type for ForEach step");

        // Evaluate the collection expression
        var collectionResult = await _expressionEvaluator.EvaluateAsync(
            config.Collection,
            context.Variables,
            cancellationToken);

        if (collectionResult is not IEnumerable<object> collection)
        {
            return StepExecutionResult.Failed("ForEach collection must be enumerable");
        }

        var items = collection.ToList();
        var branches = new List<BranchInstance>();

        // Execute iterations
        if (config.MaxConcurrency.HasValue && config.MaxConcurrency > 1)
        {
            // Parallel execution
            var semaphore = new SemaphoreSlim(config.MaxConcurrency.Value);
            var tasks = items.Select((item, index) =>
                ExecuteIterationAsync(index, item, config, context, semaphore, cancellationToken));

            var results = await Task.WhenAll(tasks);
            branches.AddRange(results);
            semaphore.Dispose();
        }
        else
        {
            // Sequential execution
            for (var i = 0; i < items.Count; i++)
            {
                var result = await ExecuteIterationAsync(i, items[i], config, context, null, cancellationToken);
                branches.Add(result);

                if (result.Status == StepStatus.Failed)
                {
                    break;
                }
            }
        }

        var hasFailure = branches.Any(b => b.Status == StepStatus.Failed);

        return new StepExecutionResult
        {
            Status = hasFailure ? StepStatus.Failed : StepStatus.Completed,
            Output = branches.Select(b => b.Output).ToList(),
            Branches = branches.AsReadOnly()
        };
    }

    private async Task<BranchInstance> ExecuteIterationAsync(
        int index,
        object item,
        ForEachStepConfig config,
        StepExecutionContext parentContext,
        SemaphoreSlim? semaphore,
        CancellationToken cancellationToken)
    {
        if (semaphore != null)
        {
            await semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            // Create iteration-scoped variables
            var iterationVariables = new Dictionary<string, object?>(parentContext.Variables)
            {
                [config.ItemVariable] = item,
                [config.IndexVariable] = index
            };

            var outputs = new List<object?>();

            foreach (var step in config.Steps)
            {
                var executor = _executorFactory.Create(step.Type);
                var stepContext = new StepExecutionContext
                {
                    WorkflowInstance = parentContext.WorkflowInstance,
                    Step = step,
                    StepInstance = new StepInstance { StepId = step.Id, Status = StepStatus.Running },
                    Variables = iterationVariables
                };

                var result = await executor.ExecuteAsync(stepContext, cancellationToken);

                if (result.Status == StepStatus.Failed)
                {
                    return new BranchInstance
                    {
                        Index = index,
                        Status = StepStatus.Failed,
                        Error = result.Error
                    };
                }

                outputs.Add(result.Output);
            }

            return new BranchInstance
            {
                Index = index,
                Status = StepStatus.Completed,
                Output = outputs.Count == 1 ? outputs[0] : outputs
            };
        }
        finally
        {
            semaphore?.Release();
        }
    }
}

/// <summary>
/// Executor for SubWorkflow steps.
/// </summary>
public sealed class SubWorkflowStepExecutor : IStepExecutor
{
    private readonly ISubWorkflowLauncher _subWorkflowLauncher;

    /// <inheritdoc />
    public StepType StepType => StepType.SubWorkflow;

    /// <summary>
    /// Initializes a new instance of SubWorkflowStepExecutor.
    /// </summary>
    public SubWorkflowStepExecutor(ISubWorkflowLauncher subWorkflowLauncher)
    {
        _subWorkflowLauncher = subWorkflowLauncher;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as SubWorkflowStepConfig
            ?? throw new InvalidOperationException("Invalid config type for SubWorkflow step");

        var result = await _subWorkflowLauncher.LaunchAsync(
            config.WorkflowId,
            config.Version,
            config.Input,
            context.WorkflowInstance.Id,
            context.Step.Id,
            config.WaitForCompletion,
            cancellationToken);

        if (result.Success)
        {
            return StepExecutionResult.Completed(result.Output) with
            {
                SubWorkflowInstanceId = result.SubWorkflowInstanceId
            };
        }

        return StepExecutionResult.Failed(result.Error ?? "Sub-workflow execution failed") with
        {
            SubWorkflowInstanceId = result.SubWorkflowInstanceId
        };
    }
}

/// <summary>
/// Executor for Notify steps.
/// </summary>
public sealed class NotifyStepExecutor : IStepExecutor
{
    private readonly INotificationSender _notificationSender;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <inheritdoc />
    public StepType StepType => StepType.Notify;

    /// <summary>
    /// Initializes a new instance of NotifyStepExecutor.
    /// </summary>
    public NotifyStepExecutor(INotificationSender notificationSender, IExpressionEvaluator expressionEvaluator)
    {
        _notificationSender = notificationSender;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc />
    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var config = context.Step.Config as NotifyStepConfig
            ?? throw new InvalidOperationException("Invalid config type for Notify step");

        // Interpolate message template
        var message = await _expressionEvaluator.InterpolateAsync(config.Message, context.Variables, cancellationToken);
        var subject = config.Subject != null
            ? await _expressionEvaluator.InterpolateAsync(config.Subject, context.Variables, cancellationToken)
            : null;

        var success = await _notificationSender.SendAsync(
            config.Channel,
            config.Target,
            message,
            subject,
            cancellationToken);

        if (success)
        {
            return StepExecutionResult.Completed(new { sent = true, channel = config.Channel.ToString() });
        }

        return StepExecutionResult.Failed("Failed to send notification");
    }
}
