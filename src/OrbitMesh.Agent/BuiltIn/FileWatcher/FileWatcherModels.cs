using MessagePack;

namespace OrbitMesh.Agent.BuiltIn.FileWatcher;

/// <summary>
/// Request to start watching a directory.
/// </summary>
[MessagePackObject]
public sealed record StartFileWatchRequest
{
    /// <summary>
    /// Unique identifier for this watch.
    /// </summary>
    [Key(0)]
    public required string WatchId { get; init; }

    /// <summary>
    /// Directory path to watch.
    /// </summary>
    [Key(1)]
    public required string Path { get; init; }

    /// <summary>
    /// File filter pattern (e.g., "*.txt", "*.*").
    /// </summary>
    [Key(2)]
    public string Filter { get; init; } = "*.*";

    /// <summary>
    /// Whether to watch subdirectories.
    /// </summary>
    [Key(3)]
    public bool IncludeSubdirectories { get; init; } = true;

    /// <summary>
    /// Debounce delay in milliseconds.
    /// </summary>
    [Key(4)]
    public int DebounceMs { get; init; } = 500;
}

/// <summary>
/// Result of starting a file watch.
/// </summary>
[MessagePackObject]
public sealed record StartFileWatchResult
{
    /// <summary>
    /// Whether the watch started successfully.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Watch ID that was started.
    /// </summary>
    [Key(1)]
    public string? WatchId { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(2)]
    public string? Error { get; init; }
}

/// <summary>
/// Request to stop watching a directory.
/// </summary>
[MessagePackObject]
public sealed record StopFileWatchRequest
{
    /// <summary>
    /// Watch ID to stop.
    /// </summary>
    [Key(0)]
    public required string WatchId { get; init; }
}

/// <summary>
/// Result of stopping a file watch.
/// </summary>
[MessagePackObject]
public sealed record StopFileWatchResult
{
    /// <summary>
    /// Whether the watch was stopped.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(1)]
    public string? Error { get; init; }
}

/// <summary>
/// Request to list active watches.
/// </summary>
[MessagePackObject]
public sealed record ListFileWatchesRequest
{
}

/// <summary>
/// Result of listing active watches.
/// </summary>
[MessagePackObject]
public sealed record ListFileWatchesResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// List of active watch information.
    /// </summary>
    [Key(1)]
    public IReadOnlyList<FileWatchInfo>? Watches { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(2)]
    public string? Error { get; init; }
}

/// <summary>
/// Information about an active file watch.
/// </summary>
[MessagePackObject]
public sealed record FileWatchInfo
{
    /// <summary>
    /// Watch identifier.
    /// </summary>
    [Key(0)]
    public required string WatchId { get; init; }

    /// <summary>
    /// Directory path being watched.
    /// </summary>
    [Key(1)]
    public required string Path { get; init; }

    /// <summary>
    /// File filter pattern.
    /// </summary>
    [Key(2)]
    public required string Filter { get; init; }

    /// <summary>
    /// Whether subdirectories are included.
    /// </summary>
    [Key(3)]
    public bool IncludeSubdirectories { get; init; }
}

/// <summary>
/// Notification sent when a file changes.
/// </summary>
[MessagePackObject]
public sealed record FileChangeNotification
{
    /// <summary>
    /// Watch ID that detected the change.
    /// </summary>
    [Key(0)]
    public required string WatchId { get; init; }

    /// <summary>
    /// Type of change.
    /// </summary>
    [Key(1)]
    public required string ChangeType { get; init; }

    /// <summary>
    /// Full path of the changed file.
    /// </summary>
    [Key(2)]
    public required string FullPath { get; init; }

    /// <summary>
    /// File name.
    /// </summary>
    [Key(3)]
    public required string Name { get; init; }

    /// <summary>
    /// Old path for rename operations.
    /// </summary>
    [Key(4)]
    public string? OldPath { get; init; }

    /// <summary>
    /// Timestamp of the change.
    /// </summary>
    [Key(5)]
    public DateTimeOffset Timestamp { get; init; }
}
