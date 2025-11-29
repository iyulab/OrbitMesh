using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Registry for managing connected agents.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Registers an agent.
    /// </summary>
    Task RegisterAsync(AgentInfo agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters an agent.
    /// </summary>
    Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent by ID.
    /// </summary>
    Task<AgentInfo?> GetAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all connected agents.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets agents by capability.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetByCapabilityAsync(
        string capability,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets agents by group.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetByGroupAsync(
        string group,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an agent's status.
    /// </summary>
    Task UpdateStatusAsync(
        string agentId,
        AgentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an agent's heartbeat timestamp.
    /// </summary>
    Task UpdateHeartbeatAsync(
        string agentId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);
}
