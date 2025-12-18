using System.Net;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Host.Services.P2P;

/// <summary>
/// Interface for STUN server functionality.
/// </summary>
public interface IStunServer
{
    /// <summary>
    /// Analyzes NAT type for a client based on their connection information.
    /// </summary>
    /// <param name="clientAddress">The client's IP address as seen by the server.</param>
    /// <param name="clientPort">The client's port as seen by the server.</param>
    /// <param name="localAddress">The client's local/private address if known.</param>
    /// <param name="localPort">The client's local port if known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>NAT information for the client.</returns>
    Task<NatInfo> AnalyzeNatAsync(
        IPAddress? clientAddress,
        int clientPort,
        string? localAddress = null,
        int? localPort = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server's public addresses that can be used for STUN.
    /// </summary>
    /// <returns>List of server addresses.</returns>
    IReadOnlyList<IPEndPoint> GetServerEndpoints();

    /// <summary>
    /// Checks if the STUN server is running.
    /// </summary>
    bool IsRunning { get; }
}
