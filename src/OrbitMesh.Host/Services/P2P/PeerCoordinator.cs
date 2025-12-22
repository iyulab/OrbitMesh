using System.Collections.Concurrent;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.FileTransfer.Protocol;
using OrbitMesh.Core.Transport.Models;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Services.P2P;

/// <summary>
/// Default implementation of peer coordinator for P2P connection management.
/// </summary>
public class PeerCoordinator : IPeerCoordinator
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly P2PServerOptions _options;
    private readonly ILogger<PeerCoordinator> _logger;

    private readonly ConcurrentDictionary<string, NatInfo> _natInfoCache = new();
    private readonly ConcurrentDictionary<string, bool> _p2pCapableAgents = new();
    private readonly ConcurrentDictionary<string, PeerConnectionState> _connectionStates = new();

    public PeerCoordinator(
        IAgentRegistry agentRegistry,
        IOptions<P2PServerOptions> options,
        ILogger<PeerCoordinator> logger)
    {
        _agentRegistry = agentRegistry;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ConnectionStrategy DetermineStrategy(NatInfo? initiatorNat, NatInfo? responderNat)
    {
        // If either NAT info is missing, default to UDP hole punching
        if (initiatorNat is null || responderNat is null)
        {
            _logger.LogDebug("NAT info missing, defaulting to UdpHolePunch strategy");
            return ConnectionStrategy.UdpHolePunch;
        }

        var strategy = DetermineStrategyFromNatTypes(initiatorNat.Type, responderNat.Type);

        _logger.LogDebug(
            "Determined connection strategy: {Strategy} for NAT types: {InitiatorNat} <-> {ResponderNat}",
            strategy,
            initiatorNat.Type,
            responderNat.Type);

        return strategy;
    }

    /// <summary>
    /// Determines connection strategy based on NAT type matrix.
    /// Research-based success rates:
    /// - FullCone ↔ FullCone: ~100%
    /// - FullCone ↔ Restricted: ~95%
    /// - Restricted ↔ Restricted: ~85%
    /// - PortRestricted ↔ PortRestricted: ~70%
    /// - Symmetric ↔ any: Requires TURN relay
    /// </summary>
    private static ConnectionStrategy DetermineStrategyFromNatTypes(NatType initiator, NatType responder)
    {
        // Symmetric NAT on either side requires TURN relay
        if (initiator == NatType.Symmetric || responder == NatType.Symmetric)
        {
            return ConnectionStrategy.TurnRelay;
        }

        // Open NAT can do direct connect
        if (initiator == NatType.Open || responder == NatType.Open)
        {
            return ConnectionStrategy.DirectConnect;
        }

        // FullCone NAT can do direct connect with the full cone side
        if (initiator == NatType.FullCone || responder == NatType.FullCone)
        {
            // If one side is FullCone and the other is not symmetric,
            // direct connect from the non-FullCone side should work
            return ConnectionStrategy.DirectConnect;
        }

        // Both are Restricted or PortRestricted - use simultaneous open
        if ((initiator == NatType.Restricted || initiator == NatType.PortRestricted) &&
            (responder == NatType.Restricted || responder == NatType.PortRestricted))
        {
            return ConnectionStrategy.SimultaneousOpen;
        }

        // Default to UDP hole punching
        return ConnectionStrategy.UdpHolePunch;
    }

    /// <inheritdoc />
    public NatInfo? GetCachedNatInfo(string agentId)
    {
        return _natInfoCache.TryGetValue(agentId, out var info) ? info : null;
    }

    /// <inheritdoc />
    public void CacheNatInfo(string agentId, NatInfo natInfo)
    {
        _natInfoCache[agentId] = natInfo;
        _logger.LogDebug(
            "Cached NAT info for agent {AgentId}: Type={NatType}, Public={PublicAddress}:{PublicPort}",
            agentId,
            natInfo.Type,
            natInfo.PublicAddress,
            natInfo.PublicPort);
    }

    /// <inheritdoc />
    public void RemoveCachedNatInfo(string agentId)
    {
        if (_natInfoCache.TryRemove(agentId, out _))
        {
            _logger.LogDebug("Removed cached NAT info for agent {AgentId}", agentId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAgentP2PCapableAsync(string agentId)
    {
        // First check our local cache
        if (_p2pCapableAgents.TryGetValue(agentId, out var capable))
        {
            return capable;
        }

        // Check if agent is registered and has P2P capability
        var agent = await _agentRegistry.GetAsync(agentId);
        if (agent is null)
        {
            return false;
        }

        // Check for P2P capability in agent capabilities
        var hasP2P = agent.Capabilities.Any(c =>
            string.Equals(c.Name, "p2p", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Name, "peer-to-peer", StringComparison.OrdinalIgnoreCase));

        return hasP2P;
    }

    /// <inheritdoc />
    public void RegisterP2PAgent(string agentId)
    {
        _p2pCapableAgents[agentId] = true;
        _logger.LogInformation("Agent {AgentId} registered as P2P capable", agentId);
    }

    /// <inheritdoc />
    public void UnregisterP2PAgent(string agentId)
    {
        _p2pCapableAgents.TryRemove(agentId, out _);
        RemoveCachedNatInfo(agentId);

        // Clean up any connection states involving this agent
        var keysToRemove = _connectionStates.Keys
            .Where(k => k.Contains(agentId, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _connectionStates.TryRemove(key, out _);
        }

        _logger.LogInformation("Agent {AgentId} unregistered from P2P", agentId);
    }

    /// <inheritdoc />
    public void RecordConnectionState(string fromAgentId, string toAgentId, PeerConnectionState state)
    {
        var key = GetConnectionKey(fromAgentId, toAgentId);
        _connectionStates[key] = state;

        if (_options.EnableMetrics)
        {
            _logger.LogInformation(
                "P2P connection state recorded: {FromAgent} -> {ToAgent}: {Status}, Transport: {Transport}",
                fromAgentId,
                toAgentId,
                state.Status,
                state.ActiveTransport);
        }
    }

    /// <inheritdoc />
    public PeerConnectionState? GetConnectionState(string fromAgentId, string toAgentId)
    {
        var key = GetConnectionKey(fromAgentId, toAgentId);
        return _connectionStates.TryGetValue(key, out var state) ? state : null;
    }

    private static string GetConnectionKey(string agentA, string agentB)
    {
        // Use consistent key ordering to ensure bidirectional lookup
        return string.CompareOrdinal(agentA, agentB) < 0
            ? $"{agentA}:{agentB}"
            : $"{agentB}:{agentA}";
    }

    /// <inheritdoc />
    public PeerInfo? GetPeerInfo(string agentId)
    {
        // Check if agent is P2P capable
        if (!_p2pCapableAgents.TryGetValue(agentId, out var capable) || !capable)
        {
            return null;
        }

        // Find any connection state involving this agent
        var connectionKey = _connectionStates.Keys
            .FirstOrDefault(k => k.Contains(agentId, StringComparison.Ordinal));

        var connectionState = connectionKey != null
            ? _connectionStates.GetValueOrDefault(connectionKey)
            : null;

        var isConnected = connectionState?.Status == PeerConnectionStatus.Connected;

        return new PeerInfo
        {
            AgentId = agentId,
            IsConnected = isConnected,
            NatInfo = GetCachedNatInfo(agentId),
            LastActivity = connectionState?.ConnectedAt
        };
    }

    /// <inheritdoc />
    public async Task SendFileChunkAsync(
        string agentId,
        P2PFileChunk chunk,
        CancellationToken cancellationToken = default)
    {
        var peerInfo = GetPeerInfo(agentId);
        if (peerInfo is null || !peerInfo.IsConnected)
        {
            throw new InvalidOperationException($"No P2P connection to agent {agentId}");
        }

        // Serialize chunk using MessagePack
        var data = MessagePackSerializer.Serialize(chunk, cancellationToken: cancellationToken);

        // The actual sending would be done through the P2P transport
        // For now, this is a placeholder that would integrate with LiteNetP2PTransport
        _logger.LogDebug(
            "Sending file chunk {ChunkIndex}/{Total} ({Size} bytes) to {AgentId}",
            chunk.ChunkIndex,
            chunk.IsLastChunk ? chunk.ChunkIndex + 1 : "?",
            chunk.Data.Length,
            agentId);

        // TODO: Integrate with actual P2P transport when available
        // await _p2pTransport.SendAsync(agentId, data, cancellationToken);

        await Task.CompletedTask;
    }
}
