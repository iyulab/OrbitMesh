using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// Defines methods that the server can invoke on connected agents.
/// This is the strongly-typed SignalR client interface.
/// </summary>
public interface IAgentClient
{
    /// <summary>
    /// Executes a job on the agent.
    /// </summary>
    /// <param name="request">The job request to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteJobAsync(JobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a heartbeat response from the agent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the agent's desired state from the server.
    /// </summary>
    /// <param name="desiredState">The desired state properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateDesiredStateAsync(
        IReadOnlyDictionary<string, string> desiredState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the agent to gracefully shutdown.
    /// </summary>
    /// <param name="reason">Optional reason for the shutdown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ShutdownAsync(string? reason = null, CancellationToken cancellationToken = default);
}
