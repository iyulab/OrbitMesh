namespace OrbitMesh.Core.Transport.Models;

/// <summary>
/// Request to establish a P2P connection with another agent.
/// </summary>
public record PeerConnectionRequest
{
    /// <summary>
    /// Gets the unique identifier for this connection request.
    /// </summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the agent initiating the connection.
    /// </summary>
    public required string FromAgentId { get; init; }

    /// <summary>
    /// Gets the local NAT information of the initiator.
    /// </summary>
    public required NatInfo NatInfo { get; init; }

    /// <summary>
    /// Gets the ICE candidates gathered by the initiator.
    /// </summary>
    public required IReadOnlyList<IceCandidate> Candidates { get; init; }

    /// <summary>
    /// Gets the recommended connection strategy from the server.
    /// </summary>
    public ConnectionStrategy? RecommendedStrategy { get; init; }

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the request timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Response to a P2P connection request.
/// </summary>
public record PeerConnectionResponse
{
    /// <summary>
    /// Gets the request ID this response is for.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the responding agent ID.
    /// </summary>
    public required string FromAgentId { get; init; }

    /// <summary>
    /// Gets whether the connection request was accepted.
    /// </summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Gets the NAT information of the responder.
    /// </summary>
    public NatInfo? NatInfo { get; init; }

    /// <summary>
    /// Gets the ICE candidates from the responder.
    /// </summary>
    public IReadOnlyList<IceCandidate>? Candidates { get; init; }

    /// <summary>
    /// Gets the selected connection strategy.
    /// </summary>
    public ConnectionStrategy? Strategy { get; init; }

    /// <summary>
    /// Gets the reason if the request was rejected.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Gets the timestamp when the response was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
