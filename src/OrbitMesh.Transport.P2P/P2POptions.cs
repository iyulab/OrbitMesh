namespace OrbitMesh.Transport.P2P;

/// <summary>
/// Configuration options for P2P transport.
/// </summary>
public class P2POptions
{
    /// <summary>
    /// Gets or sets whether to prefer P2P connections over SignalR relay.
    /// Default is true.
    /// </summary>
    public bool PreferP2P { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fallback to server relay when P2P fails.
    /// Default is true.
    /// </summary>
    public bool FallbackToRelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the local UDP port to bind to.
    /// 0 means auto-select an available port.
    /// </summary>
    public int LocalPort { get; set; }

    /// <summary>
    /// Gets or sets the STUN server address.
    /// If not specified, uses the OrbitMesh server's embedded STUN.
    /// </summary>
    public string? StunServer { get; set; }

    /// <summary>
    /// Gets or sets the STUN server port.
    /// Default is 3478 (standard STUN port).
    /// </summary>
    public int StunPort { get; set; } = 3478;

    /// <summary>
    /// Gets or sets the TURN server address for relay fallback.
    /// </summary>
    public string? TurnServer { get; set; }

    /// <summary>
    /// Gets or sets the TURN server port.
    /// Default is 3478.
    /// </summary>
    public int TurnPort { get; set; } = 3478;

    /// <summary>
    /// Gets or sets the TURN username for authentication.
    /// </summary>
    public string? TurnUsername { get; set; }

    /// <summary>
    /// Gets or sets the TURN password for authentication.
    /// </summary>
    public string? TurnPassword { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// Default is 10000 (10 seconds).
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the NAT punch retry count.
    /// Default is 10.
    /// </summary>
    public int NatPunchRetries { get; set; } = 10;

    /// <summary>
    /// Gets or sets the interval between NAT punch attempts in milliseconds.
    /// Default is 100.
    /// </summary>
    public int NatPunchIntervalMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the keep-alive interval in milliseconds.
    /// Default is 15000 (15 seconds).
    /// </summary>
    public int KeepAliveIntervalMs { get; set; } = 15000;

    /// <summary>
    /// Gets or sets the disconnect timeout in milliseconds.
    /// Default is 30000 (30 seconds).
    /// </summary>
    public int DisconnectTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to enable unreliable (UDP) delivery mode.
    /// Default is true for performance-critical data.
    /// </summary>
    public bool EnableUnreliableDelivery { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable reliable sequenced delivery mode.
    /// Default is true for ordered data.
    /// </summary>
    public bool EnableReliableDelivery { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum transmission unit size in bytes.
    /// Default is 1400 to avoid fragmentation.
    /// </summary>
    public int MaxMtu { get; set; } = 1400;

    /// <summary>
    /// Gets or sets whether to enable simulating network conditions.
    /// Only for testing/debugging.
    /// </summary>
    public bool EnableSimulation { get; set; }

    /// <summary>
    /// Gets or sets the simulated packet loss percentage (0-100).
    /// Only used if EnableSimulation is true.
    /// </summary>
    public int SimulatedPacketLossPercent { get; set; }

    /// <summary>
    /// Gets or sets the simulated latency in milliseconds.
    /// Only used if EnableSimulation is true.
    /// </summary>
    public int SimulatedLatencyMs { get; set; }
}
