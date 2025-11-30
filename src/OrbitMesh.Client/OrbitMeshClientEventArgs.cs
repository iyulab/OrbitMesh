using Microsoft.AspNetCore.SignalR.Client;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Client;

/// <summary>
/// Event arguments for connection state changes.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new connection state.
    /// </summary>
    public HubConnectionState State { get; }

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public ConnectionStateChangedEventArgs(HubConnectionState state)
    {
        State = state;
    }
}

/// <summary>
/// Event arguments for job result events.
/// </summary>
public sealed class JobResultEventArgs : EventArgs
{
    /// <summary>
    /// Gets the job result.
    /// </summary>
    public JobResult Result { get; }

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public JobResultEventArgs(JobResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Event arguments for job progress events.
/// </summary>
public sealed class JobProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the job progress.
    /// </summary>
    public JobProgress Progress { get; }

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public JobProgressEventArgs(JobProgress progress)
    {
        Progress = progress;
    }
}
