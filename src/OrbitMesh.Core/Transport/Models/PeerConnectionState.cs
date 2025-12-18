namespace OrbitMesh.Core.Transport.Models;

/// <summary>
/// Status of a peer-to-peer connection.
/// </summary>
public enum PeerConnectionStatus
{
    /// <summary>
    /// Connection object created but not started.
    /// </summary>
    New,

    /// <summary>
    /// Gathering ICE candidates.
    /// </summary>
    Gathering,

    /// <summary>
    /// Performing connectivity checks / NAT hole punching.
    /// </summary>
    Connecting,

    /// <summary>
    /// Direct P2P connection established.
    /// </summary>
    Connected,

    /// <summary>
    /// Using TURN relay (fallback for symmetric NAT).
    /// </summary>
    Relayed,

    /// <summary>
    /// Connection failed after all attempts.
    /// </summary>
    Failed,

    /// <summary>
    /// Connection was closed.
    /// </summary>
    Closed
}

/// <summary>
/// Connection strategy recommendation based on NAT types.
/// </summary>
public enum ConnectionStrategy
{
    /// <summary>
    /// Direct connection possible (one side is Open/FullCone).
    /// </summary>
    DirectConnect,

    /// <summary>
    /// Simultaneous open technique (both sides open at same time).
    /// </summary>
    SimultaneousOpen,

    /// <summary>
    /// UDP hole punching required.
    /// </summary>
    UdpHolePunch,

    /// <summary>
    /// TURN relay required (symmetric NAT detected).
    /// </summary>
    TurnRelay
}

/// <summary>
/// Performance metrics for a peer connection.
/// </summary>
public record PeerConnectionMetrics
{
    /// <summary>
    /// Gets the round-trip time to the peer.
    /// </summary>
    public TimeSpan RoundTripTime { get; init; }

    /// <summary>
    /// Gets the total bytes sent.
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// Gets the total bytes received.
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Gets the packet loss rate (0.0 to 1.0).
    /// </summary>
    public double PacketLossRate { get; init; }

    /// <summary>
    /// Gets the current estimated bandwidth in bytes per second.
    /// </summary>
    public long EstimatedBandwidth { get; init; }

    /// <summary>
    /// Gets the jitter in milliseconds.
    /// </summary>
    public double JitterMs { get; init; }

    /// <summary>
    /// Gets the timestamp when metrics were last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// State of a peer-to-peer connection.
/// </summary>
public record PeerConnectionState
{
    /// <summary>
    /// Gets the remote peer identifier.
    /// </summary>
    public required string PeerId { get; init; }

    /// <summary>
    /// Gets the current connection status.
    /// </summary>
    public required PeerConnectionStatus Status { get; init; }

    /// <summary>
    /// Gets the active transport type.
    /// </summary>
    public Transport.TransportType ActiveTransport { get; init; }

    /// <summary>
    /// Gets the selected local ICE candidate.
    /// </summary>
    public IceCandidate? LocalCandidate { get; init; }

    /// <summary>
    /// Gets the selected remote ICE candidate.
    /// </summary>
    public IceCandidate? RemoteCandidate { get; init; }

    /// <summary>
    /// Gets the local NAT information.
    /// </summary>
    public NatInfo? LocalNatInfo { get; init; }

    /// <summary>
    /// Gets the remote NAT information.
    /// </summary>
    public NatInfo? RemoteNatInfo { get; init; }

    /// <summary>
    /// Gets the connection strategy used.
    /// </summary>
    public ConnectionStrategy? Strategy { get; init; }

    /// <summary>
    /// Gets the connection metrics.
    /// </summary>
    public PeerConnectionMetrics? Metrics { get; init; }

    /// <summary>
    /// Gets the error message if connection failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets when the connection was established.
    /// </summary>
    public DateTimeOffset? ConnectedAt { get; init; }

    /// <summary>
    /// Gets when the connection state was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets whether the connection is active (Connected or Relayed).
    /// </summary>
    public bool IsActive => Status == PeerConnectionStatus.Connected || Status == PeerConnectionStatus.Relayed;
}
