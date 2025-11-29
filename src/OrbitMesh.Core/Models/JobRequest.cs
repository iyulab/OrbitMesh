using MessagePack;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a job request to be executed by an agent.
/// </summary>
[MessagePackObject]
public sealed record JobRequest
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Idempotency key to prevent duplicate execution.
    /// </summary>
    [Key(1)]
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// The command/handler name to execute.
    /// </summary>
    [Key(2)]
    public required string Command { get; init; }

    /// <summary>
    /// Execution pattern for this job.
    /// </summary>
    [Key(3)]
    public ExecutionPattern Pattern { get; init; } = ExecutionPattern.RequestResponse;

    /// <summary>
    /// Serialized parameters for the command.
    /// </summary>
    [Key(4)]
    public byte[]? Parameters { get; init; }

    /// <summary>
    /// Target agent ID (null for capability-based routing).
    /// </summary>
    [Key(5)]
    public string? TargetAgentId { get; init; }

    /// <summary>
    /// Required capabilities for agent selection.
    /// </summary>
    [Key(6)]
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }

    /// <summary>
    /// Priority level (higher = more priority).
    /// </summary>
    [Key(7)]
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Timeout for the job execution.
    /// </summary>
    [Key(8)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Maximum retry attempts on failure.
    /// </summary>
    [Key(9)]
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// Current retry attempt (0 = first attempt).
    /// </summary>
    [Key(10)]
    public int RetryAttempt { get; init; } = 0;

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    [Key(11)]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    [Key(12)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Custom metadata for domain-specific information.
    /// </summary>
    [Key(13)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Creates a new job request with a generated ID and idempotency key.
    /// </summary>
    public static JobRequest Create(string command, ExecutionPattern pattern = ExecutionPattern.RequestResponse) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Command = command,
            Pattern = pattern
        };
}
