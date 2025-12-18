namespace OrbitMesh.Core.Transport.Models;

/// <summary>
/// NAT type classification based on RFC 3489 and RFC 5780.
/// </summary>
public enum NatType
{
    /// <summary>
    /// NAT type could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// No NAT present, direct connection possible.
    /// </summary>
    Open,

    /// <summary>
    /// Full Cone NAT - most permissive, easiest to traverse.
    /// Any external host can send packets to the mapped address.
    /// </summary>
    FullCone,

    /// <summary>
    /// Address Restricted Cone NAT.
    /// External host can send packets only if internal host has sent to that IP.
    /// </summary>
    Restricted,

    /// <summary>
    /// Port Restricted Cone NAT.
    /// External host can send packets only if internal host has sent to that IP:port.
    /// </summary>
    PortRestricted,

    /// <summary>
    /// Symmetric NAT - most restrictive, typically requires TURN relay.
    /// Different mappings for different destinations.
    /// </summary>
    Symmetric
}

/// <summary>
/// Information about NAT configuration detected via STUN.
/// </summary>
public record NatInfo
{
    /// <summary>
    /// Gets the detected NAT type.
    /// </summary>
    public required NatType Type { get; init; }

    /// <summary>
    /// Gets the public IP address as seen by STUN server.
    /// </summary>
    public required string PublicAddress { get; init; }

    /// <summary>
    /// Gets the public port as seen by STUN server.
    /// </summary>
    public required int PublicPort { get; init; }

    /// <summary>
    /// Gets the local IP address used for the binding.
    /// </summary>
    public string? LocalAddress { get; init; }

    /// <summary>
    /// Gets the local port used for the binding.
    /// </summary>
    public int? LocalPort { get; init; }

    /// <summary>
    /// Gets the estimated NAT mapping lifetime.
    /// </summary>
    public TimeSpan? MappingLifetime { get; init; }

    /// <summary>
    /// Gets whether hairpinning is supported (loopback through NAT).
    /// </summary>
    public bool? SupportsHairpinning { get; init; }

    /// <summary>
    /// Gets the timestamp when this NAT info was gathered.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Determines if direct P2P connection is likely possible with another NAT type.
    /// </summary>
    public bool CanDirectConnectWith(NatType otherNatType)
    {
        // Symmetric NAT typically requires TURN relay
        if (Type == NatType.Symmetric || otherNatType == NatType.Symmetric)
            return false;

        // Open or FullCone can connect with most NAT types
        if (Type == NatType.Open || Type == NatType.FullCone)
            return true;

        if (otherNatType == NatType.Open || otherNatType == NatType.FullCone)
            return true;

        // Restricted types can connect with each other via hole punching
        return true;
    }
}
