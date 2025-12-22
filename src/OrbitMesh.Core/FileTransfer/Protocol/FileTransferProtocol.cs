using MessagePack;

namespace OrbitMesh.Core.FileTransfer.Protocol;

/// <summary>
/// Constants for the P2P file transfer protocol.
/// </summary>
public static class FileTransferProtocol
{
    /// <summary>
    /// Protocol version.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Default chunk size for P2P transfers (64KB).
    /// Optimized for NAT traversal and reliable delivery.
    /// </summary>
    public const int DefaultChunkSize = 64 * 1024;

    /// <summary>
    /// Maximum chunk size (1MB).
    /// </summary>
    public const int MaxChunkSize = 1024 * 1024;

    /// <summary>
    /// Minimum file size to consider P2P transfer (10KB).
    /// Smaller files may be faster via HTTP due to connection overhead.
    /// </summary>
    public const long MinP2PFileSize = 10 * 1024;

    /// <summary>
    /// Maximum concurrent chunk transfers per file.
    /// </summary>
    public const int MaxConcurrentChunks = 4;

    /// <summary>
    /// Timeout for individual chunk transfers.
    /// </summary>
    public static readonly TimeSpan ChunkTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum retry attempts for a chunk.
    /// </summary>
    public const int MaxChunkRetries = 3;

    /// <summary>
    /// Message type identifiers.
    /// </summary>
#pragma warning disable CA1034 // Nested types should not be visible (intentional design for protocol message grouping)
    public static class MessageTypes
    {
        public const byte TransferRequest = 0x01;
        public const byte TransferAccept = 0x02;
        public const byte TransferReject = 0x03;
        public const byte FileChunk = 0x10;
        public const byte ChunkAck = 0x11;
        public const byte ChunkNack = 0x12;
        public const byte TransferComplete = 0x20;
        public const byte TransferCancel = 0x21;
        public const byte TransferError = 0x22;
    }
#pragma warning restore CA1034
}

/// <summary>
/// P2P transfer request message.
/// </summary>
[MessagePackObject]
public sealed record P2PTransferRequest
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public required string FileName { get; init; }

    [Key(2)]
    public long FileSize { get; init; }

    [Key(3)]
    public required string Checksum { get; init; }

    [Key(4)]
    public int ChunkSize { get; init; } = FileTransferProtocol.DefaultChunkSize;

    [Key(5)]
    public int TotalChunks { get; init; }

    [Key(6)]
    public required string DestinationPath { get; init; }

    [Key(7)]
    public bool Overwrite { get; init; } = true;
}

/// <summary>
/// P2P transfer response message.
/// </summary>
[MessagePackObject]
public sealed record P2PTransferResponse
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public bool Accepted { get; init; }

    [Key(2)]
    public string? RejectReason { get; init; }

    /// <summary>
    /// Chunks already present at destination (for resume support).
    /// </summary>
    [Key(3)]
    public IReadOnlyList<int>? ExistingChunks { get; init; }
}

/// <summary>
/// File chunk message for P2P transfer.
/// </summary>
[MessagePackObject]
public sealed record P2PFileChunk
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public int ChunkIndex { get; init; }

    [Key(2)]
    public required byte[] Data { get; init; }

    [Key(3)]
    public required string ChunkChecksum { get; init; }

    [Key(4)]
    public bool IsLastChunk { get; init; }
}

/// <summary>
/// Chunk acknowledgment message.
/// </summary>
[MessagePackObject]
public sealed record P2PChunkAck
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public int ChunkIndex { get; init; }

    [Key(2)]
    public bool Success { get; init; }

    [Key(3)]
    public string? Error { get; init; }
}

/// <summary>
/// Transfer complete message.
/// </summary>
[MessagePackObject]
public sealed record P2PTransferComplete
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public bool Success { get; init; }

    [Key(2)]
    public string? FinalChecksum { get; init; }

    [Key(3)]
    public bool ChecksumVerified { get; init; }

    [Key(4)]
    public string? Error { get; init; }
}

/// <summary>
/// Transfer cancellation message.
/// </summary>
[MessagePackObject]
public sealed record P2PTransferCancel
{
    [Key(0)]
    public required string TransferId { get; init; }

    [Key(1)]
    public string? Reason { get; init; }
}
