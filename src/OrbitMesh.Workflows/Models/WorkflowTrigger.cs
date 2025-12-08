using MessagePack;

namespace OrbitMesh.Workflows.Models;

/// <summary>
/// Base class for workflow triggers.
/// Triggers define when and how workflows are started.
/// </summary>
[MessagePackObject]
[Union(0, typeof(ScheduleTrigger))]
[Union(1, typeof(EventTrigger))]
[Union(2, typeof(ManualTrigger))]
[Union(3, typeof(WebhookTrigger))]
[Union(4, typeof(JobCompletionTrigger))]
public abstract record WorkflowTrigger
{
    /// <summary>
    /// Unique identifier for this trigger.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the trigger.
    /// </summary>
    [Key(1)]
    public string? Name { get; init; }

    /// <summary>
    /// Whether this trigger is enabled.
    /// </summary>
    [Key(2)]
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// Trigger based on a schedule (cron expression or interval).
/// </summary>
[MessagePackObject]
public sealed record ScheduleTrigger : WorkflowTrigger
{
    /// <summary>
    /// Cron expression for scheduling.
    /// </summary>
    [Key(3)]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Fixed interval for scheduling (alternative to cron).
    /// </summary>
    [Key(4)]
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Timezone for cron scheduling.
    /// </summary>
    [Key(5)]
    public string Timezone { get; init; } = "UTC";

    /// <summary>
    /// Start date for the schedule.
    /// </summary>
    [Key(6)]
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>
    /// End date for the schedule.
    /// </summary>
    [Key(7)]
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>
    /// Whether to catch up missed executions.
    /// </summary>
    [Key(8)]
    public bool CatchUp { get; init; } = false;

    /// <summary>
    /// Maximum number of concurrent executions.
    /// </summary>
    [Key(9)]
    public int MaxConcurrentExecutions { get; init; } = 1;
}

/// <summary>
/// Trigger based on internal events.
/// </summary>
[MessagePackObject]
public sealed record EventTrigger : WorkflowTrigger
{
    /// <summary>
    /// Event type/name to listen for.
    /// </summary>
    [Key(3)]
    public required string EventType { get; init; }

    /// <summary>
    /// Optional filter expression for the event data.
    /// </summary>
    [Key(4)]
    public string? Filter { get; init; }

    /// <summary>
    /// Mapping of event data to workflow input variables.
    /// </summary>
    [Key(5)]
    public IReadOnlyDictionary<string, string>? InputMapping { get; init; }
}

/// <summary>
/// Manual trigger for on-demand execution.
/// </summary>
[MessagePackObject]
public sealed record ManualTrigger : WorkflowTrigger
{
    /// <summary>
    /// Input schema definition for manual trigger.
    /// </summary>
    [Key(3)]
    public IReadOnlyDictionary<string, InputParameterDefinition>? InputSchema { get; init; }

    /// <summary>
    /// Required roles/permissions to trigger manually.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string>? RequiredRoles { get; init; }
}

/// <summary>
/// Trigger based on incoming webhook.
/// </summary>
[MessagePackObject]
public sealed record WebhookTrigger : WorkflowTrigger
{
    /// <summary>
    /// Webhook path (appended to base URL).
    /// </summary>
    [Key(3)]
    public required string Path { get; init; }

    /// <summary>
    /// HTTP methods that trigger the workflow.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string> Methods { get; init; } = ["POST"];

    /// <summary>
    /// Optional secret for webhook validation.
    /// </summary>
    [Key(5)]
    public string? Secret { get; init; }

    /// <summary>
    /// Mapping of request data to workflow input variables.
    /// </summary>
    [Key(6)]
    public IReadOnlyDictionary<string, string>? InputMapping { get; init; }
}

/// <summary>
/// Trigger based on job completion.
/// </summary>
[MessagePackObject]
public sealed record JobCompletionTrigger : WorkflowTrigger
{
    /// <summary>
    /// Job command pattern to match.
    /// </summary>
    [Key(3)]
    public required string CommandPattern { get; init; }

    /// <summary>
    /// Job statuses that trigger the workflow.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string> Statuses { get; init; } = ["Completed", "Failed"];

    /// <summary>
    /// Optional filter expression for job data.
    /// </summary>
    [Key(5)]
    public string? Filter { get; init; }
}

/// <summary>
/// Definition for an input parameter.
/// </summary>
[MessagePackObject]
public sealed record InputParameterDefinition
{
    /// <summary>
    /// Data type of the parameter.
    /// </summary>
    [Key(0)]
    public required InputParameterType Type { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    [Key(1)]
    public string? Description { get; init; }

    /// <summary>
    /// Whether this parameter is required.
    /// </summary>
    [Key(2)]
    public bool Required { get; init; } = false;

    /// <summary>
    /// Default value if not provided.
    /// </summary>
    [Key(3)]
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Allowed values (for enum-like parameters).
    /// </summary>
    [Key(4)]
    public IReadOnlyList<object>? AllowedValues { get; init; }
}

/// <summary>
/// Types of input parameters.
/// </summary>
public enum InputParameterType
{
    /// <summary>
    /// String value.
    /// </summary>
    StringValue = 0,

    /// <summary>
    /// Integer value.
    /// </summary>
    IntegerValue = 1,

    /// <summary>
    /// Floating-point number.
    /// </summary>
    NumberValue = 2,

    /// <summary>
    /// Boolean value.
    /// </summary>
    BooleanValue = 3,

    /// <summary>
    /// Array of values.
    /// </summary>
    ArrayValue = 4,

    /// <summary>
    /// Complex object.
    /// </summary>
    ObjectValue = 5,

    /// <summary>
    /// Date/time value.
    /// </summary>
    DateTimeValue = 6
}
