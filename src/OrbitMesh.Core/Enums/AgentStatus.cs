namespace OrbitMesh.Core.Enums;

/// <summary>
/// Represents the lifecycle state of an agent in the mesh.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Agent has been created but not yet initialized.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Agent is initializing and establishing connection.
    /// </summary>
    Initializing = 1,

    /// <summary>
    /// Agent is connected and ready to receive work.
    /// </summary>
    Ready = 2,

    /// <summary>
    /// Agent is actively executing a job.
    /// </summary>
    Running = 3,

    /// <summary>
    /// Agent is temporarily paused but still connected.
    /// </summary>
    Paused = 4,

    /// <summary>
    /// Agent is in the process of stopping.
    /// </summary>
    Stopping = 5,

    /// <summary>
    /// Agent has been gracefully stopped.
    /// </summary>
    Stopped = 6,

    /// <summary>
    /// Agent encountered an error and is in a faulted state.
    /// </summary>
    Faulted = 7,

    /// <summary>
    /// Agent has disconnected from the server.
    /// </summary>
    Disconnected = 8
}
