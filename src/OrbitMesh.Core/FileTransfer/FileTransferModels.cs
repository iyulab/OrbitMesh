using MessagePack;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.FileTransfer;

/// <summary>
/// Request for transferring a file.
/// </summary>
[MessagePackObject]
public sealed record FileTransferRequest
{
    /// <summary>
    /// Unique identifier for this transfer request.
    /// </summary>
    [Key(0)]
    public string TransferId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Source path or URL of the file.
    /// </summary>
    [Key(1)]
    public required string SourcePath { get; init; }

    /// <summary>
    /// Destination path on the target.
    /// </summary>
    [Key(2)]
    public required string DestinationPath { get; init; }

    /// <summary>
    /// Target agent ID for the transfer.
    /// </summary>
    [Key(3)]
    public required string TargetAgentId { get; init; }

    /// <summary>
    /// Source agent ID (null for server-to-agent transfers).
    /// </summary>
    [Key(4)]
    public string? SourceAgentId { get; init; }

    /// <summary>
    /// Expected SHA256 checksum for verification.
    /// </summary>
    [Key(5)]
    public string? Checksum { get; init; }

    /// <summary>
    /// File size in bytes (if known).
    /// </summary>
    [Key(6)]
    public long? FileSize { get; init; }

    /// <summary>
    /// Preferred transfer mode.
    /// </summary>
    [Key(7)]
    public FileTransferMode Mode { get; init; } = FileTransferMode.Auto;

    /// <summary>
    /// Whether to overwrite existing file.
    /// </summary>
    [Key(8)]
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// Whether to create parent directories if they don't exist.
    /// </summary>
    [Key(9)]
    public bool CreateDirectories { get; init; } = true;

    /// <summary>
    /// Timeout for the transfer operation.
    /// </summary>
    [Key(10)]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Additional metadata for the transfer.
    /// </summary>
    [Key(11)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of a file transfer operation.
/// </summary>
[MessagePackObject]
public sealed record FileTransferResult
{
    /// <summary>
    /// Transfer request ID.
    /// </summary>
    [Key(0)]
    public required string TransferId { get; init; }

    /// <summary>
    /// Whether the transfer was successful.
    /// </summary>
    [Key(1)]
    public bool Success { get; init; }

    /// <summary>
    /// Final status of the transfer.
    /// </summary>
    [Key(2)]
    public FileTransferStatus Status { get; init; }

    /// <summary>
    /// Actual method used for the transfer.
    /// </summary>
    [Key(3)]
    public FileTransferMethod Method { get; init; }

    /// <summary>
    /// Destination path where file was saved.
    /// </summary>
    [Key(4)]
    public string? DestinationPath { get; init; }

    /// <summary>
    /// Number of bytes transferred.
    /// </summary>
    [Key(5)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// SHA256 checksum of the transferred file.
    /// </summary>
    [Key(6)]
    public string? Checksum { get; init; }

    /// <summary>
    /// Whether checksum verification passed.
    /// </summary>
    [Key(7)]
    public bool ChecksumVerified { get; init; }

    /// <summary>
    /// Transfer duration.
    /// </summary>
    [Key(8)]
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Transfer speed in bytes per second.
    /// </summary>
    [Key(9)]
    public double BytesPerSecond { get; init; }

    /// <summary>
    /// Error message if transfer failed.
    /// </summary>
    [Key(10)]
    public string? Error { get; init; }

    /// <summary>
    /// Whether P2P was attempted.
    /// </summary>
    [Key(11)]
    public bool P2PAttempted { get; init; }

    /// <summary>
    /// Reason P2P failed (if applicable).
    /// </summary>
    [Key(12)]
    public string? P2PFailureReason { get; init; }
}

/// <summary>
/// Progress information for a file transfer.
/// </summary>
[MessagePackObject]
public sealed record FileTransferProgress
{
    /// <summary>
    /// Transfer request ID.
    /// </summary>
    [Key(0)]
    public required string TransferId { get; init; }

    /// <summary>
    /// Current status.
    /// </summary>
    [Key(1)]
    public FileTransferStatus Status { get; init; }

    /// <summary>
    /// Method being used.
    /// </summary>
    [Key(2)]
    public FileTransferMethod Method { get; init; }

    /// <summary>
    /// Bytes transferred so far.
    /// </summary>
    [Key(3)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Total bytes to transfer (if known).
    /// </summary>
    [Key(4)]
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [Key(5)]
    public double ProgressPercent => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes.Value * 100 : 0;

    /// <summary>
    /// Current transfer speed in bytes per second.
    /// </summary>
    [Key(6)]
    public double BytesPerSecond { get; init; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    [Key(7)]
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current chunk being transferred (for chunked transfers).
    /// </summary>
    [Key(8)]
    public int CurrentChunk { get; init; }

    /// <summary>
    /// Total number of chunks.
    /// </summary>
    [Key(9)]
    public int TotalChunks { get; init; }
}

/// <summary>
/// Request for batch file transfer (multiple files).
/// </summary>
[MessagePackObject]
public sealed record BatchFileTransferRequest
{
    /// <summary>
    /// Unique identifier for this batch transfer.
    /// </summary>
    [Key(0)]
    public string BatchId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Target agent ID.
    /// </summary>
    [Key(1)]
    public required string TargetAgentId { get; init; }

    /// <summary>
    /// List of files to transfer.
    /// </summary>
    [Key(2)]
    public required IReadOnlyList<FileTransferItem> Files { get; init; }

    /// <summary>
    /// Preferred transfer mode for all files.
    /// </summary>
    [Key(3)]
    public FileTransferMode Mode { get; init; } = FileTransferMode.Auto;

    /// <summary>
    /// Whether to delete files at destination that don't exist in source.
    /// </summary>
    [Key(4)]
    public bool DeleteOrphans { get; init; }

    /// <summary>
    /// Maximum concurrent transfers.
    /// </summary>
    [Key(5)]
    public int MaxConcurrency { get; init; } = 4;
}

/// <summary>
/// Individual file item in a batch transfer.
/// </summary>
[MessagePackObject]
public sealed record FileTransferItem
{
    /// <summary>
    /// Relative path of the file.
    /// </summary>
    [Key(0)]
    public required string RelativePath { get; init; }

    /// <summary>
    /// SHA256 checksum.
    /// </summary>
    [Key(1)]
    public string? Checksum { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    [Key(2)]
    public long Size { get; init; }

    /// <summary>
    /// Last modified time.
    /// </summary>
    [Key(3)]
    public DateTimeOffset LastModified { get; init; }
}

/// <summary>
/// Result of a batch file transfer.
/// </summary>
[MessagePackObject]
public sealed record BatchFileTransferResult
{
    /// <summary>
    /// Batch transfer ID.
    /// </summary>
    [Key(0)]
    public required string BatchId { get; init; }

    /// <summary>
    /// Whether the batch transfer was successful overall.
    /// </summary>
    [Key(1)]
    public bool Success { get; init; }

    /// <summary>
    /// Number of files successfully transferred.
    /// </summary>
    [Key(2)]
    public int FilesTransferred { get; init; }

    /// <summary>
    /// Number of files that failed.
    /// </summary>
    [Key(3)]
    public int FilesFailed { get; init; }

    /// <summary>
    /// Number of files unchanged (checksum matched).
    /// </summary>
    [Key(4)]
    public int FilesUnchanged { get; init; }

    /// <summary>
    /// Number of files deleted (orphans).
    /// </summary>
    [Key(5)]
    public int FilesDeleted { get; init; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    [Key(6)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Total duration.
    /// </summary>
    [Key(7)]
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Transfers that used P2P.
    /// </summary>
    [Key(8)]
    public int TransfersViaP2P { get; init; }

    /// <summary>
    /// Transfers that used HTTP.
    /// </summary>
    [Key(9)]
    public int TransfersViaHttp { get; init; }

    /// <summary>
    /// Individual file results (for failures).
    /// </summary>
    [Key(10)]
    public IReadOnlyList<FileTransferResult>? FileResults { get; init; }

    /// <summary>
    /// Error message if batch failed.
    /// </summary>
    [Key(11)]
    public string? Error { get; init; }
}
