using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Host.Services.P2P;

/// <summary>
/// Service for coordinating P2P connections between agents.
/// Determines optimal connection strategies based on NAT types.
/// </summary>
public interface IPeerCoordinator
{
    /// <summary>
    /// Determines the recommended connection strategy based on both agents' NAT information.
    /// </summary>
    /// <param name="initiatorNat">NAT information of the initiating agent.</param>
    /// <param name="responderNat">NAT information of the responding agent.</param>
    /// <returns>The recommended connection strategy.</returns>
    ConnectionStrategy DetermineStrategy(NatInfo? initiatorNat, NatInfo? responderNat);

    /// <summary>
    /// Gets the cached NAT information for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>The cached NAT info, or null if not available.</returns>
    NatInfo? GetCachedNatInfo(string agentId);

    /// <summary>
    /// Caches NAT information for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="natInfo">The NAT information to cache.</param>
    void CacheNatInfo(string agentId, NatInfo natInfo);

    /// <summary>
    /// Removes cached NAT information for an agent.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    void RemoveCachedNatInfo(string agentId);

    /// <summary>
    /// Checks if an agent is P2P capable.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <returns>True if the agent supports P2P connections.</returns>
    Task<bool> IsAgentP2PCapableAsync(string agentId);

    /// <summary>
    /// Registers an agent as P2P capable.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    void RegisterP2PAgent(string agentId);

    /// <summary>
    /// Unregisters an agent from P2P capability.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    void UnregisterP2PAgent(string agentId);

    /// <summary>
    /// Records a P2P connection state for metrics and monitoring.
    /// </summary>
    /// <param name="fromAgentId">The initiating agent.</param>
    /// <param name="toAgentId">The target agent.</param>
    /// <param name="state">The connection state.</param>
    void RecordConnectionState(string fromAgentId, string toAgentId, PeerConnectionState state);

    /// <summary>
    /// Gets the current P2P connection state between two agents.
    /// </summary>
    /// <param name="fromAgentId">The initiating agent.</param>
    /// <param name="toAgentId">The target agent.</param>
    /// <returns>The connection state, or null if no connection exists.</returns>
    PeerConnectionState? GetConnectionState(string fromAgentId, string toAgentId);
}
