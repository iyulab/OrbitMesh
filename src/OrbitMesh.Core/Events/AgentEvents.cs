using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Events;

/// <summary>
/// Event raised when an agent registers with the server.
/// </summary>
public sealed record AgentRegisteredEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public required string AgentName { get; init; }
    public string? Hostname { get; init; }
    public string? Version { get; init; }
    public string? Group { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<string>? Capabilities { get; init; }
}

/// <summary>
/// Event raised when an agent connects.
/// </summary>
public sealed record AgentConnectedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public required string ConnectionId { get; init; }
    public string? IpAddress { get; init; }
}

/// <summary>
/// Event raised when an agent disconnects.
/// </summary>
public sealed record AgentDisconnectedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public string? ConnectionId { get; init; }
    public string? Reason { get; init; }
    public bool WasGraceful { get; init; }
}

/// <summary>
/// Event raised when an agent reconnects.
/// </summary>
public sealed record AgentReconnectedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public required string NewConnectionId { get; init; }
    public string? PreviousConnectionId { get; init; }
    public TimeSpan DisconnectedDuration { get; init; }
}

/// <summary>
/// Event raised when agent status changes.
/// </summary>
public sealed record AgentStatusChangedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public AgentStatus PreviousStatus { get; init; }
    public AgentStatus NewStatus { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when an agent sends a heartbeat.
/// </summary>
public sealed record AgentHeartbeatEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public int? CpuUsage { get; init; }
    public int? MemoryUsage { get; init; }
    public int? ActiveJobs { get; init; }
}

/// <summary>
/// Event raised when an agent is paused.
/// </summary>
public sealed record AgentPausedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when an agent is resumed.
/// </summary>
public sealed record AgentResumedEvent : DomainEvent
{
    public required string AgentId { get; init; }
}

/// <summary>
/// Event raised when an agent encounters a fault.
/// </summary>
public sealed record AgentFaultedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public required string Error { get; init; }
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Event raised when an agent is removed.
/// </summary>
public sealed record AgentRemovedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event raised when agent capabilities are updated.
/// </summary>
public sealed record AgentCapabilitiesUpdatedEvent : DomainEvent
{
    public required string AgentId { get; init; }
    public IReadOnlyList<string>? AddedCapabilities { get; init; }
    public IReadOnlyList<string>? RemovedCapabilities { get; init; }
}
