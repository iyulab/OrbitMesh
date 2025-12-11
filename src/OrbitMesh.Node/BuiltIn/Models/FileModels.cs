using MessagePack;

namespace OrbitMesh.Node.BuiltIn.Models;

/// <summary>
/// Request to download a file from server.
/// </summary>
[MessagePackObject]
public sealed record FileDownloadRequest
{
    /// <summary>
    /// Server-side file path or URL.
    /// </summary>
    [Key(0)]
    public required string SourcePath { get; init; }

    /// <summary>
    /// Local destination path on agent.
    /// </summary>
    [Key(1)]
    public required string DestinationPath { get; init; }

    /// <summary>
    /// Expected SHA256 checksum for verification.
    /// </summary>
    [Key(2)]
    public string? Checksum { get; init; }

    /// <summary>
    /// Whether to overwrite existing file.
    /// </summary>
    [Key(3)]
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// Whether to create parent directories if they don't exist.
    /// </summary>
    [Key(4)]
    public bool CreateDirectories { get; init; } = true;
}

/// <summary>
/// Result of a file download operation.
/// </summary>
[MessagePackObject]
public sealed record FileDownloadResult
{
    /// <summary>
    /// Whether the download was successful.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Local path where file was saved.
    /// </summary>
    [Key(1)]
    public string? LocalPath { get; init; }

    /// <summary>
    /// Size of the downloaded file in bytes.
    /// </summary>
    [Key(2)]
    public long Size { get; init; }

    /// <summary>
    /// SHA256 checksum of the downloaded file.
    /// </summary>
    [Key(3)]
    public string? Checksum { get; init; }

    /// <summary>
    /// Whether checksum verification passed.
    /// </summary>
    [Key(4)]
    public bool ChecksumVerified { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(5)]
    public string? Error { get; init; }
}

/// <summary>
/// Request to delete a file.
/// </summary>
[MessagePackObject]
public sealed record FileDeleteRequest
{
    /// <summary>
    /// Path to the file to delete.
    /// </summary>
    [Key(0)]
    public required string Path { get; init; }

    /// <summary>
    /// Whether to delete recursively (for directories).
    /// </summary>
    [Key(1)]
    public bool Recursive { get; init; }
}

/// <summary>
/// Result of file delete operation.
/// </summary>
[MessagePackObject]
public sealed record FileDeleteResult
{
    /// <summary>
    /// Whether the delete was successful.
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
/// Request to list files in a directory.
/// </summary>
[MessagePackObject]
public sealed record FileListRequest
{
    /// <summary>
    /// Directory path to list.
    /// </summary>
    [Key(0)]
    public required string Path { get; init; }

    /// <summary>
    /// Search pattern (e.g., "*.txt").
    /// </summary>
    [Key(1)]
    public string Pattern { get; init; } = "*";

    /// <summary>
    /// Whether to search recursively.
    /// </summary>
    [Key(2)]
    public bool Recursive { get; init; }

    /// <summary>
    /// Whether to include directories in results.
    /// </summary>
    [Key(3)]
    public bool IncludeDirectories { get; init; } = true;
}

/// <summary>
/// Result of file list operation.
/// </summary>
[MessagePackObject]
public sealed record FileListResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// List of file entries.
    /// </summary>
    [Key(1)]
    public IReadOnlyList<FileEntry>? Entries { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(2)]
    public string? Error { get; init; }
}

/// <summary>
/// File entry information.
/// </summary>
[MessagePackObject]
public sealed record FileEntry
{
    /// <summary>
    /// File or directory name.
    /// </summary>
    [Key(0)]
    public required string Name { get; init; }

    /// <summary>
    /// Full path.
    /// </summary>
    [Key(1)]
    public required string Path { get; init; }

    /// <summary>
    /// Whether this is a directory.
    /// </summary>
    [Key(2)]
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (0 for directories).
    /// </summary>
    [Key(3)]
    public long Size { get; init; }

    /// <summary>
    /// Last modified time.
    /// </summary>
    [Key(4)]
    public DateTimeOffset ModifiedAt { get; init; }

    /// <summary>
    /// Creation time.
    /// </summary>
    [Key(5)]
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Request to get file information.
/// </summary>
[MessagePackObject]
public sealed record FileInfoRequest
{
    /// <summary>
    /// Path to the file.
    /// </summary>
    [Key(0)]
    public required string Path { get; init; }

    /// <summary>
    /// Whether to compute checksum.
    /// </summary>
    [Key(1)]
    public bool ComputeChecksum { get; init; }
}

/// <summary>
/// File information result.
/// </summary>
[MessagePackObject]
public sealed record FileInfoResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Whether the file exists.
    /// </summary>
    [Key(1)]
    public bool Exists { get; init; }

    /// <summary>
    /// File entry details.
    /// </summary>
    [Key(2)]
    public FileEntry? Entry { get; init; }

    /// <summary>
    /// SHA256 checksum (if requested).
    /// </summary>
    [Key(3)]
    public string? Checksum { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(4)]
    public string? Error { get; init; }
}

/// <summary>
/// Request to upload a file from agent to server.
/// </summary>
[MessagePackObject]
public sealed record FileUploadRequest
{
    /// <summary>
    /// Local file path on agent to upload.
    /// </summary>
    [Key(0)]
    public required string SourcePath { get; init; }

    /// <summary>
    /// Server-side destination path or upload endpoint URL.
    /// </summary>
    [Key(1)]
#pragma warning disable CA1056 // URI properties should not be strings (MessagePack requires string)
    public required string DestinationUrl { get; init; }
#pragma warning restore CA1056

    /// <summary>
    /// Whether to compute and send checksum for verification.
    /// </summary>
    [Key(2)]
    public bool IncludeChecksum { get; init; } = true;

    /// <summary>
    /// Whether to overwrite existing file on server.
    /// </summary>
    [Key(3)]
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// Optional metadata to include with upload.
    /// </summary>
    [Key(4)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of file upload operation.
/// </summary>
[MessagePackObject]
public sealed record FileUploadResult
{
    /// <summary>
    /// Whether the upload was successful.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Server-side path where file was stored.
    /// </summary>
    [Key(1)]
    public string? ServerPath { get; init; }

    /// <summary>
    /// Size of the uploaded file in bytes.
    /// </summary>
    [Key(2)]
    public long Size { get; init; }

    /// <summary>
    /// SHA256 checksum of the uploaded file.
    /// </summary>
    [Key(3)]
    public string? Checksum { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(4)]
    public string? Error { get; init; }
}

/// <summary>
/// Request to sync files from server to agent.
/// </summary>
[MessagePackObject]
public sealed record FileSyncRequest
{
    /// <summary>
    /// Server-side source directory or manifest URL.
    /// </summary>
    [Key(0)]
    public required string Source { get; init; }

    /// <summary>
    /// Local destination directory.
    /// </summary>
    [Key(1)]
    public required string Destination { get; init; }

    /// <summary>
    /// Whether to delete files not present on server.
    /// </summary>
    [Key(2)]
    public bool DeleteOrphans { get; init; }

    /// <summary>
    /// File patterns to include.
    /// </summary>
    [Key(3)]
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>
    /// File patterns to exclude.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<string>? ExcludePatterns { get; init; }
}

/// <summary>
/// Result of file sync operation.
/// </summary>
[MessagePackObject]
public sealed record FileSyncResult
{
    /// <summary>
    /// Whether the sync was successful.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Number of files downloaded.
    /// </summary>
    [Key(1)]
    public int FilesDownloaded { get; init; }

    /// <summary>
    /// Number of files deleted.
    /// </summary>
    [Key(2)]
    public int FilesDeleted { get; init; }

    /// <summary>
    /// Number of files unchanged.
    /// </summary>
    [Key(3)]
    public int FilesUnchanged { get; init; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    [Key(4)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// List of errors (if any).
    /// </summary>
    [Key(5)]
    public IReadOnlyList<string>? Errors { get; init; }
}
