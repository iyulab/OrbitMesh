using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a callback request from server to agent.
/// Used for bidirectional RPC where the server needs a response from the agent.
/// </summary>
[MessagePackObject]
public sealed record AgentCallbackRequest
{
    /// <summary>
    /// Unique identifier for this callback request.
    /// </summary>
    [Key(0)]
    public required string CallbackId { get; init; }

    /// <summary>
    /// The type of callback operation.
    /// </summary>
    [Key(1)]
    public required AgentCallbackType Type { get; init; }

    /// <summary>
    /// Optional job ID associated with this callback.
    /// </summary>
    [Key(2)]
    public string? JobId { get; init; }

    /// <summary>
    /// Serialized request payload.
    /// </summary>
    [Key(3)]
    public byte[]? Payload { get; init; }

    /// <summary>
    /// Timeout for the callback response.
    /// </summary>
    [Key(4)]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When the callback was issued.
    /// </summary>
    [Key(5)]
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new callback request.
    /// </summary>
    public static AgentCallbackRequest Create(
        AgentCallbackType type,
        string? jobId = null,
        byte[]? payload = null,
        TimeSpan? timeout = null) =>
        new()
        {
            CallbackId = Guid.NewGuid().ToString("N"),
            Type = type,
            JobId = jobId,
            Payload = payload,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
}

/// <summary>
/// Response from an agent callback.
/// </summary>
[MessagePackObject]
public sealed record AgentCallbackResponse
{
    /// <summary>
    /// The callback ID this response is for.
    /// </summary>
    [Key(0)]
    public required string CallbackId { get; init; }

    /// <summary>
    /// Whether the callback was successful.
    /// </summary>
    [Key(1)]
    public bool Success { get; init; }

    /// <summary>
    /// Serialized response payload.
    /// </summary>
    [Key(2)]
    public byte[]? Payload { get; init; }

    /// <summary>
    /// Error message if the callback failed.
    /// </summary>
    [Key(3)]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code if the callback failed.
    /// </summary>
    [Key(4)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// When the response was created.
    /// </summary>
    [Key(5)]
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static AgentCallbackResponse Succeeded(string callbackId, byte[]? payload = null) =>
        new()
        {
            CallbackId = callbackId,
            Success = true,
            Payload = payload
        };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static AgentCallbackResponse Failed(string callbackId, string errorMessage, string? errorCode = null) =>
        new()
        {
            CallbackId = callbackId,
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
}

/// <summary>
/// Types of agent callbacks supported by the system.
/// </summary>
public enum AgentCallbackType
{
    /// <summary>
    /// Request agent health/status information.
    /// </summary>
    HealthCheck = 0,

    /// <summary>
    /// Request agent capabilities list.
    /// </summary>
    GetCapabilities = 1,

    /// <summary>
    /// Request agent configuration.
    /// </summary>
    GetConfiguration = 2,

    /// <summary>
    /// Request agent to perform a custom operation.
    /// </summary>
    CustomOperation = 3,

    /// <summary>
    /// Request confirmation for a pending operation.
    /// </summary>
    Confirmation = 4,

    /// <summary>
    /// Request agent resource usage statistics.
    /// </summary>
    GetResourceUsage = 5,

    /// <summary>
    /// Request agent to validate a job before execution.
    /// </summary>
    ValidateJob = 6,

    /// <summary>
    /// Request agent-specific data or state.
    /// </summary>
    GetAgentData = 7
}

/// <summary>
/// Resource usage information from an agent.
/// </summary>
[MessagePackObject]
public sealed record AgentResourceUsage
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    [Key(0)]
    public double CpuPercentage { get; init; }

    /// <summary>
    /// Memory usage in bytes.
    /// </summary>
    [Key(1)]
    public long MemoryBytes { get; init; }

    /// <summary>
    /// Number of active jobs.
    /// </summary>
    [Key(2)]
    public int ActiveJobs { get; init; }

    /// <summary>
    /// Queue depth (pending jobs).
    /// </summary>
    [Key(3)]
    public int QueueDepth { get; init; }

    /// <summary>
    /// Available worker threads.
    /// </summary>
    [Key(4)]
    public int AvailableWorkers { get; init; }

    /// <summary>
    /// When this measurement was taken.
    /// </summary>
    [Key(5)]
    public DateTimeOffset MeasuredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional resource metrics.
    /// </summary>
    [Key(6)]
    public IReadOnlyDictionary<string, double>? AdditionalMetrics { get; init; }
}

/// <summary>
/// Health check response from an agent.
/// </summary>
[MessagePackObject]
public sealed record AgentHealthResponse
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    [Key(0)]
    public AgentHealthStatus Status { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    [Key(1)]
    public string? Message { get; init; }

    /// <summary>
    /// Individual health check results.
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<string, bool>? Checks { get; init; }

    /// <summary>
    /// When the health check was performed.
    /// </summary>
    [Key(3)]
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Agent uptime.
    /// </summary>
    [Key(4)]
    public TimeSpan? Uptime { get; init; }
}

/// <summary>
/// Health status values for agents.
/// </summary>
public enum AgentHealthStatus
{
    /// <summary>
    /// Agent is healthy and operational.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Agent is operational but experiencing issues.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Agent is not operational.
    /// </summary>
    Unhealthy = 2
}
