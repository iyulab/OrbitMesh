using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Transport;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Transport.P2P;

/// <summary>
/// P2P transport implementation using LiteNetLib with NAT punch-through support.
/// </summary>
public class LiteNetP2PTransport : ITransport, INatPunchListener, INetEventListener
{
    private readonly NetManager _netManager;
    private readonly NatPunchModule _natPunch;
    private readonly string _agentId;
    private readonly P2POptions _options;
    private readonly ILogger<LiteNetP2PTransport> _logger;

    private readonly ConcurrentDictionary<string, NetPeer> _peers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConnections = new();

    private TransportState _state = TransportState.Disconnected;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public string TransportId { get; }
    public TransportType Type => TransportType.P2P;
    public TransportState State => _state;
    public string AgentId => _agentId;
    public int LocalPort => _netManager.LocalPort;

    public event EventHandler<TransportStateChangedEventArgs>? StateChanged;
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Raised when NAT punch-through succeeds for a peer.
    /// </summary>
    public event EventHandler<NatPunchSuccessEventArgs>? NatPunchSuccess;

    public LiteNetP2PTransport(
        string agentId,
        IOptions<P2POptions> options,
        ILogger<LiteNetP2PTransport> logger)
    {
        _agentId = agentId;
        _options = options.Value;
        _logger = logger;
        TransportId = $"p2p-{agentId}";

        _netManager = new NetManager(this)
        {
            NatPunchEnabled = true,
            UpdateTime = 15,
            DisconnectTimeout = _options.DisconnectTimeoutMs,
            AutoRecycle = true,
            EnableStatistics = true
        };

        if (_options.EnableSimulation)
        {
            _netManager.SimulatePacketLoss = true;
            _netManager.SimulationPacketLossChance = _options.SimulatedPacketLossPercent;
            _netManager.SimulateLatency = true;
            _netManager.SimulationMinLatency = _options.SimulatedLatencyMs;
            _netManager.SimulationMaxLatency = _options.SimulatedLatencyMs + 50;
        }

        _natPunch = _netManager.NatPunchModule;
        _natPunch.Init(this);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_state == TransportState.Connected)
        {
            return Task.CompletedTask;
        }

        var port = _options.LocalPort > 0 ? _options.LocalPort : 0;
        if (!_netManager.Start(port))
        {
            throw new InvalidOperationException($"Failed to start P2P transport on port {port}");
        }

        _logger.LogInformation("P2P Transport started on port {Port}", _netManager.LocalPort);

        SetState(TransportState.Connected);

        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTask = PollLoopAsync(_pollCts.Token);

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_state == TransportState.Disconnected)
        {
            return;
        }

        SetState(TransportState.Disconnected);

        if (_pollCts != null)
        {
            await _pollCts.CancelAsync();
        }

        if (_pollTask != null)
        {
            try
            {
                await _pollTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Poll loop did not complete within timeout");
            }
        }

        _netManager.Stop();
        _peers.Clear();

        _logger.LogInformation("P2P Transport stopped");
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        // Broadcast to all peers
        foreach (var peer in _peers.Values)
        {
            peer.Send(data.Span, DeliveryMethod.ReliableOrdered);
        }
        return Task.CompletedTask;
    }

    public Task SendAsync(string peerId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Send(data.Span, DeliveryMethod.ReliableOrdered);
        }
        else
        {
            _logger.LogWarning("Cannot send to peer {PeerId}: not connected", peerId);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends data with a specific delivery method.
    /// </summary>
    public Task SendAsync(string peerId, ReadOnlyMemory<byte> data, DeliveryMethod deliveryMethod,
        CancellationToken cancellationToken = default)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Send(data.Span, deliveryMethod);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initiates NAT punch-through to connect to a peer.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="remoteEndPoint">The peer's public endpoint from ICE.</param>
    /// <param name="token">Shared token for hole punching coordination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> ConnectToPeerAsync(
        string peerId,
        IPEndPoint remoteEndPoint,
        string token,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating NAT punch to {PeerId} at {EndPoint}", peerId, remoteEndPoint);

        var tcs = new TaskCompletionSource<bool>();
        _pendingConnections[token] = tcs;

        try
        {
            // Send NAT punch request
            _natPunch.SendNatIntroduceRequest(remoteEndPoint, token);

            // Wait for connection with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.ConnectionTimeoutMs);

            await using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("NAT punch to {PeerId} timed out", peerId);
            return false;
        }
        finally
        {
            _pendingConnections.TryRemove(token, out _);
        }
    }

    /// <summary>
    /// Directly connects to a peer without NAT punching (for Open/FullCone NAT).
    /// </summary>
    public Task<bool> DirectConnectAsync(string peerId, IPEndPoint endPoint, string connectionKey,
        CancellationToken cancellationToken = default)
    {
        var peer = _netManager.Connect(endPoint, connectionKey);
        if (peer != null)
        {
            _peers[peerId] = peer;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets connection statistics for a peer.
    /// </summary>
    public PeerConnectionMetrics? GetPeerMetrics(string peerId)
    {
        if (!_peers.TryGetValue(peerId, out var peer))
        {
            return null;
        }

        return new PeerConnectionMetrics
        {
            RoundTripTime = TimeSpan.FromMilliseconds(peer.Ping),
            BytesSent = peer.Statistics.BytesSent,
            BytesReceived = peer.Statistics.BytesReceived,
            PacketLossRate = peer.Statistics.PacketLossPercent / 100.0,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }

    #region INatPunchListener

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        _logger.LogDebug("NAT Introduction Request: local={Local}, remote={Remote}, token={Token}",
            localEndPoint, remoteEndPoint, token);
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        _logger.LogInformation("NAT Punch Success! Target={Target}, Type={Type}, Token={Token}",
            targetEndPoint, type, token);

        // Connect to the peer
        var peer = _netManager.Connect(targetEndPoint, token);
        if (peer != null && _pendingConnections.TryGetValue(token, out var tcs))
        {
            _peers[token] = peer;
            tcs.TrySetResult(true);
        }

        NatPunchSuccess?.Invoke(this, new NatPunchSuccessEventArgs(targetEndPoint, type, token));
    }

    #endregion

    #region INetEventListener

    public void OnPeerConnected(NetPeer peer)
    {
        _logger.LogInformation("Peer connected: {Address}, Id={Id}", peer.Address, peer.Id);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        _logger.LogInformation("Peer disconnected: {Address}, Reason={Reason}",
            peer.Address, disconnectInfo.Reason);

        // Remove from peers dictionary
        var peerIdToRemove = _peers.FirstOrDefault(kvp => kvp.Value == peer).Key;
        if (peerIdToRemove != null)
        {
            _peers.TryRemove(peerIdToRemove, out _);
        }
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        _logger.LogError("Network error from {EndPoint}: {Error}", endPoint, socketError);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var data = new byte[reader.AvailableBytes];
        reader.GetBytes(data, data.Length);

        var peerId = _peers.FirstOrDefault(kvp => kvp.Value == peer).Key ?? peer.Address.ToString();

        DataReceived?.Invoke(this, new DataReceivedEventArgs(peerId, data, channelNumber));

        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        // Handle unconnected messages (e.g., discovery)
        _logger.LogDebug("Unconnected message from {EndPoint}, Type={Type}", remoteEndPoint, messageType);
        reader.Recycle();
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        _logger.LogTrace("Latency update for {Address}: {Latency}ms", peer.Address, latency);
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        // Accept all connections (authentication happens at application layer)
        _logger.LogDebug("Connection request from {Address}", request.RemoteEndPoint);
        request.Accept();
    }

    #endregion

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _netManager.PollEvents();
                _natPunch.PollEvents();
                await Task.Delay(15, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in P2P poll loop");
            SetState(TransportState.Failed, ex);
        }
    }

    private void SetState(TransportState newState, Exception? exception = null)
    {
        var previousState = _state;
        if (previousState == newState) return;

        _state = newState;
        StateChanged?.Invoke(this, new TransportStateChangedEventArgs(previousState, newState, exception));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _pollCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for NAT punch success.
/// </summary>
public class NatPunchSuccessEventArgs : EventArgs
{
    public IPEndPoint TargetEndPoint { get; }
    public NatAddressType AddressType { get; }
    public string Token { get; }

    public NatPunchSuccessEventArgs(IPEndPoint targetEndPoint, NatAddressType addressType, string token)
    {
        TargetEndPoint = targetEndPoint;
        AddressType = addressType;
        Token = token;
    }
}
