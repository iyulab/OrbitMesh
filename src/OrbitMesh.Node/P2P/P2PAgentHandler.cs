using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Transport;
using OrbitMesh.Core.Transport.Models;
using OrbitMesh.Transport.P2P;

namespace OrbitMesh.Node.P2P;

/// <summary>
/// Handles P2P transport operations for an agent.
/// This class integrates with the SignalR connection to enable P2P capabilities.
/// </summary>
public sealed class P2PAgentHandler : IAsyncDisposable
{
    private readonly LiteNetP2PTransport _transport;
    private readonly PeerConnectionManager _connectionManager;
    private readonly IceGatherer _iceGatherer;
    private readonly P2POptions _options;
    private readonly ILogger<P2PAgentHandler> _logger;

    private HubConnection? _p2pConnection;
    private SignalRP2PSignalingProxy? _signalingProxy;
    private NatInfo? _localNatInfo;
    private bool _disposed;

    /// <summary>
    /// Gets the agent ID.
    /// </summary>
    public string AgentId { get; }

    /// <summary>
    /// Gets the P2P transport for direct peer communication.
    /// </summary>
    public ITransport Transport => _transport;

    /// <summary>
    /// Gets the peer connection manager.
    /// </summary>
    public PeerConnectionManager ConnectionManager => _connectionManager;

    /// <summary>
    /// Gets the local NAT information once detected.
    /// </summary>
    public NatInfo? LocalNatInfo => _localNatInfo;

    /// <summary>
    /// Gets whether P2P is initialized and ready.
    /// </summary>
    public bool IsReady => _transport.State == TransportState.Connected && _localNatInfo != null;

    /// <summary>
    /// Event raised when data is received from a peer.
    /// </summary>
    public event EventHandler<P2PDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when a peer connection state changes.
    /// </summary>
    public event EventHandler<PeerConnectionStateChangedEventArgs>? PeerStateChanged;

    public P2PAgentHandler(
        string agentId,
        P2POptions options,
        ILoggerFactory loggerFactory)
    {
        AgentId = agentId;
        _options = options;
        _logger = loggerFactory.CreateLogger<P2PAgentHandler>();

        var transportLogger = loggerFactory.CreateLogger<LiteNetP2PTransport>();
        _transport = new LiteNetP2PTransport(agentId, Options.Create(_options), transportLogger);

        var gathererLogger = loggerFactory.CreateLogger<IceGatherer>();
        _iceGatherer = new IceGatherer(
            Options.Create(_options),
            gathererLogger);

        var connectionManagerLogger = loggerFactory.CreateLogger<PeerConnectionManager>();
        _connectionManager = new PeerConnectionManager(
            _transport,
            _iceGatherer,
            Options.Create(options),
            connectionManagerLogger);

        // Subscribe to transport events
        _transport.DataReceived += OnTransportDataReceived;
    }

    /// <summary>
    /// Initializes P2P with a dedicated SignalR connection.
    /// </summary>
    /// <param name="serverUrl">The server URL.</param>
    /// <param name="accessTokenProvider">Token provider for authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(
        string serverUrl,
        Func<Task<string?>>? accessTokenProvider = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing P2P handler for agent {AgentId}", AgentId);

        // Create P2P signaling connection
        var p2pUrl = CombineUrlPath(serverUrl, "/p2p");

        var connectionBuilder = new HubConnectionBuilder()
            .WithUrl(p2pUrl, options =>
            {
                if (accessTokenProvider != null)
                {
                    options.AccessTokenProvider = accessTokenProvider;
                }
            })
            .WithAutomaticReconnect()
            .AddMessagePackProtocol();

        _p2pConnection = connectionBuilder.Build();
        _signalingProxy = new SignalRP2PSignalingProxy(_p2pConnection);

        // Configure P2P signaling callbacks
        ConfigureP2PCallbacks(_p2pConnection);

        // Set signaling proxy on connection manager
        _connectionManager.SetSignalingHub(_signalingProxy);

        // Start P2P connection
        await _p2pConnection.StartAsync(cancellationToken);

        // Start local transport
        await _transport.ConnectAsync(cancellationToken);

        // Get NAT info from server
        _localNatInfo = await _signalingProxy.GetNatInfoAsync(cancellationToken);
        _connectionManager.SetLocalNatInfo(_localNatInfo);

        _logger.LogInformation(
            "P2P initialized. NAT Type: {NatType}, Public: {PublicAddress}:{PublicPort}",
            _localNatInfo.Type,
            _localNatInfo.PublicAddress,
            _localNatInfo.PublicPort);
    }

    /// <summary>
    /// Initializes P2P using an existing signaling connection.
    /// </summary>
    /// <param name="connection">The existing HubConnection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeWithConnectionAsync(
        HubConnection connection,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing P2P handler with existing connection for agent {AgentId}", AgentId);

        _p2pConnection = connection;
        _signalingProxy = new SignalRP2PSignalingProxy(connection);

        // Configure P2P signaling callbacks
        ConfigureP2PCallbacks(connection);

        // Set signaling proxy on connection manager
        _connectionManager.SetSignalingHub(_signalingProxy);

        // Start local transport
        await _transport.ConnectAsync(cancellationToken);

        // Get NAT info from server
        _localNatInfo = await _signalingProxy.GetNatInfoAsync(cancellationToken);
        _connectionManager.SetLocalNatInfo(_localNatInfo);

        _logger.LogInformation(
            "P2P initialized with existing connection. NAT Type: {NatType}, Public: {PublicAddress}:{PublicPort}",
            _localNatInfo.Type,
            _localNatInfo.PublicAddress,
            _localNatInfo.PublicPort);
    }

    private void ConfigureP2PCallbacks(HubConnection connection)
    {
        // ICE candidate reception
        connection.On<string, IceCandidate, CancellationToken>(
            nameof(IP2PAgentClient.ReceiveIceCandidateAsync),
            HandleReceiveIceCandidateAsync);

        // Peer connection request
        connection.On<string, PeerConnectionRequest, CancellationToken>(
            nameof(IP2PAgentClient.ReceivePeerConnectionRequestAsync),
            HandleReceivePeerConnectionRequestAsync);

        // Peer connection response
        connection.On<string, PeerConnectionResponse, CancellationToken>(
            nameof(IP2PAgentClient.ReceivePeerConnectionResponseAsync),
            HandleReceivePeerConnectionResponseAsync);

        // Peer connection state change
        connection.On<string, PeerConnectionState, CancellationToken>(
            nameof(IP2PAgentClient.ReceivePeerConnectionStateChangedAsync),
            HandleReceivePeerConnectionStateChangedAsync);

        // Relayed data (fallback)
        connection.On<string, byte[], CancellationToken>(
            nameof(IP2PAgentClient.ReceiveRelayedDataAsync),
            HandleReceiveRelayedDataAsync);
    }

    private Task HandleReceiveIceCandidateAsync(
        string fromAgentId,
        IceCandidate candidate,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received ICE candidate from {AgentId}: {CandidateType}",
            fromAgentId, candidate.Type);

        // ICE candidates are typically processed by the ice gatherer
        // during connectivity checks
        return Task.CompletedTask;
    }

    private async Task HandleReceivePeerConnectionRequestAsync(
        string fromAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received P2P connection request from {AgentId}", fromAgentId);

        await _connectionManager.HandleConnectionRequestAsync(fromAgentId, request, cancellationToken);
    }

    private Task HandleReceivePeerConnectionResponseAsync(
        string fromAgentId,
        PeerConnectionResponse response,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received P2P connection response from {AgentId}", fromAgentId);

        _connectionManager.HandleConnectionResponse(fromAgentId, response);
        return Task.CompletedTask;
    }

    private Task HandleReceivePeerConnectionStateChangedAsync(
        string peerId,
        PeerConnectionState state,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Peer {PeerId} connection state changed to {Status}", peerId, state.Status);

        PeerStateChanged?.Invoke(this, new PeerConnectionStateChangedEventArgs(peerId, state));
        return Task.CompletedTask;
    }

    private Task HandleReceiveRelayedDataAsync(
        string fromAgentId,
        byte[] data,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received {Size} bytes relayed from {AgentId}", data.Length, fromAgentId);

        DataReceived?.Invoke(this, new P2PDataReceivedEventArgs(fromAgentId, data, isRelayed: true));
        return Task.CompletedTask;
    }

    private void OnTransportDataReceived(object? sender, DataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, new P2PDataReceivedEventArgs(e.Source, e.Data.ToArray(), isRelayed: false));
    }

    /// <summary>
    /// Connects to a peer using P2P.
    /// </summary>
    /// <param name="peerId">The target peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection state.</returns>
    public async Task<PeerConnectionState> ConnectToPeerAsync(
        string peerId,
        CancellationToken cancellationToken = default)
    {
        return await _connectionManager.ConnectToPeerAsync(peerId, cancellationToken);
    }

    /// <summary>
    /// Sends data to a peer, preferring P2P but falling back to relay if needed.
    /// </summary>
    /// <param name="peerId">The target peer ID.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(
        string peerId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        var connectionState = _connectionManager.GetConnectionState(peerId);

        if (connectionState?.Status == PeerConnectionStatus.Connected &&
            connectionState.ActiveTransport == TransportType.P2P)
        {
            // Use P2P transport
            await _transport.SendAsync(peerId, data, cancellationToken);
        }
        else if (_options.FallbackToRelay && _signalingProxy != null)
        {
            // Fall back to server relay
            await _signalingProxy.RelayDataAsync(peerId, data.ToArray(), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot send to peer {peerId}: not connected and relay fallback is disabled");
        }
    }

    /// <summary>
    /// Checks if a peer supports P2P connections.
    /// </summary>
    public async Task<bool> IsP2PCapableAsync(string peerId, CancellationToken cancellationToken = default)
    {
        if (_signalingProxy == null)
        {
            return false;
        }

        return await _signalingProxy.IsAgentP2PCapableAsync(peerId, cancellationToken);
    }

    private static string CombineUrlPath(string baseUrl, string path)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + path);
        return uri.ToString();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _transport.DataReceived -= OnTransportDataReceived;

        await _transport.DisposeAsync();

        if (_p2pConnection != null)
        {
            await _p2pConnection.DisposeAsync();
        }
    }
}

/// <summary>
/// Event args for P2P data received events.
/// </summary>
public sealed class P2PDataReceivedEventArgs : EventArgs
{
    public string FromAgentId { get; }
    public byte[] Data { get; }
    public bool IsRelayed { get; }

    public P2PDataReceivedEventArgs(string fromAgentId, byte[] data, bool isRelayed)
    {
        FromAgentId = fromAgentId;
        Data = data;
        IsRelayed = isRelayed;
    }
}

/// <summary>
/// Event args for peer connection state changed events.
/// </summary>
public sealed class PeerConnectionStateChangedEventArgs : EventArgs
{
    public string PeerId { get; }
    public PeerConnectionState State { get; }

    public PeerConnectionStateChangedEventArgs(string peerId, PeerConnectionState state)
    {
        PeerId = peerId;
        State = state;
    }
}
