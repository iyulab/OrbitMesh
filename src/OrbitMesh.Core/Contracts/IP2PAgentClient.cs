using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// P2P signaling extension for agent client interface.
/// Agents implementing P2P capabilities should also implement this interface.
/// </summary>
public interface IP2PAgentClient
{
    /// <summary>
    /// Receives an ICE candidate from another agent via the signaling server.
    /// </summary>
    /// <param name="fromAgentId">The agent sending the candidate.</param>
    /// <param name="candidate">The ICE candidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceiveIceCandidateAsync(
        string fromAgentId,
        IceCandidate candidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a P2P connection request from another agent.
    /// </summary>
    /// <param name="fromAgentId">The agent requesting the connection.</param>
    /// <param name="request">The connection request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceivePeerConnectionRequestAsync(
        string fromAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a P2P connection response from another agent.
    /// </summary>
    /// <param name="fromAgentId">The agent responding to the connection request.</param>
    /// <param name="response">The connection response details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceivePeerConnectionResponseAsync(
        string fromAgentId,
        PeerConnectionResponse response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives notification that a peer connection state has changed.
    /// </summary>
    /// <param name="peerId">The peer whose connection state changed.</param>
    /// <param name="state">The new connection state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceivePeerConnectionStateChangedAsync(
        string peerId,
        PeerConnectionState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives data relayed through the server (fallback when P2P fails).
    /// </summary>
    /// <param name="fromAgentId">The sending agent.</param>
    /// <param name="data">The data being relayed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceiveRelayedDataAsync(
        string fromAgentId,
        byte[] data,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Capability marker indicating an agent supports P2P connections.
/// </summary>
public interface IP2PCapableAgent : IAgentClient, IP2PAgentClient
{
}
