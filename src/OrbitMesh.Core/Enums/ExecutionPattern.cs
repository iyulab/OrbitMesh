namespace OrbitMesh.Core.Enums;

/// <summary>
/// Defines the execution pattern for a job request.
/// </summary>
public enum ExecutionPattern
{
    /// <summary>
    /// Fire and forget - no response expected.
    /// Use for notifications, logging, and non-critical operations.
    /// </summary>
    FireAndForget = 0,

    /// <summary>
    /// Request-response - synchronous result expected.
    /// Use for quick queries, status checks, and immediate operations.
    /// </summary>
    RequestResponse = 1,

    /// <summary>
    /// Streaming - continuous data flow.
    /// Use for LLM inference, log tailing, and real-time data.
    /// </summary>
    Streaming = 2,

    /// <summary>
    /// Long-running job - progress reporting with final result.
    /// Use for ML training, data processing, and batch operations.
    /// </summary>
    LongRunning = 3
}
