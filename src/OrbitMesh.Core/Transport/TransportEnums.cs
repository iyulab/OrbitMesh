namespace OrbitMesh.Core.Transport;

/// <summary>
/// Transport layer type for agent communication.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Centralized SignalR transport (default, always available).
    /// </summary>
    SignalR,

    /// <summary>
    /// Direct UDP peer-to-peer transport.
    /// </summary>
    P2P,

    /// <summary>
    /// TURN relay transport (fallback for symmetric NAT).
    /// </summary>
    Relay
}

/// <summary>
/// Connection state of a transport.
/// </summary>
public enum TransportState
{
    /// <summary>
    /// Transport is not connected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Transport is establishing connection.
    /// </summary>
    Connecting,

    /// <summary>
    /// Transport is connected and ready.
    /// </summary>
    Connected,

    /// <summary>
    /// Transport is attempting to reconnect after a failure.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Transport has permanently failed.
    /// </summary>
    Failed
}
