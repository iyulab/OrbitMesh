namespace OrbitMesh.Host.Services.P2P;

/// <summary>
/// Configuration options for P2P server services.
/// </summary>
public class P2PServerOptions
{
    /// <summary>
    /// Gets or sets whether P2P features are enabled.
    /// Default is false for backward compatibility.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the embedded STUN server port.
    /// Default is 3478 (standard STUN port).
    /// </summary>
    public int StunPort { get; set; } = 3478;

    /// <summary>
    /// Gets or sets the secondary STUN port for NAT type detection.
    /// Default is 3479.
    /// </summary>
    public int StunSecondaryPort { get; set; } = 3479;

    /// <summary>
    /// Gets or sets whether the embedded STUN server is enabled.
    /// When disabled, agents must use an external STUN server.
    /// Default is true.
    /// </summary>
    public bool EnableEmbeddedStun { get; set; } = true;

    /// <summary>
    /// Gets or sets the external STUN server address.
    /// Used when embedded STUN is disabled.
    /// </summary>
    public string? ExternalStunServer { get; set; }

    /// <summary>
    /// Gets or sets the external STUN server port.
    /// Default is 3478.
    /// </summary>
    public int ExternalStunPort { get; set; } = 3478;

    /// <summary>
    /// Gets or sets whether to track P2P connection metrics.
    /// Default is true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval in seconds for peer connection health checks.
    /// Default is 30 seconds.
    /// </summary>
    public int PeerHealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the timeout in seconds for NAT detection requests.
    /// Default is 5 seconds.
    /// </summary>
    public int NatDetectionTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to allow SignalR relay as fallback when P2P fails.
    /// Default is true.
    /// </summary>
    public bool AllowRelayFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent P2P connections per agent.
    /// 0 means unlimited. Default is 0.
    /// </summary>
    public int MaxConnectionsPerAgent { get; set; }
}
