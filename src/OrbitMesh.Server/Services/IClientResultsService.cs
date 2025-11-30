using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Service for invoking methods on agents and receiving results (Client Results pattern).
/// Provides bidirectional RPC capabilities between server and agents.
/// </summary>
public interface IClientResultsService
{
    /// <summary>
    /// Gets the health status of a specific agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's health response.</returns>
    Task<AgentHealthResponse> GetAgentHealthAsync(
        string agentId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resource usage of a specific agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's resource usage.</returns>
    Task<AgentResourceUsage> GetAgentResourceUsageAsync(
        string agentId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a job with a specific agent before assignment.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="request">The job request to validate.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the agent can execute the job.</returns>
    Task<bool> ValidateJobWithAgentAsync(
        string agentId,
        JobRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a custom callback to an agent and waits for a response.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="request">The callback request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The callback response.</returns>
    Task<AgentCallbackResponse> SendCallbackAsync(
        string agentId,
        AgentCallbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health status from all connected agents.
    /// </summary>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of agent IDs to their health responses.</returns>
    Task<IReadOnlyDictionary<string, AgentHealthResponse>> GetAllAgentHealthAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource usage from all connected agents.
    /// </summary>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of agent IDs to their resource usage.</returns>
    Task<IReadOnlyDictionary<string, AgentResourceUsage>> GetAllAgentResourceUsageAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
