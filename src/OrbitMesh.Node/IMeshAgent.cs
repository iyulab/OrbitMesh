namespace OrbitMesh.Node;

/// <summary>
/// Represents an OrbitMesh agent connection.
/// </summary>
public interface IMeshAgent : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier of the agent.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the agent name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the agent is currently connected to the server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the OrbitMesh server.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the OrbitMesh server.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the agent to stop.
    /// </summary>
    Task WaitForShutdownAsync(CancellationToken cancellationToken = default);
}
