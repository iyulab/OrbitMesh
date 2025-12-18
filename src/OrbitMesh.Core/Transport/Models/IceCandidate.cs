using System.Net;

namespace OrbitMesh.Core.Transport.Models;

/// <summary>
/// ICE candidate type based on RFC 8445.
/// </summary>
public enum IceCandidateType
{
    /// <summary>
    /// Host candidate - local IP address.
    /// </summary>
    Host,

    /// <summary>
    /// Server reflexive candidate - public IP discovered via STUN.
    /// </summary>
    ServerReflexive,

    /// <summary>
    /// Peer reflexive candidate - discovered during connectivity checks.
    /// </summary>
    PeerReflexive,

    /// <summary>
    /// Relayed candidate - allocated from TURN server.
    /// </summary>
    Relayed
}

/// <summary>
/// ICE candidate representing a potential connection endpoint.
/// </summary>
public record IceCandidate
{
    /// <summary>
    /// Gets the unique identifier for this candidate.
    /// </summary>
    public required string CandidateId { get; init; }

    /// <summary>
    /// Gets the candidate type.
    /// </summary>
    public required IceCandidateType Type { get; init; }

    /// <summary>
    /// Gets the IP address or hostname.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets the port number.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets the transport protocol (typically UDP).
    /// </summary>
    public string Protocol { get; init; } = "udp";

    /// <summary>
    /// Gets the priority value for candidate selection (higher is better).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets the foundation string for candidate pairing.
    /// </summary>
    public string? Foundation { get; init; }

    /// <summary>
    /// Gets the component ID (1 for RTP, 2 for RTCP in media, 1 for data channels).
    /// </summary>
    public int ComponentId { get; init; } = 1;

    /// <summary>
    /// Gets the related address for server reflexive and relayed candidates.
    /// </summary>
    public string? RelatedAddress { get; init; }

    /// <summary>
    /// Gets the related port for server reflexive and relayed candidates.
    /// </summary>
    public int? RelatedPort { get; init; }

    /// <summary>
    /// Gets the TURN server address for relayed candidates.
    /// </summary>
    public string? RelayServer { get; init; }

    /// <summary>
    /// Creates an IPEndPoint from this candidate.
    /// </summary>
    public IPEndPoint ToEndPoint()
    {
        var address = IPAddress.Parse(Address);
        return new IPEndPoint(address, Port);
    }

    /// <summary>
    /// Creates a host candidate from local address.
    /// </summary>
    public static IceCandidate CreateHost(string address, int port, int priority = 0)
    {
        return new IceCandidate
        {
            CandidateId = Guid.NewGuid().ToString("N"),
            Type = IceCandidateType.Host,
            Address = address,
            Port = port,
            Priority = priority > 0 ? priority : CalculatePriority(IceCandidateType.Host, 0),
            Foundation = $"host-{address}"
        };
    }

    /// <summary>
    /// Creates a server reflexive candidate from STUN response.
    /// </summary>
    public static IceCandidate CreateServerReflexive(
        string publicAddress, int publicPort,
        string localAddress, int localPort,
        int priority = 0)
    {
        return new IceCandidate
        {
            CandidateId = Guid.NewGuid().ToString("N"),
            Type = IceCandidateType.ServerReflexive,
            Address = publicAddress,
            Port = publicPort,
            Priority = priority > 0 ? priority : CalculatePriority(IceCandidateType.ServerReflexive, 0),
            Foundation = $"srflx-{publicAddress}",
            RelatedAddress = localAddress,
            RelatedPort = localPort
        };
    }

    /// <summary>
    /// Creates a relayed candidate from TURN allocation.
    /// </summary>
    public static IceCandidate CreateRelayed(
        string relayedAddress, int relayedPort,
        string relayServer,
        int priority = 0)
    {
        return new IceCandidate
        {
            CandidateId = Guid.NewGuid().ToString("N"),
            Type = IceCandidateType.Relayed,
            Address = relayedAddress,
            Port = relayedPort,
            Priority = priority > 0 ? priority : CalculatePriority(IceCandidateType.Relayed, 0),
            Foundation = $"relay-{relayServer}",
            RelayServer = relayServer
        };
    }

    /// <summary>
    /// Creates a relayed candidate from TURN allocation with related address info.
    /// </summary>
    public static IceCandidate CreateRelayed(
        string relayedAddress, int relayedPort,
        string localAddress, int localPort,
        string relayServer, int relayServerPort,
        int priority = 0)
    {
        return new IceCandidate
        {
            CandidateId = Guid.NewGuid().ToString("N"),
            Type = IceCandidateType.Relayed,
            Address = relayedAddress,
            Port = relayedPort,
            Priority = priority > 0 ? priority : CalculatePriority(IceCandidateType.Relayed, 0),
            Foundation = $"relay-{relayServer}:{relayServerPort}",
            RelayServer = $"{relayServer}:{relayServerPort}",
            RelatedAddress = localAddress,
            RelatedPort = localPort
        };
    }

    /// <summary>
    /// Calculates ICE priority based on RFC 8445 formula.
    /// priority = (2^24) * type_preference + (2^8) * local_preference + (2^0) * (256 - component_id)
    /// </summary>
    public static int CalculatePriority(IceCandidateType type, int localPreference, int componentId = 1)
    {
        var typePreference = type switch
        {
            IceCandidateType.Host => 126,
            IceCandidateType.PeerReflexive => 110,
            IceCandidateType.ServerReflexive => 100,
            IceCandidateType.Relayed => 0,
            _ => 0
        };

        return (typePreference << 24) + (localPreference << 8) + (256 - componentId);
    }
}
