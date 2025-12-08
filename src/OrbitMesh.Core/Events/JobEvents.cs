using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Events;

/// <summary>
/// Event raised when a job is created.
/// </summary>
public sealed record JobCreatedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string Command { get; init; }
    public int Priority { get; init; }
    public ExecutionPattern ExecutionPattern { get; init; }
    public string? TargetAgentId { get; init; }
    public string? TargetGroup { get; init; }
}

/// <summary>
/// Event raised when a job is assigned to an agent.
/// </summary>
public sealed record JobAssignedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
}

/// <summary>
/// Event raised when an agent accepts a job.
/// </summary>
public sealed record JobAcceptedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
}

/// <summary>
/// Event raised when an agent rejects a job.
/// </summary>
public sealed record JobRejectedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when a job starts executing.
/// </summary>
public sealed record JobStartedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
}

/// <summary>
/// Event raised when job progress is reported.
/// </summary>
public sealed record JobProgressEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
    public int ProgressPercentage { get; init; }
    public string? Message { get; init; }
    public string? CurrentStep { get; init; }
}

/// <summary>
/// Event raised when a job completes successfully.
/// </summary>
public sealed record JobCompletedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public required string AgentId { get; init; }
    public bool Success { get; init; } = true;
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Event raised when a job fails.
/// </summary>
public sealed record JobFailedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public string? AgentId { get; init; }
    public required string Error { get; init; }
    public string? ErrorCode { get; init; }
    public int RetryCount { get; init; }
    public bool WillRetry { get; init; }
}

/// <summary>
/// Event raised when a job is cancelled.
/// </summary>
public sealed record JobCancelledEvent : DomainEvent
{
    public required string JobId { get; init; }
    public string? AgentId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when a job times out.
/// </summary>
public sealed record JobTimedOutEvent : DomainEvent
{
    public required string JobId { get; init; }
    public string? AgentId { get; init; }
    public TimeSpan Timeout { get; init; }
    public int TimeoutCount { get; init; }
    public bool WillRetry { get; init; }
}

/// <summary>
/// Event raised when a job is retried.
/// </summary>
public sealed record JobRetriedEvent : DomainEvent
{
    public required string JobId { get; init; }
    public int RetryCount { get; init; }
    public string? PreviousAgentId { get; init; }
    public string? NewAgentId { get; init; }
}
