using MessagePack;

namespace OrbitMesh.Workflows.Models;

/// <summary>
/// Represents a running or completed instance of a workflow.
/// </summary>
[MessagePackObject]
public sealed record WorkflowInstance
{
    /// <summary>
    /// Unique identifier for this workflow instance.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// ID of the workflow definition being executed.
    /// </summary>
    [Key(1)]
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Version of the workflow definition.
    /// </summary>
    [Key(2)]
    public required string WorkflowVersion { get; init; }

    /// <summary>
    /// Current status of the workflow instance.
    /// </summary>
    [Key(3)]
    public WorkflowStatus Status { get; init; } = WorkflowStatus.Pending;

    /// <summary>
    /// ID of the trigger that started this instance.
    /// </summary>
    [Key(4)]
    public string? TriggerId { get; init; }

    /// <summary>
    /// Type of trigger that started this instance.
    /// </summary>
    [Key(5)]
    public string? TriggerType { get; init; }

    /// <summary>
    /// Input variables provided when the workflow was started.
    /// </summary>
    [Key(6)]
    public IReadOnlyDictionary<string, object?>? Input { get; init; }

    /// <summary>
    /// Current variable state during execution.
    /// </summary>
    [Key(7)]
    public Dictionary<string, object?>? Variables { get; init; }

    /// <summary>
    /// Output produced by the workflow.
    /// </summary>
    [Key(8)]
    public IReadOnlyDictionary<string, object?>? Output { get; init; }

    /// <summary>
    /// Status of each step in the workflow.
    /// </summary>
    [Key(9)]
    public Dictionary<string, StepInstance>? StepInstances { get; init; }

    /// <summary>
    /// Timestamp when the instance was created.
    /// </summary>
    [Key(10)]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    [Key(11)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the instance completed (success or failure).
    /// </summary>
    [Key(12)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Error message if the workflow failed.
    /// </summary>
    [Key(13)]
    public string? Error { get; init; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    [Key(14)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Number of retries attempted.
    /// </summary>
    [Key(15)]
    public int RetryCount { get; init; } = 0;

    /// <summary>
    /// ID of the parent workflow instance (for sub-workflows).
    /// </summary>
    [Key(16)]
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// Step ID in parent workflow that spawned this instance.
    /// </summary>
    [Key(17)]
    public string? ParentStepId { get; init; }

    /// <summary>
    /// Correlation ID for tracing related instances.
    /// </summary>
    [Key(18)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// User/service that initiated this instance.
    /// </summary>
    [Key(19)]
    public string? InitiatedBy { get; init; }

    /// <summary>
    /// Duration of the workflow execution.
    /// </summary>
    [IgnoreMember]
    public TimeSpan? Duration =>
        CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;

    /// <summary>
    /// Whether the workflow is in a terminal state.
    /// </summary>
    [IgnoreMember]
    public bool IsTerminal =>
        Status is WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.TimedOut;
}

/// <summary>
/// Represents the execution state of a single step.
/// </summary>
[MessagePackObject]
public sealed record StepInstance
{
    /// <summary>
    /// Step ID from the workflow definition.
    /// </summary>
    [Key(0)]
    public required string StepId { get; init; }

    /// <summary>
    /// Current status of this step.
    /// </summary>
    [Key(1)]
    public StepStatus Status { get; init; } = StepStatus.Pending;

    /// <summary>
    /// Timestamp when the step started.
    /// </summary>
    [Key(2)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the step completed.
    /// </summary>
    [Key(3)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Output produced by this step.
    /// </summary>
    [Key(4)]
    public object? Output { get; init; }

    /// <summary>
    /// Error message if the step failed.
    /// </summary>
    [Key(5)]
    public string? Error { get; init; }

    /// <summary>
    /// Number of retries for this step.
    /// </summary>
    [Key(6)]
    public int RetryCount { get; init; } = 0;

    /// <summary>
    /// Job ID if this is a job step.
    /// </summary>
    [Key(7)]
    public string? JobId { get; init; }

    /// <summary>
    /// Sub-workflow instance ID if this is a sub-workflow step.
    /// </summary>
    [Key(8)]
    public string? SubWorkflowInstanceId { get; init; }

    /// <summary>
    /// For parallel/foreach steps: status of each branch/iteration.
    /// </summary>
    [Key(9)]
    public IReadOnlyList<BranchInstance>? Branches { get; init; }

    /// <summary>
    /// Compensation status if compensation was triggered.
    /// </summary>
    [Key(10)]
    public CompensationInstance? Compensation { get; init; }
}

/// <summary>
/// Status of a workflow step.
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// Step is pending execution.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Step is waiting for dependencies.
    /// </summary>
    WaitingForDependencies = 1,

    /// <summary>
    /// Step is currently running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Step completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Step failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Step was skipped (condition not met).
    /// </summary>
    Skipped = 5,

    /// <summary>
    /// Step was cancelled.
    /// </summary>
    Cancelled = 6,

    /// <summary>
    /// Step timed out.
    /// </summary>
    TimedOut = 7,

    /// <summary>
    /// Step is waiting for external event.
    /// </summary>
    WaitingForEvent = 8,

    /// <summary>
    /// Step is waiting for approval.
    /// </summary>
    WaitingForApproval = 9,

    /// <summary>
    /// Step is being compensated.
    /// </summary>
    Compensating = 10,

    /// <summary>
    /// Compensation completed.
    /// </summary>
    Compensated = 11
}

/// <summary>
/// Status of a workflow instance.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// Workflow is pending execution.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Workflow is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Workflow completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Workflow failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Workflow was cancelled.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Workflow timed out.
    /// </summary>
    TimedOut = 5,

    /// <summary>
    /// Workflow is paused (waiting for event/approval).
    /// </summary>
    Paused = 6,

    /// <summary>
    /// Workflow is compensating due to failure.
    /// </summary>
    Compensating = 7
}

/// <summary>
/// Represents a branch or iteration instance in parallel/foreach steps.
/// </summary>
[MessagePackObject]
public sealed record BranchInstance
{
    /// <summary>
    /// Index or identifier of this branch.
    /// </summary>
    [Key(0)]
    public required int Index { get; init; }

    /// <summary>
    /// Status of this branch.
    /// </summary>
    [Key(1)]
    public StepStatus Status { get; init; } = StepStatus.Pending;

    /// <summary>
    /// Output produced by this branch.
    /// </summary>
    [Key(2)]
    public object? Output { get; init; }

    /// <summary>
    /// Error message if the branch failed.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }

    /// <summary>
    /// Step instances within this branch.
    /// </summary>
    [Key(4)]
    public Dictionary<string, StepInstance>? Steps { get; init; }
}

/// <summary>
/// Represents the compensation state for a step.
/// </summary>
[MessagePackObject]
public sealed record CompensationInstance
{
    /// <summary>
    /// Status of compensation.
    /// </summary>
    [Key(0)]
    public StepStatus Status { get; init; } = StepStatus.Pending;

    /// <summary>
    /// Timestamp when compensation started.
    /// </summary>
    [Key(1)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Timestamp when compensation completed.
    /// </summary>
    [Key(2)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Error during compensation.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }

    /// <summary>
    /// Number of compensation retries.
    /// </summary>
    [Key(4)]
    public int RetryCount { get; init; } = 0;
}
