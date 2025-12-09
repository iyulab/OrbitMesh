using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.BuiltIn;

/// <summary>
/// Fluent builder for creating workflow definitions.
/// </summary>
public sealed class WorkflowBuilder
{
    private string _id;
    private string _name = string.Empty;
    private string _version = "1.0.0";
    private string? _description;
    private readonly List<string> _tags = [];
    private readonly List<WorkflowStep> _steps = [];
    private readonly List<WorkflowTrigger> _triggers = [];
    private readonly Dictionary<string, object?> _variables = [];
    private TimeSpan? _timeout;
    private int _maxRetries;
    private bool _isEnabled = true;
    private WorkflowErrorHandling? _errorHandling;

    /// <summary>
    /// Initializes a new workflow builder.
    /// </summary>
    public WorkflowBuilder()
    {
        _id = $"workflow-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates a new workflow builder.
    /// </summary>
    public static WorkflowBuilder Create() => new();

    /// <summary>
    /// Creates a new workflow builder with a specific ID.
    /// </summary>
    public static WorkflowBuilder Create(string id) => new() { _id = id };

    /// <summary>
    /// Sets the workflow ID.
    /// </summary>
    public WorkflowBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the workflow name.
    /// </summary>
    public WorkflowBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the workflow version.
    /// </summary>
    public WorkflowBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the workflow description.
    /// </summary>
    public WorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Adds tags to the workflow.
    /// </summary>
    public WorkflowBuilder WithTags(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    /// <summary>
    /// Sets the workflow timeout.
    /// </summary>
    public WorkflowBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the maximum retries for the workflow.
    /// </summary>
    public WorkflowBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Sets whether the workflow is enabled.
    /// </summary>
    public WorkflowBuilder Enabled(bool enabled = true)
    {
        _isEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Adds a variable to the workflow.
    /// </summary>
    public WorkflowBuilder WithVariable(string name, object? value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple variables to the workflow.
    /// </summary>
    public WorkflowBuilder WithVariables(IDictionary<string, object?> variables)
    {
        foreach (var (key, value) in variables)
        {
            _variables[key] = value;
        }
        return this;
    }

    /// <summary>
    /// Sets the error handling configuration.
    /// </summary>
    public WorkflowBuilder WithErrorHandling(ErrorStrategy strategy, bool continueOnError = false, string? compensationWorkflowId = null)
    {
        _errorHandling = new WorkflowErrorHandling
        {
            Strategy = strategy,
            ContinueOnError = continueOnError,
            CompensationWorkflowId = compensationWorkflowId
        };
        return this;
    }

    /// <summary>
    /// Adds a manual trigger.
    /// </summary>
    public WorkflowBuilder WithManualTrigger(string? id = null, string? name = null)
    {
        _triggers.Add(new ManualTrigger
        {
            Id = id ?? "manual",
            Name = name ?? "Manual Trigger"
        });
        return this;
    }

    /// <summary>
    /// Adds a schedule trigger with a cron expression.
    /// </summary>
    public WorkflowBuilder WithScheduleTrigger(string cronExpression, string? id = null, string? name = null)
    {
        _triggers.Add(new ScheduleTrigger
        {
            Id = id ?? "schedule",
            Name = name ?? "Scheduled Trigger",
            CronExpression = cronExpression
        });
        return this;
    }

    /// <summary>
    /// Adds a schedule trigger with an interval.
    /// </summary>
    public WorkflowBuilder WithIntervalTrigger(TimeSpan interval, string? id = null, string? name = null)
    {
        _triggers.Add(new ScheduleTrigger
        {
            Id = id ?? "interval",
            Name = name ?? "Interval Trigger",
            Interval = interval
        });
        return this;
    }

    /// <summary>
    /// Adds an event trigger.
    /// </summary>
    public WorkflowBuilder WithEventTrigger(string eventType, string? filter = null, string? id = null)
    {
        _triggers.Add(new EventTrigger
        {
            Id = id ?? $"event-{eventType}",
            Name = $"Event: {eventType}",
            EventType = eventType,
            Filter = filter
        });
        return this;
    }

    /// <summary>
    /// Adds a webhook trigger.
    /// </summary>
    public WorkflowBuilder WithWebhookTrigger(string path, string? id = null, IReadOnlyList<string>? methods = null)
    {
        _triggers.Add(new WebhookTrigger
        {
            Id = id ?? "webhook",
            Name = $"Webhook: {path}",
            Path = path,
            Methods = methods ?? ["POST"]
        });
        return this;
    }

    /// <summary>
    /// Adds a job completion trigger.
    /// </summary>
    public WorkflowBuilder WithJobCompletionTrigger(string commandPattern, IReadOnlyList<string>? statuses = null, string? id = null)
    {
        _triggers.Add(new JobCompletionTrigger
        {
            Id = id ?? "job-completion",
            Name = $"Job Completion: {commandPattern}",
            CommandPattern = commandPattern,
            Statuses = statuses ?? ["Completed", "Failed"]
        });
        return this;
    }

    /// <summary>
    /// Adds a custom trigger.
    /// </summary>
    public WorkflowBuilder WithTrigger(WorkflowTrigger trigger)
    {
        _triggers.Add(trigger);
        return this;
    }

    /// <summary>
    /// Adds a job step.
    /// </summary>
    public WorkflowBuilder AddJobStep(
        string id,
        string name,
        string command,
        string pattern = "*",
        object? payload = null,
        IReadOnlyList<string>? dependsOn = null,
        string? condition = null,
        int maxRetries = 0,
        TimeSpan? timeout = null,
        string? outputVariable = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Job,
            Config = new JobStepConfig
            {
                Command = command,
                Pattern = pattern,
                Payload = payload
            },
            DependsOn = dependsOn,
            Condition = condition,
            MaxRetries = maxRetries,
            Timeout = timeout,
            OutputVariable = outputVariable
        });
        return this;
    }

    /// <summary>
    /// Adds a delay step.
    /// </summary>
    public WorkflowBuilder AddDelayStep(
        string id,
        string name,
        TimeSpan duration,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Delay,
            Config = new DelayStepConfig { Duration = duration },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a parallel step with branches.
    /// </summary>
    public WorkflowBuilder AddParallelStep(
        string id,
        string name,
        IReadOnlyList<WorkflowStep> branches,
        int? maxConcurrency = null,
        bool failFast = true,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Parallel,
            Config = new ParallelStepConfig
            {
                Branches = branches,
                MaxConcurrency = maxConcurrency,
                FailFast = failFast
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a conditional step.
    /// </summary>
    public WorkflowBuilder AddConditionalStep(
        string id,
        string name,
        string expression,
        IReadOnlyList<WorkflowStep> thenBranch,
        IReadOnlyList<WorkflowStep>? elseBranch = null,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Conditional,
            Config = new ConditionalStepConfig
            {
                Expression = expression,
                ThenBranch = thenBranch,
                ElseBranch = elseBranch
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a foreach step.
    /// </summary>
    public WorkflowBuilder AddForEachStep(
        string id,
        string name,
        string collection,
        IReadOnlyList<WorkflowStep> steps,
        string itemVariable = "item",
        int? maxConcurrency = null,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.ForEach,
            Config = new ForEachStepConfig
            {
                Collection = collection,
                ItemVariable = itemVariable,
                Steps = steps,
                MaxConcurrency = maxConcurrency
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a sub-workflow step.
    /// </summary>
    public WorkflowBuilder AddSubWorkflowStep(
        string id,
        string name,
        string workflowId,
        IReadOnlyDictionary<string, object?>? input = null,
        bool waitForCompletion = true,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.SubWorkflow,
            Config = new SubWorkflowStepConfig
            {
                WorkflowId = workflowId,
                Input = input,
                WaitForCompletion = waitForCompletion
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a notification step.
    /// </summary>
    public WorkflowBuilder AddNotifyStep(
        string id,
        string name,
        NotifyChannel channel,
        string target,
        string message,
        string? subject = null,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Notify,
            Config = new NotifyStepConfig
            {
                Channel = channel,
                Target = target,
                Message = message,
                Subject = subject
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds an approval step.
    /// </summary>
    public WorkflowBuilder AddApprovalStep(
        string id,
        string name,
        IReadOnlyList<string> approvers,
        int requiredApprovals = 1,
        string? message = null,
        TimeSpan? timeout = null,
        ApprovalTimeoutAction timeoutAction = ApprovalTimeoutAction.Fail,
        IReadOnlyList<string>? dependsOn = null)
    {
        _steps.Add(new WorkflowStep
        {
            Id = id,
            Name = name,
            Type = StepType.Approval,
            Config = new ApprovalStepConfig
            {
                Approvers = approvers,
                RequiredApprovals = requiredApprovals,
                Message = message,
                Timeout = timeout,
                TimeoutAction = timeoutAction
            },
            DependsOn = dependsOn
        });
        return this;
    }

    /// <summary>
    /// Adds a custom step.
    /// </summary>
    public WorkflowBuilder AddStep(WorkflowStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Builds the workflow definition.
    /// </summary>
    /// <returns>The constructed workflow definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required fields are missing.</exception>
    public WorkflowDefinition Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("Workflow name is required");
        }

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Workflow must have at least one step");
        }

        return new WorkflowDefinition
        {
            Id = _id,
            Name = _name,
            Version = _version,
            Description = _description,
            Tags = _tags.Count > 0 ? _tags.AsReadOnly() : null,
            Steps = _steps.AsReadOnly(),
            Triggers = _triggers.Count > 0 ? _triggers.AsReadOnly() : null,
            Variables = _variables.Count > 0 ? _variables.AsReadOnly() : null,
            Timeout = _timeout,
            MaxRetries = _maxRetries,
            IsEnabled = _isEnabled,
            ErrorHandling = _errorHandling
        };
    }
}

/// <summary>
/// Extension methods for workflow building.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Creates a step builder for building individual steps.
    /// </summary>
    public static StepBuilder Step(this WorkflowBuilder _, string id) => new(id);
}

/// <summary>
/// Fluent builder for creating workflow steps.
/// </summary>
public sealed class StepBuilder
{
    private readonly string _id;
    private string _name = string.Empty;
    private StepType _type;
    private StepConfig? _config;
    private List<string>? _dependsOn;
    private string? _condition;
    private TimeSpan? _timeout;
    private int _maxRetries;
    private TimeSpan? _retryDelay;
    private bool _continueOnError;
    private string? _outputVariable;

    /// <summary>
    /// Initializes a new step builder.
    /// </summary>
    public StepBuilder(string id)
    {
        _id = id;
    }

    /// <summary>
    /// Sets the step name.
    /// </summary>
    public StepBuilder Named(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Configures as a job step.
    /// </summary>
    public StepBuilder AsJob(string command, string pattern = "*", object? payload = null)
    {
        _type = StepType.Job;
        _config = new JobStepConfig
        {
            Command = command,
            Pattern = pattern,
            Payload = payload
        };
        return this;
    }

    /// <summary>
    /// Configures as a delay step.
    /// </summary>
    public StepBuilder AsDelay(TimeSpan duration)
    {
        _type = StepType.Delay;
        _config = new DelayStepConfig { Duration = duration };
        return this;
    }

    /// <summary>
    /// Sets step dependencies.
    /// </summary>
    public StepBuilder After(params string[] stepIds)
    {
        _dependsOn = [.. stepIds];
        return this;
    }

    /// <summary>
    /// Sets the step condition.
    /// </summary>
    public StepBuilder When(string condition)
    {
        _condition = condition;
        return this;
    }

    /// <summary>
    /// Sets the step timeout.
    /// </summary>
    public StepBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets retry configuration.
    /// </summary>
    public StepBuilder WithRetry(int maxRetries, TimeSpan? delay = null)
    {
        _maxRetries = maxRetries;
        _retryDelay = delay;
        return this;
    }

    /// <summary>
    /// Sets continue on error.
    /// </summary>
    public StepBuilder ContinueOnError(bool continueOnError = true)
    {
        _continueOnError = continueOnError;
        return this;
    }

    /// <summary>
    /// Sets the output variable name.
    /// </summary>
    public StepBuilder Output(string variableName)
    {
        _outputVariable = variableName;
        return this;
    }

    /// <summary>
    /// Builds the workflow step.
    /// </summary>
    public WorkflowStep Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            _name = _id;
        }

        if (_config == null)
        {
            throw new InvalidOperationException($"Step '{_id}' requires configuration. Call AsJob(), AsDelay(), etc.");
        }

        return new WorkflowStep
        {
            Id = _id,
            Name = _name,
            Type = _type,
            Config = _config,
            DependsOn = _dependsOn?.AsReadOnly(),
            Condition = _condition,
            Timeout = _timeout,
            MaxRetries = _maxRetries,
            RetryDelay = _retryDelay,
            ContinueOnError = _continueOnError,
            OutputVariable = _outputVariable
        };
    }
}
