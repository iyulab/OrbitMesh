namespace OrbitMesh.Host.Services;

/// <summary>
/// Server-side file watcher service for monitoring file changes in the server storage.
/// Triggers synchronization to connected agents when files change.
/// </summary>
public interface IServerFileWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when a file is created, modified, deleted, or renamed.
    /// </summary>
    event EventHandler<ServerFileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Starts watching the specified path.
    /// </summary>
    /// <param name="path">Path to watch (relative to storage root).</param>
    /// <param name="filter">File filter pattern (e.g., "*.*").</param>
    /// <param name="includeSubdirectories">Whether to watch subdirectories.</param>
    /// <returns>True if watch started successfully.</returns>
    bool StartWatch(string path, string filter = "*.*", bool includeSubdirectories = true);

    /// <summary>
    /// Stops watching.
    /// </summary>
    void StopWatch();

    /// <summary>
    /// Gets whether the watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the current watch path.
    /// </summary>
    string? WatchPath { get; }
}

/// <summary>
/// Event arguments for server file change events.
/// </summary>
public sealed class ServerFileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Type of change that occurred.
    /// </summary>
    public required ServerFileChangeType ChangeType { get; init; }

    /// <summary>
    /// Relative path of the changed file.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Full path of the changed file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Old path for rename operations.
    /// </summary>
    public string? OldRelativePath { get; init; }

    /// <summary>
    /// Timestamp when the change was detected.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of file change on server.
/// </summary>
public enum ServerFileChangeType
{
    /// <summary>File was created.</summary>
    Created,

    /// <summary>File was modified.</summary>
    Modified,

    /// <summary>File was deleted.</summary>
    Deleted,

    /// <summary>File was renamed.</summary>
    Renamed
}
