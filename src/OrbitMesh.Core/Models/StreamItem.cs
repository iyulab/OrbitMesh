using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a single item in a streaming data flow.
/// Used for real-time data delivery from agents to clients.
/// </summary>
[MessagePackObject]
public sealed record StreamItem
{
    /// <summary>
    /// The ID of the job producing this stream.
    /// </summary>
    [Key(0)]
    public required string JobId { get; init; }

    /// <summary>
    /// Sequence number for ordering stream items.
    /// </summary>
    [Key(1)]
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// The serialized data payload.
    /// </summary>
    [Key(2)]
    public required byte[] Data { get; init; }

    /// <summary>
    /// Optional content type hint (e.g., "text/plain", "application/json").
    /// </summary>
    [Key(3)]
    public string? ContentType { get; init; }

    /// <summary>
    /// Timestamp when this item was created.
    /// </summary>
    [Key(4)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Indicates if this is the final item in the stream.
    /// </summary>
    [Key(5)]
    public bool IsEndOfStream { get; init; }

    /// <summary>
    /// Optional metadata for this stream item.
    /// </summary>
    [Key(6)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Creates a text stream item.
    /// </summary>
    public static StreamItem FromText(string jobId, long sequenceNumber, string text, bool isEnd = false) =>
        new()
        {
            JobId = jobId,
            SequenceNumber = sequenceNumber,
            Data = System.Text.Encoding.UTF8.GetBytes(text),
            ContentType = "text/plain",
            IsEndOfStream = isEnd
        };

    /// <summary>
    /// Creates a JSON stream item.
    /// </summary>
    public static StreamItem FromJson<T>(string jobId, long sequenceNumber, T value, bool isEnd = false) =>
        new()
        {
            JobId = jobId,
            SequenceNumber = sequenceNumber,
            Data = MessagePackSerializer.Serialize(value),
            ContentType = "application/x-msgpack",
            IsEndOfStream = isEnd
        };

    /// <summary>
    /// Creates an end-of-stream marker.
    /// </summary>
    public static StreamItem EndOfStream(string jobId, long sequenceNumber) =>
        new()
        {
            JobId = jobId,
            SequenceNumber = sequenceNumber,
            Data = [],
            IsEndOfStream = true
        };

    /// <summary>
    /// Gets the data as a UTF-8 string if content type is text.
    /// </summary>
    [IgnoreMember]
    public string? TextData =>
        ContentType?.StartsWith("text/", StringComparison.Ordinal) == true
            ? System.Text.Encoding.UTF8.GetString(Data)
            : null;
}

/// <summary>
/// Stream state tracking for a single job.
/// </summary>
[MessagePackObject]
public sealed record StreamState
{
    /// <summary>
    /// The job ID.
    /// </summary>
    [Key(0)]
    public required string JobId { get; init; }

    /// <summary>
    /// Current sequence number (last received).
    /// </summary>
    [Key(1)]
    public long CurrentSequence { get; init; }

    /// <summary>
    /// Whether the stream has completed.
    /// </summary>
    [Key(2)]
    public bool IsComplete { get; init; }

    /// <summary>
    /// When the stream started.
    /// </summary>
    [Key(3)]
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the stream completed (if complete).
    /// </summary>
    [Key(4)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Total number of items streamed.
    /// </summary>
    [Key(5)]
    public long TotalItems { get; init; }

    /// <summary>
    /// Total bytes streamed.
    /// </summary>
    [Key(6)]
    public long TotalBytes { get; init; }
}
