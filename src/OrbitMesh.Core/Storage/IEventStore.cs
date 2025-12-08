namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for event sourcing.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends events to a stream.
    /// </summary>
    /// <param name="streamId">The stream identifier (e.g., "job-123", "agent-456").</param>
    /// <param name="events">The events to append.</param>
    /// <param name="expectedVersion">Expected stream version for optimistic concurrency. -1 for new stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new stream version after appending.</returns>
    Task<long> AppendAsync(
        string streamId,
        IEnumerable<EventData> events,
        long expectedVersion = -1,
        CancellationToken ct = default);

    /// <summary>
    /// Reads all events from a stream.
    /// </summary>
    Task<IReadOnlyList<EventData>> ReadStreamAsync(
        string streamId,
        CancellationToken ct = default);

    /// <summary>
    /// Reads events from a stream starting from a specific version.
    /// </summary>
    Task<IReadOnlyList<EventData>> ReadStreamAsync(
        string streamId,
        long fromVersion,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current version of a stream.
    /// </summary>
    Task<long> GetStreamVersionAsync(string streamId, CancellationToken ct = default);

    /// <summary>
    /// Reads all events across all streams (for projections/rebuilding).
    /// </summary>
    Task<IReadOnlyList<EventData>> ReadAllAsync(
        long fromPosition = 0,
        int maxCount = 1000,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a stream.
    /// </summary>
    Task DeleteStreamAsync(string streamId, CancellationToken ct = default);
}

/// <summary>
/// Represents a stored event.
/// </summary>
public sealed record EventData
{
    /// <summary>
    /// Unique event ID.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// The stream this event belongs to.
    /// </summary>
    public required string StreamId { get; init; }

    /// <summary>
    /// Event type name (e.g., "JobCreated", "AgentConnected").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Serialized event payload (JSON or MessagePack).
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Optional metadata.
    /// </summary>
    public byte[]? Metadata { get; init; }

    /// <summary>
    /// Version within the stream.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Global position across all streams.
    /// </summary>
    public long Position { get; init; }

    /// <summary>
    /// When the event was stored.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new event data for appending.
    /// </summary>
    public static EventData Create<T>(string streamId, string eventType, T payload)
        where T : class
    {
        return new EventData
        {
            EventId = Guid.NewGuid().ToString("N"),
            StreamId = streamId,
            EventType = eventType,
            Data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Deserializes the event data to the specified type.
    /// </summary>
    public T? Deserialize<T>() where T : class
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(Data);
    }
}
