namespace OrbitMesh.Core.Events;

/// <summary>
/// Base class for all domain events.
/// </summary>
public abstract record DomainEvent
{
    /// <summary>
    /// Unique event identifier.
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing related events.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Optional causation ID (ID of the event that caused this one).
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// User or system that triggered this event.
    /// </summary>
    public string? TriggeredBy { get; init; }
}
