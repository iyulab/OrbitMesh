using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// Defines methods that agents can invoke on the server hub.
/// </summary>
public interface IServerHub
{
    /// <summary>
    /// Registers an agent with the server.
    /// </summary>
    /// <param name="agentInfo">The agent information.</param>
    /// <returns>Registration confirmation with server-assigned data.</returns>
    Task<AgentRegistrationResult> RegisterAsync(AgentInfo agentInfo);

    /// <summary>
    /// Unregisters an agent from the server.
    /// </summary>
    /// <param name="agentId">The agent ID to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat to the server.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    Task HeartbeatAsync(string agentId);

    /// <summary>
    /// Acknowledges receipt of a job assignment.
    /// </summary>
    /// <param name="jobId">The job ID being acknowledged.</param>
    /// <param name="agentId">The agent ID acknowledging the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AcknowledgeJobAsync(
        string jobId,
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the result of a completed job.
    /// </summary>
    /// <param name="result">The job result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportResultAsync(JobResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports progress for a long-running job.
    /// </summary>
    /// <param name="progress">The progress information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the agent's current state to the server.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="reportedState">The reported state properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStateAsync(
        string agentId,
        IReadOnlyDictionary<string, string> reportedState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a stream item for a streaming job.
    /// </summary>
    /// <param name="item">The stream item to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStreamItemAsync(StreamItem item, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of agent registration.
/// </summary>
public sealed record AgentRegistrationResult
{
    /// <summary>
    /// Whether the registration was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if registration failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Server-assigned connection metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ServerMetadata { get; init; }

    /// <summary>
    /// Recommended heartbeat interval.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a successful registration result.
    /// </summary>
    public static AgentRegistrationResult Succeeded(TimeSpan? heartbeatInterval = null) =>
        new()
        {
            Success = true,
            HeartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30)
        };

    /// <summary>
    /// Creates a failed registration result.
    /// </summary>
    public static AgentRegistrationResult Failed(string error) =>
        new() { Success = false, Error = error };
}
