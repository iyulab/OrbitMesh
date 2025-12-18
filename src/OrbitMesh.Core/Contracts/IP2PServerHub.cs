using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// P2P signaling extension for server hub interface.
/// Provides ICE signaling, NAT detection, and peer coordination services.
/// </summary>
public interface IP2PServerHub
{
    /// <summary>
    /// Gets NAT information for the calling agent by analyzing the connection.
    /// Uses the embedded STUN server functionality.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>NAT information including type, public IP, and port.</returns>
    Task<NatInfo> GetNatInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an ICE candidate to another agent via the signaling server.
    /// </summary>
    /// <param name="toAgentId">The target agent ID.</param>
    /// <param name="candidate">The ICE candidate to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendIceCandidateAsync(
        string toAgentId,
        IceCandidate candidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a P2P connection with another agent.
    /// The server will relay the request and may suggest an optimal connection strategy.
    /// </summary>
    /// <param name="toAgentId">The target agent ID.</param>
    /// <param name="request">The connection request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RequestPeerConnectionAsync(
        string toAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Responds to a P2P connection request from another agent.
    /// </summary>
    /// <param name="toAgentId">The agent who sent the original request.</param>
    /// <param name="response">The connection response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RespondPeerConnectionAsync(
        string toAgentId,
        PeerConnectionResponse response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the current state of a P2P connection to the server.
    /// Used for monitoring and connection quality tracking.
    /// </summary>
    /// <param name="peerId">The connected peer ID.</param>
    /// <param name="state">The current connection state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportPeerConnectionStateAsync(
        string peerId,
        PeerConnectionState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relays data through the server to another agent.
    /// Used as fallback when direct P2P connection is not possible.
    /// </summary>
    /// <param name="toAgentId">The target agent ID.</param>
    /// <param name="data">The data to relay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RelayDataAsync(
        string toAgentId,
        byte[] data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the target agent supports P2P connections.
    /// </summary>
    /// <param name="agentId">The agent to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the agent supports P2P, false otherwise.</returns>
    Task<bool> IsAgentP2PCapableAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recommended connection strategy for connecting to a specific agent.
    /// Based on both agents' NAT types.
    /// </summary>
    /// <param name="toAgentId">The target agent ID.</param>
    /// <param name="localNatInfo">The local agent's NAT information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recommended connection strategy.</returns>
    Task<ConnectionStrategy> GetRecommendedStrategyAsync(
        string toAgentId,
        NatInfo localNatInfo,
        CancellationToken cancellationToken = default);
}
