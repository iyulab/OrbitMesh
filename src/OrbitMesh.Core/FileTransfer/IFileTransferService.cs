using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.FileTransfer;

/// <summary>
/// Centralized file transfer service that supports multiple transfer methods
/// with automatic fallback for guaranteed delivery.
/// </summary>
/// <remarks>
/// Transfer priority:
/// 1. P2P direct transfer (highest performance)
/// 2. HTTP through server (guaranteed fallback)
/// </remarks>
public interface IFileTransferService
{
    /// <summary>
    /// Transfers a single file to the target agent.
    /// </summary>
    /// <param name="request">Transfer request details.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the transfer operation.</returns>
    Task<FileTransferResult> TransferFileAsync(
        FileTransferRequest request,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers multiple files to the target agent in a batch operation.
    /// Uses parallel transfers and intelligent chunking for optimal performance.
    /// </summary>
    /// <param name="request">Batch transfer request.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the batch transfer operation.</returns>
    Task<BatchFileTransferResult> TransferBatchAsync(
        BatchFileTransferRequest request,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if P2P transfer is available to the specified agent.
    /// </summary>
    /// <param name="agentId">Target agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if P2P is available and recommended.</returns>
    Task<bool> IsP2PAvailableAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recommended transfer mode for the specified agent based on
    /// current connectivity, latency, and historical performance.
    /// </summary>
    /// <param name="agentId">Target agent ID.</param>
    /// <param name="fileSize">Size of file to transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recommended transfer mode.</returns>
    Task<FileTransferMode> GetRecommendedModeAsync(
        string agentId,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when transfer progress changes.
    /// </summary>
#pragma warning disable CA1003 // Use generic event handler instances (FileTransferProgress is a MessagePack DTO, not EventArgs)
    event EventHandler<FileTransferProgress>? ProgressChanged;
#pragma warning restore CA1003

    /// <summary>
    /// Raised when a transfer completes (success or failure).
    /// </summary>
#pragma warning disable CA1003 // Use generic event handler instances (FileTransferResult is a MessagePack DTO, not EventArgs)
    event EventHandler<FileTransferResult>? TransferCompleted;
#pragma warning restore CA1003
}

/// <summary>
/// Extension methods for IFileTransferService.
/// </summary>
public static class FileTransferServiceExtensions
{
    /// <summary>
    /// Transfers a file using the default settings.
    /// </summary>
    public static Task<FileTransferResult> TransferFileAsync(
        this IFileTransferService service,
        string sourcePath,
        string destinationPath,
        string targetAgentId,
        CancellationToken cancellationToken = default)
    {
        var request = new FileTransferRequest
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            TargetAgentId = targetAgentId
        };

        return service.TransferFileAsync(request, null, cancellationToken);
    }

    /// <summary>
    /// Transfers a file with a specific transfer mode.
    /// </summary>
    public static Task<FileTransferResult> TransferFileAsync(
        this IFileTransferService service,
        string sourcePath,
        string destinationPath,
        string targetAgentId,
        FileTransferMode mode,
        CancellationToken cancellationToken = default)
    {
        var request = new FileTransferRequest
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            TargetAgentId = targetAgentId,
            Mode = mode
        };

        return service.TransferFileAsync(request, null, cancellationToken);
    }
}
