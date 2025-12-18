using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Transport;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Transport.P2P;

/// <summary>
/// Manages P2P connections to other agents including ICE negotiation and NAT traversal.
/// </summary>
public class PeerConnectionManager
{
    private readonly LiteNetP2PTransport _transport;
    private readonly IceGatherer _iceGatherer;
    private readonly P2POptions _options;
    private readonly ILogger<PeerConnectionManager> _logger;

    private readonly ConcurrentDictionary<string, PeerConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PeerConnectionResponse>> _pendingResponses = new();

    private IP2PServerHub? _signalingHub;
    private NatInfo? _localNatInfo;

    public PeerConnectionManager(
        LiteNetP2PTransport transport,
        IceGatherer iceGatherer,
        IOptions<P2POptions> options,
        ILogger<PeerConnectionManager> logger)
    {
        _transport = transport;
        _iceGatherer = iceGatherer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sets the signaling hub for ICE candidate exchange.
    /// </summary>
    public void SetSignalingHub(IP2PServerHub signalingHub)
    {
        _signalingHub = signalingHub;
    }

    /// <summary>
    /// Sets the local NAT information (typically obtained from server's STUN).
    /// </summary>
    public void SetLocalNatInfo(NatInfo natInfo)
    {
        _localNatInfo = natInfo;
    }

    /// <summary>
    /// Gets the current connection state for a peer.
    /// </summary>
    public PeerConnectionState? GetConnectionState(string peerId)
    {
        return _connections.TryGetValue(peerId, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all current peer connections.
    /// </summary>
    public IReadOnlyDictionary<string, PeerConnectionState> Connections => _connections;

    /// <summary>
    /// Initiates a P2P connection to another agent.
    /// </summary>
    /// <param name="peerId">The target agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final connection state.</returns>
    public async Task<PeerConnectionState> ConnectToPeerAsync(
        string peerId,
        CancellationToken cancellationToken = default)
    {
        if (_signalingHub == null)
        {
            throw new InvalidOperationException("Signaling hub not configured");
        }

        _logger.LogInformation("Initiating P2P connection to {PeerId}", peerId);

        // Update state: New -> Gathering
        var state = new PeerConnectionState
        {
            PeerId = peerId,
            Status = PeerConnectionStatus.Gathering,
            LocalNatInfo = _localNatInfo
        };
        _connections[peerId] = state;

        try
        {
            // 1. Get NAT info if not already available
            if (_localNatInfo == null)
            {
                _localNatInfo = await _signalingHub.GetNatInfoAsync(cancellationToken);
                state = state with { LocalNatInfo = _localNatInfo };
                _connections[peerId] = state;
            }

            // 2. Gather local ICE candidates
            var localCandidates = await _iceGatherer.GatherCandidatesAsync(
                _transport.LocalPort,
                _localNatInfo,
                cancellationToken);

            // 3. Send connection request via signaling
            var request = new PeerConnectionRequest
            {
                FromAgentId = _transport.AgentId,
                NatInfo = _localNatInfo!,
                Candidates = localCandidates
            };

            // Create task completion source for response
            var responseTcs = new TaskCompletionSource<PeerConnectionResponse>();
            _pendingResponses[request.RequestId] = responseTcs;

            await _signalingHub.RequestPeerConnectionAsync(peerId, request, cancellationToken);

            // Update state: Gathering -> Connecting
            state = state with
            {
                Status = PeerConnectionStatus.Connecting,
                LocalCandidate = localCandidates.Count > 0 ? localCandidates[0] : null
            };
            _connections[peerId] = state;

            // 4. Wait for response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.ConnectionTimeoutMs);

            PeerConnectionResponse response;
            try
            {
                await using (timeoutCts.Token.Register(() => responseTcs.TrySetCanceled()))
                {
                    response = await responseTcs.Task;
                }
            }
            finally
            {
                _pendingResponses.TryRemove(request.RequestId, out _);
            }

            if (!response.Accepted)
            {
                _logger.LogWarning("Connection to {PeerId} rejected: {Reason}", peerId, response.RejectionReason);
                state = state with
                {
                    Status = PeerConnectionStatus.Failed,
                    ErrorMessage = response.RejectionReason
                };
                _connections[peerId] = state;
                return state;
            }

            // 5. Perform ICE connectivity checks and select best candidate pair
            var (selectedLocal, selectedRemote) = await SelectBestCandidatePairAsync(
                localCandidates,
                response.Candidates!,
                cancellationToken);

            state = state with
            {
                LocalCandidate = selectedLocal,
                RemoteCandidate = selectedRemote,
                RemoteNatInfo = response.NatInfo,
                Strategy = response.Strategy ?? DetermineStrategy(_localNatInfo, response.NatInfo)
            };
            _connections[peerId] = state;

            // 6. Establish connection based on strategy
            var connected = await EstablishConnectionAsync(
                peerId,
                selectedLocal,
                selectedRemote,
                state.Strategy!.Value,
                cancellationToken);

            // 7. Update final state
            state = state with
            {
                Status = connected ? PeerConnectionStatus.Connected : PeerConnectionStatus.Failed,
                ActiveTransport = connected ? TransportType.P2P : TransportType.SignalR,
                ConnectedAt = connected ? DateTimeOffset.UtcNow : null,
                ErrorMessage = connected ? null : "Failed to establish P2P connection"
            };
            _connections[peerId] = state;

            if (connected)
            {
                _logger.LogInformation("P2P connection to {PeerId} established via {Strategy}",
                    peerId, state.Strategy);
            }
            else if (_options.FallbackToRelay)
            {
                _logger.LogWarning("P2P failed, will use SignalR relay for {PeerId}", peerId);
            }

            return state;
        }
        catch (OperationCanceledException)
        {
            state = state with
            {
                Status = PeerConnectionStatus.Failed,
                ErrorMessage = "Connection timed out"
            };
            _connections[peerId] = state;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to peer {PeerId}", peerId);
            state = state with
            {
                Status = PeerConnectionStatus.Failed,
                ErrorMessage = ex.Message
            };
            _connections[peerId] = state;
            throw;
        }
    }

    /// <summary>
    /// Handles an incoming connection request from another agent.
    /// </summary>
    public async Task HandleConnectionRequestAsync(
        string fromAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received P2P connection request from {AgentId}", fromAgentId);

        if (_signalingHub == null)
        {
            _logger.LogWarning("Cannot handle P2P request: signaling hub not configured");
            return;
        }

        // Gather our candidates
        var localCandidates = await _iceGatherer.GatherCandidatesAsync(
            _transport.LocalPort,
            _localNatInfo,
            cancellationToken);

        var strategy = request.RecommendedStrategy ??
            DetermineStrategy(_localNatInfo, request.NatInfo);

        // Send response
        var response = new PeerConnectionResponse
        {
            RequestId = request.RequestId,
            FromAgentId = _transport.AgentId,
            Accepted = true,
            NatInfo = _localNatInfo,
            Candidates = localCandidates,
            Strategy = strategy
        };

        await _signalingHub.RespondPeerConnectionAsync(fromAgentId, response, cancellationToken);

        // Update our connection state
        var state = new PeerConnectionState
        {
            PeerId = fromAgentId,
            Status = PeerConnectionStatus.Connecting,
            LocalNatInfo = _localNatInfo,
            RemoteNatInfo = request.NatInfo,
            Strategy = strategy
        };
        _connections[fromAgentId] = state;
    }

    /// <summary>
    /// Handles an incoming connection response.
    /// </summary>
    public void HandleConnectionResponse(string fromAgentId, PeerConnectionResponse response)
    {
        _logger.LogDebug("Received P2P connection response from {AgentId}", fromAgentId);

        if (_pendingResponses.TryGetValue(response.RequestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    /// <summary>
    /// Closes a P2P connection.
    /// </summary>
    public Task ClosePeerConnectionAsync(string peerId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryRemove(peerId, out var state))
        {
            _logger.LogInformation("Closed P2P connection to {PeerId}", peerId);
        }
        return Task.CompletedTask;
    }

    private async Task<(IceCandidate Local, IceCandidate Remote)> SelectBestCandidatePairAsync(
        IReadOnlyList<IceCandidate> localCandidates,
        IReadOnlyList<IceCandidate> remoteCandidates,
        CancellationToken cancellationToken)
    {
        // Sort candidates by priority
        var localSorted = localCandidates.OrderByDescending(c => c.Priority).ToList();
        var remoteSorted = remoteCandidates.OrderByDescending(c => c.Priority).ToList();

        // Try each candidate pair in priority order
        foreach (var local in localSorted)
        {
            foreach (var remote in remoteSorted)
            {
                // Skip incompatible types (e.g., both are relay)
                if (local.Type == IceCandidateType.Relayed && remote.Type == IceCandidateType.Relayed)
                    continue;

                var success = await _iceGatherer.CheckConnectivityAsync(local, remote, cancellationToken);
                if (success)
                {
                    _logger.LogDebug("Selected candidate pair: {LocalType} -> {RemoteType}",
                        local.Type, remote.Type);
                    return (local, remote);
                }
            }
        }

        // If no pair worked through connectivity check, return highest priority pair
        // and let the actual connection attempt determine success
        _logger.LogDebug("Using highest priority candidates without connectivity check");
        return (localSorted.First(), remoteSorted.First());
    }

    private async Task<bool> EstablishConnectionAsync(
        string peerId,
        IceCandidate localCandidate,
        IceCandidate remoteCandidate,
        ConnectionStrategy strategy,
        CancellationToken cancellationToken)
    {
        var remoteEndPoint = remoteCandidate.ToEndPoint();
        var connectionToken = $"{_transport.AgentId}:{peerId}:{Guid.NewGuid():N}";

        _logger.LogDebug("Establishing connection using strategy: {Strategy}", strategy);

        switch (strategy)
        {
            case ConnectionStrategy.DirectConnect:
                return await _transport.DirectConnectAsync(peerId, remoteEndPoint, connectionToken, cancellationToken);

            case ConnectionStrategy.SimultaneousOpen:
            case ConnectionStrategy.UdpHolePunch:
                return await _transport.ConnectToPeerAsync(peerId, remoteEndPoint, connectionToken, cancellationToken);

            case ConnectionStrategy.TurnRelay:
                return await EstablishTurnRelayConnectionAsync(peerId, localCandidate, remoteCandidate, cancellationToken);

            default:
                _logger.LogWarning("Unknown connection strategy: {Strategy}", strategy);
                return false;
        }
    }

    /// <summary>
    /// Establishes a connection through TURN relay when direct P2P is not possible.
    /// </summary>
    private async Task<bool> EstablishTurnRelayConnectionAsync(
        string peerId,
        IceCandidate localCandidate,
        IceCandidate remoteCandidate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Establishing TURN relay connection to {PeerId}", peerId);

        // Get the TURN client from the ICE gatherer
        var turnClient = _iceGatherer.TurnClient;

        // If we have a local relayed candidate, use our TURN client
        if (turnClient != null && turnClient.IsAllocated)
        {
            var remoteEndPoint = remoteCandidate.ToEndPoint();

            // Create permission for the peer's address
            var permissionCreated = await turnClient.CreatePermissionAsync(remoteEndPoint, cancellationToken);
            if (!permissionCreated)
            {
                _logger.LogWarning("Failed to create TURN permission for {PeerId}", peerId);
                return false;
            }

            // Bind a channel for efficient data transfer
            var channel = await turnClient.BindChannelAsync(remoteEndPoint, cancellationToken);
            if (channel == 0)
            {
                _logger.LogWarning("Failed to bind TURN channel for {PeerId}", peerId);
                // Permission is still valid, can use Send indication
            }

            // Store the TURN client for this peer
            _turnClients[peerId] = turnClient;

            // Set up data reception handler
            turnClient.DataReceived += (sender, args) =>
            {
                OnTurnDataReceived(peerId, args.Data);
            };

            _logger.LogInformation(
                "TURN relay established for {PeerId}, Channel: {Channel}",
                peerId, channel > 0 ? channel.ToString(System.Globalization.CultureInfo.InvariantCulture) : "N/A (using Send indication)");

            return true;
        }

        // If the remote has a relayed candidate, connect to their relay address
        if (remoteCandidate.Type == IceCandidateType.Relayed && remoteCandidate.RelayServer != null)
        {
            var relayEndPoint = new IPEndPoint(
                IPAddress.Parse(remoteCandidate.Address),
                remoteCandidate.Port);

            var connectionToken = $"{_transport.AgentId}:{peerId}:{Guid.NewGuid():N}";
            return await _transport.DirectConnectAsync(peerId, relayEndPoint, connectionToken, cancellationToken);
        }

        _logger.LogWarning("No TURN relay available for connection to {PeerId}", peerId);
        return false;
    }

    // Store TURN clients for peer connections
    private readonly ConcurrentDictionary<string, TurnClient> _turnClients = new();

    /// <summary>
    /// Sends data to a peer, using TURN relay if that's the active transport.
    /// </summary>
    public async Task SendToPeerAsync(string peerId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var connection = GetConnectionState(peerId);

        if (connection?.ActiveTransport == TransportType.Relay && _turnClients.TryGetValue(peerId, out var turnClient))
        {
            // Use TURN relay
            if (connection.RemoteCandidate != null)
            {
                var remoteEndPoint = connection.RemoteCandidate.ToEndPoint();
                await turnClient.SendDataAsync(remoteEndPoint, data, cancellationToken);
            }
        }
        else
        {
            // Use direct P2P transport
            await _transport.SendAsync(peerId, data, cancellationToken);
        }
    }

    private void OnTurnDataReceived(string peerId, byte[] data)
    {
        // Forward TURN relayed data as if it came from the transport
        _logger.LogDebug("Received {Size} bytes via TURN relay from {PeerId}", data.Length, peerId);
        // The transport layer will handle routing this to the appropriate handler
    }

    private static ConnectionStrategy DetermineStrategy(NatInfo? localNat, NatInfo? remoteNat)
    {
        if (localNat == null || remoteNat == null)
        {
            return ConnectionStrategy.UdpHolePunch;
        }

        // Symmetric NAT on either side requires TURN
        if (localNat.Type == NatType.Symmetric || remoteNat.Type == NatType.Symmetric)
        {
            return ConnectionStrategy.TurnRelay;
        }

        // Open or FullCone can do direct connect
        if (localNat.Type == NatType.Open || remoteNat.Type == NatType.Open ||
            localNat.Type == NatType.FullCone || remoteNat.Type == NatType.FullCone)
        {
            return ConnectionStrategy.DirectConnect;
        }

        // Default to UDP hole punching
        return ConnectionStrategy.UdpHolePunch;
    }
}
