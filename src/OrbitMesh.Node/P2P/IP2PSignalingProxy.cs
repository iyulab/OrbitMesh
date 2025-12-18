using Microsoft.AspNetCore.SignalR.Client;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Node.P2P;

/// <summary>
/// Proxy interface for P2P signaling operations via SignalR.
/// Wraps the HubConnection for P2P-specific method calls.
/// </summary>
public interface IP2PSignalingProxy : IP2PServerHub
{
    /// <summary>
    /// Gets whether the proxy is connected and ready.
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// SignalR-based implementation of P2P signaling proxy.
/// </summary>
public class SignalRP2PSignalingProxy : IP2PSignalingProxy
{
    private readonly HubConnection _connection;
    private readonly string _hubPath;

    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    public SignalRP2PSignalingProxy(HubConnection connection, string hubPath = "/p2p")
    {
        _connection = connection;
        _hubPath = hubPath;
    }

    /// <inheritdoc />
    public async Task<NatInfo> GetNatInfoAsync(CancellationToken cancellationToken = default)
    {
        return await _connection.InvokeAsync<NatInfo>(
            nameof(IP2PServerHub.GetNatInfoAsync),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendIceCandidateAsync(
        string toAgentId,
        IceCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        await _connection.InvokeAsync(
            nameof(IP2PServerHub.SendIceCandidateAsync),
            toAgentId,
            candidate,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task RequestPeerConnectionAsync(
        string toAgentId,
        PeerConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _connection.InvokeAsync(
            nameof(IP2PServerHub.RequestPeerConnectionAsync),
            toAgentId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task RespondPeerConnectionAsync(
        string toAgentId,
        PeerConnectionResponse response,
        CancellationToken cancellationToken = default)
    {
        await _connection.InvokeAsync(
            nameof(IP2PServerHub.RespondPeerConnectionAsync),
            toAgentId,
            response,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReportPeerConnectionStateAsync(
        string peerId,
        PeerConnectionState state,
        CancellationToken cancellationToken = default)
    {
        await _connection.InvokeAsync(
            nameof(IP2PServerHub.ReportPeerConnectionStateAsync),
            peerId,
            state,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task RelayDataAsync(
        string toAgentId,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        await _connection.InvokeAsync(
            nameof(IP2PServerHub.RelayDataAsync),
            toAgentId,
            data,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsAgentP2PCapableAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        return await _connection.InvokeAsync<bool>(
            nameof(IP2PServerHub.IsAgentP2PCapableAsync),
            agentId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConnectionStrategy> GetRecommendedStrategyAsync(
        string toAgentId,
        NatInfo localNatInfo,
        CancellationToken cancellationToken = default)
    {
        return await _connection.InvokeAsync<ConnectionStrategy>(
            nameof(IP2PServerHub.GetRecommendedStrategyAsync),
            toAgentId,
            localNatInfo,
            cancellationToken);
    }
}
