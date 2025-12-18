namespace OrbitMesh.Core.Transport;

/// <summary>
/// Abstraction for transport layer communication.
/// Supports multiple transport types: SignalR (centralized), P2P (direct UDP), Relay (TURN).
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this transport instance.
    /// </summary>
    string TransportId { get; }

    /// <summary>
    /// Gets the type of this transport.
    /// </summary>
    TransportType Type { get; }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    TransportState State { get; }

    /// <summary>
    /// Raised when the transport state changes.
    /// </summary>
    event EventHandler<TransportStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Raised when data is received from a peer.
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Initiates connection to the transport layer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully disconnects from the transport layer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to the default destination (e.g., server for SignalR).
    /// </summary>
    /// <param name="data">Data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to a specific peer.
    /// </summary>
    /// <param name="peerId">Target peer identifier.</param>
    /// <param name="data">Data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(string peerId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}
