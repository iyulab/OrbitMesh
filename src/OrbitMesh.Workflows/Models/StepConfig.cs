using MessagePack;

namespace OrbitMesh.Workflows.Models;

/// <summary>
/// Base configuration for workflow steps.
/// Uses union types for MessagePack polymorphism.
/// </summary>
[MessagePackObject]
[Union(0, typeof(JobStepConfig))]
[Union(1, typeof(ParallelStepConfig))]
[Union(2, typeof(ConditionalStepConfig))]
[Union(3, typeof(DelayStepConfig))]
[Union(4, typeof(WaitForEventStepConfig))]
[Union(5, typeof(SubWorkflowStepConfig))]
[Union(6, typeof(ForEachStepConfig))]
[Union(7, typeof(TransformStepConfig))]
[Union(8, typeof(NotifyStepConfig))]
[Union(9, typeof(ApprovalStepConfig))]
public abstract record StepConfig;

/// <summary>
/// Configuration for executing a job on an agent.
/// </summary>
[MessagePackObject]
public sealed record JobStepConfig : StepConfig
{
    /// <summary>
    /// The command/action to execute.
    /// </summary>
    [Key(0)]
    public required string Command { get; init; }

    /// <summary>
    /// Agent selection pattern (supports wildcards).
    /// </summary>
    [Key(1)]
    public string Pattern { get; init; } = "*";

    /// <summary>
    /// Payload to send with the job.
    /// Supports expression syntax for dynamic values.
    /// </summary>
    [Key(2)]
    public object? Payload { get; init; }

    /// <summary>
    /// Priority of the job (higher = more priority).
    /// </summary>
    [Key(3)]
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Tags required on the target agent.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string>? RequiredTags { get; init; }
}

/// <summary>
/// Configuration for parallel step execution.
/// </summary>
[MessagePackObject]
public sealed record ParallelStepConfig : StepConfig
{
    /// <summary>
    /// Steps to execute in parallel.
    /// </summary>
    [Key(0)]
    public required IReadOnlyList<WorkflowStep> Branches { get; init; }

    /// <summary>
    /// Maximum concurrent branches to execute.
    /// Null means unlimited parallelism.
    /// </summary>
    [Key(1)]
    public int? MaxConcurrency { get; init; }

    /// <summary>
    /// Whether to fail fast when any branch fails.
    /// </summary>
    [Key(2)]
    public bool FailFast { get; init; } = true;
}

/// <summary>
/// Configuration for conditional branching.
/// </summary>
[MessagePackObject]
public sealed record ConditionalStepConfig : StepConfig
{
    /// <summary>
    /// Condition expression to evaluate.
    /// </summary>
    [Key(0)]
    public required string Expression { get; init; }

    /// <summary>
    /// Steps to execute if condition is true.
    /// </summary>
    [Key(1)]
    public required IReadOnlyList<WorkflowStep> ThenBranch { get; init; }

    /// <summary>
    /// Steps to execute if condition is false.
    /// </summary>
    [Key(2)]
    public IReadOnlyList<WorkflowStep>? ElseBranch { get; init; }
}

/// <summary>
/// Configuration for delay step.
/// </summary>
[MessagePackObject]
public sealed record DelayStepConfig : StepConfig
{
    /// <summary>
    /// Duration to wait.
    /// </summary>
    [Key(0)]
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Configuration for waiting on an external event.
/// </summary>
[MessagePackObject]
public sealed record WaitForEventStepConfig : StepConfig
{
    /// <summary>
    /// Event type/name to wait for.
    /// </summary>
    [Key(0)]
    public required string EventType { get; init; }

    /// <summary>
    /// Optional correlation key for matching events.
    /// </summary>
    [Key(1)]
    public string? CorrelationKey { get; init; }

    /// <summary>
    /// Timeout for waiting. Null means wait indefinitely.
    /// </summary>
    [Key(2)]
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Configuration for executing a sub-workflow.
/// </summary>
[MessagePackObject]
public sealed record SubWorkflowStepConfig : StepConfig
{
    /// <summary>
    /// ID of the workflow to execute.
    /// </summary>
    [Key(0)]
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Optional specific version to use.
    /// </summary>
    [Key(1)]
    public string? Version { get; init; }

    /// <summary>
    /// Input variables to pass to the sub-workflow.
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<string, object?>? Input { get; init; }

    /// <summary>
    /// Whether to wait for sub-workflow completion.
    /// </summary>
    [Key(3)]
    public bool WaitForCompletion { get; init; } = true;
}

/// <summary>
/// Configuration for iterating over a collection.
/// </summary>
[MessagePackObject]
public sealed record ForEachStepConfig : StepConfig
{
    /// <summary>
    /// Expression that evaluates to the collection to iterate.
    /// </summary>
    [Key(0)]
    public required string Collection { get; init; }

    /// <summary>
    /// Variable name for the current item.
    /// </summary>
    [Key(1)]
    public string ItemVariable { get; init; } = "item";

    /// <summary>
    /// Variable name for the current index.
    /// </summary>
    [Key(2)]
    public string IndexVariable { get; init; } = "index";

    /// <summary>
    /// Steps to execute for each item.
    /// </summary>
    [Key(3)]
    public required IReadOnlyList<WorkflowStep> Steps { get; init; }

    /// <summary>
    /// Maximum concurrent iterations. Null means sequential.
    /// </summary>
    [Key(4)]
    public int? MaxConcurrency { get; init; }
}

/// <summary>
/// Configuration for data transformation.
/// </summary>
[MessagePackObject]
public sealed record TransformStepConfig : StepConfig
{
    /// <summary>
    /// Expression or JMESPath for transformation.
    /// </summary>
    [Key(0)]
    public required string Expression { get; init; }

    /// <summary>
    /// Source variable/path to transform.
    /// </summary>
    [Key(1)]
    public string? Source { get; init; }
}

/// <summary>
/// Configuration for sending notifications.
/// </summary>
[MessagePackObject]
public sealed record NotifyStepConfig : StepConfig
{
    /// <summary>
    /// Notification channel type.
    /// </summary>
    [Key(0)]
    public required NotifyChannel Channel { get; init; }

    /// <summary>
    /// Target (email address, webhook URL, etc.).
    /// </summary>
    [Key(1)]
    public required string Target { get; init; }

    /// <summary>
    /// Message template with expression support.
    /// </summary>
    [Key(2)]
    public required string Message { get; init; }

    /// <summary>
    /// Optional subject/title.
    /// </summary>
    [Key(3)]
    public string? Subject { get; init; }
}

/// <summary>
/// Notification channel types.
/// </summary>
public enum NotifyChannel
{
    /// <summary>
    /// HTTP webhook notification.
    /// </summary>
    Webhook = 0,

    /// <summary>
    /// Email notification.
    /// </summary>
    Email = 1,

    /// <summary>
    /// Slack notification.
    /// </summary>
    Slack = 2,

    /// <summary>
    /// Microsoft Teams notification.
    /// </summary>
    Teams = 3
}

/// <summary>
/// Configuration for human approval gate.
/// </summary>
[MessagePackObject]
public sealed record ApprovalStepConfig : StepConfig
{
    /// <summary>
    /// Users/groups who can approve.
    /// </summary>
    [Key(0)]
    public required IReadOnlyList<string> Approvers { get; init; }

    /// <summary>
    /// Number of approvals required.
    /// </summary>
    [Key(1)]
    public int RequiredApprovals { get; init; } = 1;

    /// <summary>
    /// Message to display to approvers.
    /// </summary>
    [Key(2)]
    public string? Message { get; init; }

    /// <summary>
    /// Timeout for approval. Null means wait indefinitely.
    /// </summary>
    [Key(3)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Action on timeout (approve, reject, or fail).
    /// </summary>
    [Key(4)]
    public ApprovalTimeoutAction TimeoutAction { get; init; } = ApprovalTimeoutAction.Fail;
}

/// <summary>
/// Action to take when approval times out.
/// </summary>
public enum ApprovalTimeoutAction
{
    /// <summary>
    /// Fail the workflow on timeout.
    /// </summary>
    Fail = 0,

    /// <summary>
    /// Auto-approve on timeout.
    /// </summary>
    Approve = 1,

    /// <summary>
    /// Auto-reject on timeout.
    /// </summary>
    Reject = 2
}
