using System.Diagnostics.CodeAnalysis;

namespace OrbitMesh.Client;

/// <summary>
/// Configuration options for the OrbitMesh client.
/// </summary>
public sealed class OrbitMeshClientOptions
{
    /// <summary>
    /// The URI of the OrbitMesh server.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI properties should not be strings",
        Justification = "String URI provides simpler API for SDK consumers")]
    public string? ServerUri { get; set; }

    /// <summary>
    /// The path to the SignalR hub (default: /agent).
    /// </summary>
    public string HubPath { get; set; } = "/agent";

    /// <summary>
    /// Default timeout for operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to automatically reconnect when disconnected.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnection attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// Delay between reconnection attempts.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Access token provider for authentication.
    /// </summary>
    public Func<Task<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Additional headers to include in the connection.
    /// </summary>
    public IDictionary<string, string>? Headers { get; init; }
}
