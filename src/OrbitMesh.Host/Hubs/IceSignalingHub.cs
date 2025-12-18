using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Transport.Models;
using OrbitMesh.Host.Services;
using OrbitMesh.Host.Services.P2P;

namespace OrbitMesh.Host.Hubs;

/// <summary>
/// SignalR hub for ICE signaling and P2P connection coordination.
/// Provides NAT detection, ICE candidate exchange, and peer connection management.
/// </summary>
public class IceSignalingHub : Hub<IP2PAgentClient>, IP2PServerHub
{
    private readonly IPeerCoordinator _peerCoordinator;
    private readonly IStunServer _stunServer;
    private readonly IAgentRegistry _agentRegistry;
    private readonly P2PServerOptions _options;
    private readonly ILogger<IceSignalingHub> _logger;

    /// <summary>
    /// SignalR group for all P2P-capable agents.
    /// </summary>
    public const string P2PAgentsGroup = "p2p-agents";

    public IceSignalingHub(
        IPeerCoordinator peerCoordinator,
        IStunServer stunServer,
        IAgentRegistry agentRegistry,
        IOptions<P2PServerOptions> options,
        ILogger<IceSignalingHub> logger)
    {
        _peerCoordinator = peerCoordinator;
        _stunServer = stunServer;
        _agentRegistry = agentRegistry;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var agentId = GetAgentId();
        if (agentId is not null)
        {
            _peerCoordinator.RegisterP2PAgent(agentId);
            await Groups.AddToGroupAsync(Context.ConnectionId, P2PAgentsGroup);

            _logger.LogInformation(
                "P2P agent connected. AgentId: {AgentId}, ConnectionId: {ConnectionId}",
                agentId,
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = GetAgentId();
        if (agentId is not null)
        {
            _peerCoordinator.UnregisterP2PAgent(agentId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, P2PAgentsGroup);

            _logger.LogInformation(
                "P2P agent disconnected. AgentId: {AgentId}, ConnectionId: {ConnectionId}, Reason: {Reason}",
                agentId,
                Context.ConnectionId,
                exception?.Message ?? "Normal disconnect");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <inheritdoc />
    public async Task<NatInfo> GetNatInfoAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = Context.GetHttpContext();
        var remoteAddress = httpContext?.Connection.RemoteIpAddress;
        var remotePort = httpContext?.Connection.RemotePort ?? 0;

        // Get local address info from query string if provided
        var localAddress = httpContext?.Request.Query["localAddress"].FirstOrDefault();
        var localPortStr = httpContext?.Request.Query["localPort"].FirstOrDefault();
        int? localPort = int.TryParse(localPortStr, out var lp) ? lp : null;

        var natInfo = await _stunServer.AnalyzeNatAsync(
            remoteAddress,
            remotePort,
            localAddress,
            localPort,
            cancellationToken);

        // Cache the NAT info for this agent
        var agentId = GetAgentId();
        if (agentId is not null)
        {
            _peerCoordinator.CacheNatInfo(agentId, natInfo);
        }

        _logger.LogDebug(
            "NAT info retrieved for {AgentId}: Type={NatType}, Public={PublicAddress}:{PublicPort}",
            agentId ?? "unknown",
            natInfo.Type,
            natInfo.PublicAddress,
            natInfo.PublicPort);

        return natInfo;
    }

    /// <inheritdoc />
    public async Task SendIceCandidateAsync(
        string toAgentId,
        IceCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var fromAgentId = GetAgentId();
        if (fromAgentId is null)
        {
            _logger.LogWarning("ICE candidate send failed: Agent not identified");
            return;
        }

        _logger.LogDebug(
            "Relaying ICE candidate: {FromAgent} -> {ToAgent}, Type={CandidateType}",
            fromAgentId,
            toAgentId,
            candidate.Type);

        // Get the target agent's connection
        var targetAgent = await _agentRegistry.GetAsync(toAgentId);
        if (targetAgent?.ConnectionId is null)
        {
            _logger.LogWarning(
                "ICE candidate relay failed: Target agent {AgentId} not connected",
                toAgentId);
            return;
        }

        await Clients.Client(targetAgent.ConnectionId)
            .ReceiveIceCandidateAsync(fromAgentId, candidate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RequestPeerConnectionAsync(
        string toAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var fromAgentId = GetAgentId();
        if (fromAgentId is null)
        {
            _logger.LogWarning("Peer connection request failed: Agent not identified");
            return;
        }

        _logger.LogInformation(
            "P2P connection request: {FromAgent} -> {ToAgent}",
            fromAgentId,
            toAgentId);

        // Get remote agent's NAT info for strategy determination
        var remoteNatInfo = _peerCoordinator.GetCachedNatInfo(toAgentId);

        // Determine recommended strategy
        var strategy = _peerCoordinator.DetermineStrategy(request.NatInfo, remoteNatInfo);

        // Add recommended strategy to request
        var enrichedRequest = request with { RecommendedStrategy = strategy };

        // Get the target agent's connection
        var targetAgent = await _agentRegistry.GetAsync(toAgentId);
        if (targetAgent?.ConnectionId is null)
        {
            _logger.LogWarning(
                "Peer connection request failed: Target agent {AgentId} not connected",
                toAgentId);
            return;
        }

        await Clients.Client(targetAgent.ConnectionId)
            .ReceivePeerConnectionRequestAsync(fromAgentId, enrichedRequest, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RespondPeerConnectionAsync(
        string toAgentId,
        PeerConnectionResponse response,
        CancellationToken cancellationToken = default)
    {
        var fromAgentId = GetAgentId();
        if (fromAgentId is null)
        {
            _logger.LogWarning("Peer connection response failed: Agent not identified");
            return;
        }

        _logger.LogInformation(
            "P2P connection response: {FromAgent} -> {ToAgent}, Accepted={Accepted}",
            fromAgentId,
            toAgentId,
            response.Accepted);

        // Get the target agent's connection
        var targetAgent = await _agentRegistry.GetAsync(toAgentId);
        if (targetAgent?.ConnectionId is null)
        {
            _logger.LogWarning(
                "Peer connection response failed: Target agent {AgentId} not connected",
                toAgentId);
            return;
        }

        await Clients.Client(targetAgent.ConnectionId)
            .ReceivePeerConnectionResponseAsync(fromAgentId, response, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReportPeerConnectionStateAsync(
        string peerId,
        PeerConnectionState state,
        CancellationToken cancellationToken = default)
    {
        var agentId = GetAgentId();
        if (agentId is null)
        {
            _logger.LogWarning("Connection state report failed: Agent not identified");
            return Task.CompletedTask;
        }

        _peerCoordinator.RecordConnectionState(agentId, peerId, state);

        _logger.LogDebug(
            "P2P connection state reported: {FromAgent} <-> {ToAgent}: {Status}",
            agentId,
            peerId,
            state.Status);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RelayDataAsync(
        string toAgentId,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (!_options.AllowRelayFallback)
        {
            _logger.LogWarning("Data relay rejected: Relay fallback is disabled");
            return;
        }

        var fromAgentId = GetAgentId();
        if (fromAgentId is null)
        {
            _logger.LogWarning("Data relay failed: Agent not identified");
            return;
        }

        // Get the target agent's connection
        var targetAgent = await _agentRegistry.GetAsync(toAgentId);
        if (targetAgent?.ConnectionId is null)
        {
            _logger.LogWarning(
                "Data relay failed: Target agent {AgentId} not connected",
                toAgentId);
            return;
        }

        _logger.LogDebug(
            "Relaying data: {FromAgent} -> {ToAgent}, Size={Size} bytes",
            fromAgentId,
            toAgentId,
            data.Length);

        await Clients.Client(targetAgent.ConnectionId)
            .ReceiveRelayedDataAsync(fromAgentId, data, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsAgentP2PCapableAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        return await _peerCoordinator.IsAgentP2PCapableAsync(agentId);
    }

    /// <inheritdoc />
    public async Task<ConnectionStrategy> GetRecommendedStrategyAsync(
        string toAgentId,
        NatInfo localNatInfo,
        CancellationToken cancellationToken = default)
    {
        var remoteNatInfo = _peerCoordinator.GetCachedNatInfo(toAgentId);

        if (remoteNatInfo is null)
        {
            // Try to get fresh NAT info if agent is connected
            var targetAgent = await _agentRegistry.GetAsync(toAgentId);
            if (targetAgent is null)
            {
                _logger.LogDebug("Cannot determine strategy: Agent {AgentId} not found", toAgentId);
                return ConnectionStrategy.UdpHolePunch;
            }
        }

        return _peerCoordinator.DetermineStrategy(localNatInfo, remoteNatInfo);
    }

    private string? GetAgentId()
    {
        return Context.Items["AgentId"] as string;
    }
}
