namespace OrbitMesh.Host.Services;

/// <summary>
/// Service for synchronizing files between server and agents.
/// Handles bidirectional file sync with automatic propagation.
/// </summary>
public interface IFileSyncService
{
    /// <summary>
    /// Event raised when a sync operation completes.
    /// </summary>
    event EventHandler<FileSyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// Starts the file sync service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the file sync service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a sync from server to specific agents.
    /// </summary>
    /// <param name="agentPattern">Agent pattern to target (e.g., "*" for all, "node1" for specific).</param>
    /// <param name="filePath">Optional specific file path to sync. If null, syncs all files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileSyncResult> SyncToAgentsAsync(
        string agentPattern,
        string? filePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a sync from agent to server and then to other agents.
    /// Called when an agent uploads a file.
    /// </summary>
    /// <param name="sourceAgentId">ID of the agent that uploaded the file.</param>
    /// <param name="filePath">Path of the uploaded file.</param>
    /// <param name="changeType">Type of change (created, modified, deleted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileSyncResult> PropagateFromAgentAsync(
        string sourceAgentId,
        string filePath,
        string changeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    FileSyncStatus GetStatus();
}

/// <summary>
/// Result of a file sync operation.
/// </summary>
public sealed record FileSyncResult
{
    /// <summary>Whether the sync was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Number of files synced.</summary>
    public int FilesSynced { get; init; }

    /// <summary>Number of agents synced to.</summary>
    public int AgentsSynced { get; init; }

    /// <summary>Error message if sync failed.</summary>
    public string? Error { get; init; }

    /// <summary>Individual file results.</summary>
    public IReadOnlyList<FileSyncFileResult> Files { get; init; } = [];
}

/// <summary>
/// Result for a single file sync.
/// </summary>
public sealed record FileSyncFileResult
{
    /// <summary>File path.</summary>
    public required string Path { get; init; }

    /// <summary>Whether sync was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Current status of the file sync service.
/// </summary>
public sealed record FileSyncStatus
{
    /// <summary>Whether the service is running.</summary>
    public bool IsRunning { get; init; }

    /// <summary>Whether server file watching is active.</summary>
    public bool IsWatching { get; init; }

    /// <summary>Current watch path.</summary>
    public string? WatchPath { get; init; }

    /// <summary>Number of connected agents.</summary>
    public int ConnectedAgents { get; init; }

    /// <summary>Last sync timestamp.</summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>Total files synced since start.</summary>
    public long TotalFilesSynced { get; init; }
}

/// <summary>
/// Event arguments for sync completion.
/// </summary>
public sealed class FileSyncCompletedEventArgs : EventArgs
{
    /// <summary>The sync result.</summary>
    public required FileSyncResult Result { get; init; }

    /// <summary>Direction of the sync.</summary>
    public required FileSyncDirection Direction { get; init; }

    /// <summary>Source of the sync (server or agent ID).</summary>
    public required string Source { get; init; }

    /// <summary>Timestamp of completion.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Direction of file sync.
/// </summary>
public enum FileSyncDirection
{
    /// <summary>Server to agents.</summary>
    ServerToAgents,

    /// <summary>Agent to server.</summary>
    AgentToServer,

    /// <summary>Agent to server then to other agents.</summary>
    AgentToAll
}
