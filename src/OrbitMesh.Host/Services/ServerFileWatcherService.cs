using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Server-side file watcher service implementation.
/// Monitors the file storage directory and raises events on file changes.
/// </summary>
public sealed class ServerFileWatcherService : IServerFileWatcherService
{
    private readonly string _storageRootPath;
    private readonly ILogger<ServerFileWatcherService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastEventTimes = new();
    private readonly int _debounceMs;

    private FileSystemWatcher? _watcher;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<ServerFileChangedEventArgs>? FileChanged;

    /// <inheritdoc />
    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    /// <inheritdoc />
    public string? WatchPath { get; private set; }

    /// <summary>
    /// Creates a new server file watcher service.
    /// </summary>
    /// <param name="storageRootPath">Root path of the file storage.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="debounceMs">Debounce delay in milliseconds.</param>
    public ServerFileWatcherService(
        string storageRootPath,
        ILogger<ServerFileWatcherService> logger,
        int debounceMs = 500)
    {
        _storageRootPath = Path.GetFullPath(storageRootPath);
        _logger = logger;
        _debounceMs = debounceMs;

        // Ensure directory exists
        Directory.CreateDirectory(_storageRootPath);
    }

    /// <inheritdoc />
    public bool StartWatch(string path, string filter = "*.*", bool includeSubdirectories = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Stop existing watch if any
        StopWatch();

        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Cannot start watch: Directory does not exist: {Path}", fullPath);
            return false;
        }

        try
        {
            _watcher = new FileSystemWatcher(fullPath)
            {
                Filter = filter,
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
            };

            _watcher.Created += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnError;

            _watcher.EnableRaisingEvents = true;
            WatchPath = path;

            _logger.LogInformation(
                "Server file watcher started. Path: {Path}, Filter: {Filter}, IncludeSubdirs: {IncludeSubdirs}",
                fullPath, filter, includeSubdirectories);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server file watcher for path: {Path}", fullPath);
            _watcher?.Dispose();
            _watcher = null;
            return false;
        }
    }

    /// <inheritdoc />
    public void StopWatch()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
            _watcher.Deleted -= OnFileEvent;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;

            _logger.LogInformation("Server file watcher stopped. Path: {Path}", WatchPath);
            WatchPath = null;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // Skip temporary files
        if (e.FullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Skip directories for Changed events
        if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
        {
            return;
        }

        // Debounce: skip if same file was changed recently
        var key = $"{e.FullPath}:{e.ChangeType}";
        var now = DateTimeOffset.UtcNow;

        if (_lastEventTimes.TryGetValue(key, out var lastTime))
        {
            if ((now - lastTime).TotalMilliseconds < _debounceMs)
            {
                return;
            }
        }
        _lastEventTimes[key] = now;

        // Clean up old entries
        var cutoff = now.AddMinutes(-1);
        foreach (var oldKey in _lastEventTimes.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList())
        {
            _lastEventTimes.TryRemove(oldKey, out _);
        }

        var changeType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => ServerFileChangeType.Created,
            WatcherChangeTypes.Changed => ServerFileChangeType.Modified,
            WatcherChangeTypes.Deleted => ServerFileChangeType.Deleted,
            _ => ServerFileChangeType.Modified
        };

        var relativePath = GetRelativePath(e.FullPath);

        _logger.LogDebug(
            "Server file change detected: {ChangeType} - {Path}",
            changeType, relativePath);

        RaiseFileChanged(new ServerFileChangedEventArgs
        {
            ChangeType = changeType,
            RelativePath = relativePath,
            FullPath = e.FullPath
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Skip temporary files
        if (e.FullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            e.OldFullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = GetRelativePath(e.FullPath);
        var oldRelativePath = GetRelativePath(e.OldFullPath);

        _logger.LogDebug(
            "Server file renamed: {OldPath} -> {NewPath}",
            oldRelativePath, relativePath);

        RaiseFileChanged(new ServerFileChangedEventArgs
        {
            ChangeType = ServerFileChangeType.Renamed,
            RelativePath = relativePath,
            FullPath = e.FullPath,
            OldRelativePath = oldRelativePath
        });
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex, "Server file watcher error");

        // Try to restart the watcher
        if (WatchPath != null && _watcher != null)
        {
            _logger.LogInformation("Attempting to restart server file watcher...");
            var path = WatchPath;
            var filter = _watcher.Filter;
            var includeSubdirs = _watcher.IncludeSubdirectories;

            StopWatch();

            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    StartWatch(path, filter, includeSubdirs);
                }
                catch (Exception restartEx)
                {
                    _logger.LogError(restartEx, "Failed to restart server file watcher");
                }
            }, TaskScheduler.Default);
        }
    }

    private void RaiseFileChanged(ServerFileChangedEventArgs args)
    {
        try
        {
            FileChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FileChanged event handler");
        }
    }

    private string GetFullPath(string relativePath)
    {
        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        if (string.IsNullOrEmpty(normalized) || normalized == ".")
        {
            return _storageRootPath;
        }

        return Path.GetFullPath(Path.Combine(_storageRootPath, normalized));
    }

    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_storageRootPath, fullPath).Replace('\\', '/');
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopWatch();
        _disposed = true;
    }
}
