using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Transport.P2P;

/// <summary>
/// Gathers ICE candidates for P2P connection establishment.
/// </summary>
public class IceGatherer
{
    private readonly P2POptions _options;
    private readonly ILogger<IceGatherer> _logger;

    public IceGatherer(IOptions<P2POptions> options, ILogger<IceGatherer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gathers all available ICE candidates for this agent.
    /// </summary>
    /// <param name="localPort">The local UDP port being used.</param>
    /// <param name="natInfo">NAT information from STUN if available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of gathered ICE candidates.</returns>
    public async Task<IReadOnlyList<IceCandidate>> GatherCandidatesAsync(
        int localPort,
        NatInfo? natInfo = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<IceCandidate>();
        var localPreference = 65535; // Decrements for each candidate

        // 1. Gather Host candidates (local addresses)
        var localAddresses = GetLocalAddresses();
        foreach (var addr in localAddresses)
        {
            var candidate = IceCandidate.CreateHost(addr.ToString(), localPort,
                IceCandidate.CalculatePriority(IceCandidateType.Host, localPreference--));
            candidates.Add(candidate);
            _logger.LogDebug("Gathered host candidate: {Address}:{Port}", addr, localPort);
        }

        // 2. Gather Server Reflexive candidate from NAT info
        if (natInfo != null)
        {
            var localAddress = natInfo.LocalAddress ?? (localAddresses.Count > 0 ? localAddresses[0].ToString() : "0.0.0.0");
            var srflxCandidate = IceCandidate.CreateServerReflexive(
                natInfo.PublicAddress,
                natInfo.PublicPort,
                localAddress,
                natInfo.LocalPort ?? localPort,
                IceCandidate.CalculatePriority(IceCandidateType.ServerReflexive, localPreference--));
            candidates.Add(srflxCandidate);
            _logger.LogDebug("Gathered server reflexive candidate: {Address}:{Port}",
                natInfo.PublicAddress, natInfo.PublicPort);
        }

        // 3. Gather Relayed candidate if TURN is configured and needed
        if (!string.IsNullOrEmpty(_options.TurnServer) &&
            (natInfo?.Type == NatType.Symmetric || _options.FallbackToRelay))
        {
            try
            {
                var relayCandidate = await AllocateTurnRelayAsync(cancellationToken);
                if (relayCandidate != null)
                {
                    candidates.Add(relayCandidate);
                    _logger.LogDebug("Gathered relayed candidate from TURN server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to allocate TURN relay");
            }
        }

        _logger.LogInformation("Gathered {Count} ICE candidates", candidates.Count);
        return candidates.OrderByDescending(c => c.Priority).ToList();
    }

    /// <summary>
    /// Gets all usable local IPv4 addresses.
    /// </summary>
    private static List<IPAddress> GetLocalAddresses()
    {
        var addresses = new List<IPAddress>();

        try
        {
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip loopback, down, and virtual interfaces
                if (netInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                // Skip common virtual adapters
                var name = netInterface.Name;
                var desc = netInterface.Description;
                if (name.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("VMWARE", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("VBOX", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("DOCKER", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("VMWARE", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("VBOX", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("HYPER-V", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var ipProps = netInterface.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    // Only IPv4 for now
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        addresses.Add(addr.Address);
                    }
                }
            }
        }
        catch
        {
            // Fallback to Dns method
            var hostName = Dns.GetHostName();
            var hostEntry = Dns.GetHostEntry(hostName);
            addresses.AddRange(hostEntry.AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork &&
                           !IPAddress.IsLoopback(a)));
        }

        return addresses;
    }

    /// <summary>
    /// Allocates a relay address from the TURN server.
    /// Uses the TurnClient for RFC 5766 compliant TURN allocation.
    /// </summary>
    private async Task<IceCandidate?> AllocateTurnRelayAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.TurnServer))
        {
            return null;
        }

        _logger.LogInformation("Allocating TURN relay from {Server}:{Port}", _options.TurnServer, _options.TurnPort);

        // Create TURN client and allocate relay
        var turnClient = new TurnClient(
            Microsoft.Extensions.Options.Options.Create(_options),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TurnClient>.Instance);

        try
        {
            var relayCandidate = await turnClient.AllocateAsync(cancellationToken);

            if (relayCandidate != null)
            {
                _logger.LogInformation(
                    "TURN relay allocated: {Address}:{Port} via {Server}",
                    relayCandidate.Address,
                    relayCandidate.Port,
                    relayCandidate.RelayServer);

                // Store the TURN client for later use (data relay)
                _turnClient = turnClient;
                return relayCandidate;
            }

            _logger.LogWarning("TURN allocation returned no relay address");
            await turnClient.DisposeAsync();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TURN allocation failed");
            await turnClient.DisposeAsync();
            return null;
        }
    }

    private TurnClient? _turnClient;

    /// <summary>
    /// Gets the TURN client if a relay was allocated.
    /// </summary>
    public TurnClient? TurnClient => _turnClient;

    /// <summary>
    /// Performs connectivity check between local and remote candidates.
    /// </summary>
    /// <param name="localCandidate">Local ICE candidate.</param>
    /// <param name="remoteCandidate">Remote ICE candidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connectivity check succeeds.</returns>
    public async Task<bool> CheckConnectivityAsync(
        IceCandidate localCandidate,
        IceCandidate remoteCandidate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking connectivity: {Local} -> {Remote}",
            $"{localCandidate.Address}:{localCandidate.Port}",
            $"{remoteCandidate.Address}:{remoteCandidate.Port}");

        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(
                IPAddress.Parse(localCandidate.Address),
                localCandidate.Port));

            var remoteEndPoint = remoteCandidate.ToEndPoint();

            // Send STUN Binding Request
            var stunRequest = CreateStunBindingRequest();
            await udpClient.SendAsync(stunRequest, remoteEndPoint, cancellationToken);

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var result = await udpClient.ReceiveAsync(cts.Token);

            // Verify it's a STUN response
            return IsStunResponse(result.Buffer);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connectivity check failed");
            return false;
        }
    }

    private static byte[] CreateStunBindingRequest()
    {
        // Simplified STUN Binding Request (RFC 5389)
        var transactionId = Guid.NewGuid().ToByteArray()[..12];

        var message = new byte[20]; // Minimal STUN message
        // Type: Binding Request (0x0001)
        message[0] = 0x00;
        message[1] = 0x01;
        // Length: 0
        message[2] = 0x00;
        message[3] = 0x00;
        // Magic Cookie
        message[4] = 0x21;
        message[5] = 0x12;
        message[6] = 0xA4;
        message[7] = 0x42;
        // Transaction ID
        Array.Copy(transactionId, 0, message, 8, 12);

        return message;
    }

    private static bool IsStunResponse(byte[] data)
    {
        if (data.Length < 20) return false;

        // Check for STUN magic cookie
        return data[4] == 0x21 && data[5] == 0x12 && data[6] == 0xA4 && data[7] == 0x42;
    }
}
