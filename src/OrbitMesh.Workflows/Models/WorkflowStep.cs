using MessagePack;

namespace OrbitMesh.Workflows.Models;

/// <summary>
/// Represents a single step in a workflow.
/// </summary>
[MessagePackObject]
public sealed record WorkflowStep
{
    /// <summary>
    /// Unique identifier for this step within the workflow.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the step.
    /// </summary>
    [Key(1)]
    public required string Name { get; init; }

    /// <summary>
    /// The type of step to execute.
    /// </summary>
    [Key(2)]
    public required StepType Type { get; init; }

    /// <summary>
    /// Configuration specific to the step type.
    /// </summary>
    [Key(3)]
    public required StepConfig Config { get; init; }

    /// <summary>
    /// IDs of steps that must complete before this step can run.
    /// Empty or null means this step has no dependencies.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string>? DependsOn { get; init; }

    /// <summary>
    /// Condition that must be true for this step to execute.
    /// Supports expression syntax for dynamic evaluation.
    /// </summary>
    [Key(5)]
    public string? Condition { get; init; }

    /// <summary>
    /// Maximum execution time for this step.
    /// </summary>
    [Key(6)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Maximum number of retries for this step on failure.
    /// </summary>
    [Key(7)]
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// Delay between retries.
    /// </summary>
    [Key(8)]
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>
    /// Whether to continue workflow execution if this step fails.
    /// </summary>
    [Key(9)]
    public bool ContinueOnError { get; init; } = false;

    /// <summary>
    /// Optional compensation step configuration for saga pattern.
    /// </summary>
    [Key(10)]
    public CompensationConfig? Compensation { get; init; }

    /// <summary>
    /// Output variable name to store the result of this step.
    /// </summary>
    [Key(11)]
    public string? OutputVariable { get; init; }
}

/// <summary>
/// Types of workflow steps supported.
/// </summary>
public enum StepType
{
    /// <summary>
    /// Execute a job on an agent.
    /// </summary>
    Job = 0,

    /// <summary>
    /// Execute multiple steps in parallel.
    /// </summary>
    Parallel = 1,

    /// <summary>
    /// Conditional branching based on expression.
    /// </summary>
    Conditional = 2,

    /// <summary>
    /// Wait for a specified duration.
    /// </summary>
    Delay = 3,

    /// <summary>
    /// Wait for an external event/signal.
    /// </summary>
    WaitForEvent = 4,

    /// <summary>
    /// Execute another workflow as a sub-workflow.
    /// </summary>
    SubWorkflow = 5,

    /// <summary>
    /// Loop/iterate over a collection.
    /// </summary>
    ForEach = 6,

    /// <summary>
    /// Transform/map data between steps.
    /// </summary>
    Transform = 7,

    /// <summary>
    /// Send a notification (email, webhook, etc.).
    /// </summary>
    Notify = 8,

    /// <summary>
    /// Human approval gate - pause until approved.
    /// </summary>
    Approval = 9
}

/// <summary>
/// Compensation configuration for saga pattern support.
/// </summary>
[MessagePackObject]
public sealed record CompensationConfig
{
    /// <summary>
    /// The step configuration to execute as compensation.
    /// </summary>
    [Key(0)]
    public required StepConfig Config { get; init; }

    /// <summary>
    /// Maximum execution time for compensation.
    /// </summary>
    [Key(1)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Maximum retries for compensation action.
    /// </summary>
    [Key(2)]
    public int MaxRetries { get; init; } = 3;
}
