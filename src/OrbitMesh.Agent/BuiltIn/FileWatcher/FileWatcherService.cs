using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Agent.BuiltIn.FileWatcher;

/// <summary>
/// Event arguments for file change events.
/// </summary>
public sealed class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }

    /// <summary>
    /// Full path of the changed file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Name of the changed file.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Old path for rename operations.
    /// </summary>
    public string? OldPath { get; init; }

    /// <summary>
    /// Watch ID that detected this change.
    /// </summary>
    public required string WatchId { get; init; }
}

/// <summary>
/// Type of file change.
/// </summary>
public enum FileChangeType
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

/// <summary>
/// Configuration for a file watch.
/// </summary>
public sealed class FileWatchConfig
{
    /// <summary>
    /// Unique identifier for this watch.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Directory path to watch.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File filter pattern (e.g., "*.txt", "*.*").
    /// </summary>
    public string Filter { get; init; } = "*.*";

    /// <summary>
    /// Whether to watch subdirectories.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = true;

    /// <summary>
    /// Change types to watch for.
    /// </summary>
    public NotifyFilters NotifyFilter { get; init; } =
        NotifyFilters.FileName |
        NotifyFilters.DirectoryName |
        NotifyFilters.LastWrite |
        NotifyFilters.Size;

    /// <summary>
    /// Debounce delay in milliseconds to prevent duplicate events.
    /// </summary>
    public int DebounceMs { get; init; } = 500;
}

/// <summary>
/// Service for watching file system changes.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly ConcurrentDictionary<string, WatcherState> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when a file change is detected.
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Creates a new file watcher service.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts watching a directory for changes.
    /// </summary>
    /// <param name="config">Watch configuration.</param>
    /// <returns>True if watch started successfully.</returns>
    public bool StartWatch(FileWatchConfig config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_watchers.ContainsKey(config.Id))
        {
            _logger.LogWarning("Watch {WatchId} already exists", config.Id);
            return false;
        }

        if (!Directory.Exists(config.Path))
        {
            _logger.LogError("Directory does not exist: {Path}", config.Path);
            return false;
        }

        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(config.Path, config.Filter)
            {
                IncludeSubdirectories = config.IncludeSubdirectories,
                NotifyFilter = config.NotifyFilter,
                EnableRaisingEvents = false
            };

            var state = new WatcherState(config, watcher);

            watcher.Created += (s, e) => OnFileEvent(config.Id, FileChangeType.Created, e.FullPath, e.Name ?? "", null, config.DebounceMs);
            watcher.Changed += (s, e) => OnFileEvent(config.Id, FileChangeType.Modified, e.FullPath, e.Name ?? "", null, config.DebounceMs);
            watcher.Deleted += (s, e) => OnFileEvent(config.Id, FileChangeType.Deleted, e.FullPath, e.Name ?? "", null, config.DebounceMs);
            watcher.Renamed += (s, e) => OnFileEvent(config.Id, FileChangeType.Renamed, e.FullPath, e.Name ?? "", e.OldFullPath, config.DebounceMs);
            watcher.Error += (s, e) => OnError(config.Id, e.GetException());

            if (!_watchers.TryAdd(config.Id, state))
            {
                watcher.Dispose();
                watcher = null;
                return false;
            }

            watcher.EnableRaisingEvents = true;
            _logger.LogInformation("Started watching {Path} with ID {WatchId}", config.Path, config.Id);
            watcher = null; // Ownership transferred to _watchers
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watch for {Path}", config.Path);
            return false;
        }
        finally
        {
            watcher?.Dispose();
        }
    }

    /// <summary>
    /// Stops watching a directory.
    /// </summary>
    /// <param name="watchId">Watch identifier.</param>
    /// <returns>True if watch was stopped.</returns>
    public bool StopWatch(string watchId)
    {
        if (_watchers.TryRemove(watchId, out var state))
        {
            state.Watcher.EnableRaisingEvents = false;
            state.Watcher.Dispose();
            _logger.LogInformation("Stopped watch {WatchId}", watchId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all active watch IDs.
    /// </summary>
    /// <returns>Collection of watch IDs.</returns>
    public IEnumerable<string> GetActiveWatches()
    {
        return _watchers.Keys.ToList();
    }

    /// <summary>
    /// Gets the configuration for a watch.
    /// </summary>
    /// <param name="watchId">Watch identifier.</param>
    /// <returns>Watch configuration or null if not found.</returns>
    public FileWatchConfig? GetWatchConfig(string watchId)
    {
        return _watchers.TryGetValue(watchId, out var state) ? state.Config : null;
    }

    private void OnFileEvent(string watchId, FileChangeType changeType, string fullPath, string name, string? oldPath, int debounceMs)
    {
        // Debounce logic to prevent duplicate events
        var eventKey = $"{watchId}:{fullPath}:{changeType}";
        var now = DateTime.UtcNow;

        if (_lastEventTimes.TryGetValue(eventKey, out var lastTime))
        {
            if ((now - lastTime).TotalMilliseconds < debounceMs)
            {
                return;
            }
        }

        _lastEventTimes[eventKey] = now;

        // Clean up old event times periodically
        if (_lastEventTimes.Count > 1000)
        {
            var cutoff = now.AddMinutes(-1);
            foreach (var key in _lastEventTimes.Keys.ToList())
            {
                if (_lastEventTimes.TryGetValue(key, out var time) && time < cutoff)
                {
                    _lastEventTimes.TryRemove(key, out _);
                }
            }
        }

        var args = new FileChangedEventArgs
        {
            ChangeType = changeType,
            FullPath = fullPath,
            Name = name,
            OldPath = oldPath,
            WatchId = watchId
        };

        _logger.LogDebug("File {ChangeType}: {FullPath} (Watch: {WatchId})", changeType, fullPath, watchId);

        try
        {
            FileChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in file change handler for {FullPath}", fullPath);
        }
    }

    private void OnError(string watchId, Exception ex)
    {
        _logger.LogError(ex, "File watcher error for watch {WatchId}", watchId);

        // Try to restart the watcher
        if (_watchers.TryGetValue(watchId, out var state))
        {
            try
            {
                state.Watcher.EnableRaisingEvents = false;
                state.Watcher.EnableRaisingEvents = true;
                _logger.LogInformation("Restarted watcher {WatchId} after error", watchId);
            }
            catch (Exception restartEx)
            {
                _logger.LogError(restartEx, "Failed to restart watcher {WatchId}", watchId);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var state in _watchers.Values)
        {
            try
            {
                state.Watcher.EnableRaisingEvents = false;
                state.Watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing watcher");
            }
        }

        _watchers.Clear();
        _lastEventTimes.Clear();
    }

    private sealed record WatcherState(FileWatchConfig Config, FileSystemWatcher Watcher);
}
