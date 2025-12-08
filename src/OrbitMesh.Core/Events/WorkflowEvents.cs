using OrbitMesh.Core.Storage;

namespace OrbitMesh.Core.Events;

/// <summary>
/// Event raised when a workflow definition is created.
/// </summary>
public sealed record WorkflowDefinitionCreatedEvent : DomainEvent
{
    public required string WorkflowId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Event raised when a workflow definition is updated.
/// </summary>
public sealed record WorkflowDefinitionUpdatedEvent : DomainEvent
{
    public required string WorkflowId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public IReadOnlyList<string>? ChangedFields { get; init; }
}

/// <summary>
/// Event raised when a workflow definition is activated.
/// </summary>
public sealed record WorkflowDefinitionActivatedEvent : DomainEvent
{
    public required string WorkflowId { get; init; }
}

/// <summary>
/// Event raised when a workflow definition is deactivated.
/// </summary>
public sealed record WorkflowDefinitionDeactivatedEvent : DomainEvent
{
    public required string WorkflowId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when a workflow definition is deleted.
/// </summary>
public sealed record WorkflowDefinitionDeletedEvent : DomainEvent
{
    public required string WorkflowId { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Event raised when a workflow instance is created.
/// </summary>
public sealed record WorkflowInstanceCreatedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string WorkflowName { get; init; }
    public string? TriggerType { get; init; }
}

/// <summary>
/// Event raised when a workflow instance starts execution.
/// </summary>
public sealed record WorkflowInstanceStartedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
}

/// <summary>
/// Event raised when a workflow step starts.
/// </summary>
public sealed record WorkflowStepStartedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string StepName { get; init; }
    public string? AgentId { get; init; }
    public int StepIndex { get; init; }
}

/// <summary>
/// Event raised when a workflow step completes.
/// </summary>
public sealed record WorkflowStepCompletedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string StepName { get; init; }
    public string? AgentId { get; init; }
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Event raised when a workflow step fails.
/// </summary>
public sealed record WorkflowStepFailedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string StepName { get; init; }
    public string? AgentId { get; init; }
    public required string Error { get; init; }
    public bool WillRetry { get; init; }
}

/// <summary>
/// Event raised when a workflow instance completes.
/// </summary>
public sealed record WorkflowInstanceCompletedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public WorkflowInstanceStatus Status { get; init; }
    public TimeSpan Duration { get; init; }
    public int TotalSteps { get; init; }
    public int CompletedSteps { get; init; }
}

/// <summary>
/// Event raised when a workflow instance fails.
/// </summary>
public sealed record WorkflowInstanceFailedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string Error { get; init; }
    public string? FailedStep { get; init; }
}

/// <summary>
/// Event raised when a workflow instance is cancelled.
/// </summary>
public sealed record WorkflowInstanceCancelledEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public string? Reason { get; init; }
    public string? CurrentStep { get; init; }
}

/// <summary>
/// Event raised when workflow compensation starts.
/// </summary>
public sealed record WorkflowCompensationStartedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string FailedStep { get; init; }
    public int StepsToCompensate { get; init; }
}

/// <summary>
/// Event raised when workflow compensation completes.
/// </summary>
public sealed record WorkflowCompensationCompletedEvent : DomainEvent
{
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public bool Success { get; init; }
    public int CompensatedSteps { get; init; }
}
