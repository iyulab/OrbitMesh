using MessagePack;

namespace OrbitMesh.Workflows.Models;

/// <summary>
/// Represents a complete workflow definition with steps, triggers, and metadata.
/// </summary>
[MessagePackObject]
public sealed record WorkflowDefinition
{
    /// <summary>
    /// Unique identifier for this workflow definition.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the workflow.
    /// </summary>
    [Key(1)]
    public required string Name { get; init; }

    /// <summary>
    /// Version of the workflow definition (semver format recommended).
    /// </summary>
    [Key(2)]
    public required string Version { get; init; }

    /// <summary>
    /// Optional description of what this workflow does.
    /// </summary>
    [Key(3)]
    public string? Description { get; init; }

    /// <summary>
    /// The steps that make up this workflow.
    /// </summary>
    [Key(4)]
    public required IReadOnlyList<WorkflowStep> Steps { get; init; }

    /// <summary>
    /// Triggers that can start this workflow.
    /// </summary>
    [Key(5)]
    public IReadOnlyList<WorkflowTrigger>? Triggers { get; init; }

    /// <summary>
    /// Global variables available to all steps.
    /// </summary>
    [Key(6)]
    public IReadOnlyDictionary<string, object?>? Variables { get; init; }

    /// <summary>
    /// Maximum execution time for the entire workflow.
    /// </summary>
    [Key(7)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Maximum number of retries for the entire workflow on failure.
    /// </summary>
    [Key(8)]
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    [Key(9)]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Whether this workflow is enabled for execution.
    /// </summary>
    [Key(10)]
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Timestamp when this definition was created.
    /// </summary>
    [Key(11)]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this definition was last modified.
    /// </summary>
    [Key(12)]
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>
    /// Error handling configuration for the workflow.
    /// </summary>
    [Key(13)]
    public WorkflowErrorHandling? ErrorHandling { get; init; }
}

/// <summary>
/// Error handling configuration for workflows.
/// </summary>
[MessagePackObject]
public sealed record WorkflowErrorHandling
{
    /// <summary>
    /// Strategy when a step fails.
    /// </summary>
    [Key(0)]
    public ErrorStrategy Strategy { get; init; } = ErrorStrategy.StopOnFirstError;

    /// <summary>
    /// Optional compensation workflow to run on failure.
    /// </summary>
    [Key(1)]
    public string? CompensationWorkflowId { get; init; }

    /// <summary>
    /// Whether to continue with remaining steps on error.
    /// </summary>
    [Key(2)]
    public bool ContinueOnError { get; init; } = false;
}

/// <summary>
/// Strategy for handling errors in workflows.
/// </summary>
public enum ErrorStrategy
{
    /// <summary>
    /// Stop workflow execution on the first error.
    /// </summary>
    StopOnFirstError = 0,

    /// <summary>
    /// Continue execution and aggregate errors at the end.
    /// </summary>
    ContinueAndAggregate = 1,

    /// <summary>
    /// Run compensation steps on error (saga pattern).
    /// </summary>
    Compensate = 2
}
