using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.FileTransfer;
using OrbitMesh.Core.FileTransfer.Protocol;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services.P2P;

namespace OrbitMesh.Host.Services.FileTransfer;

/// <summary>
/// Smart file transfer service that automatically selects the best transfer method.
/// Priority: P2P → HTTP (guaranteed fallback).
/// </summary>
public class SmartFileTransferService : IFileTransferService
{
    private readonly IPeerCoordinator? _peerCoordinator;
    private readonly IFileStorageService _fileStorage;
    private readonly HttpClient _httpClient;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly SmartFileTransferOptions _options;
    private readonly ILogger<SmartFileTransferService> _logger;

    private readonly ConcurrentDictionary<string, TransferStatistics> _transferStats = new();

    public event EventHandler<FileTransferProgress>? ProgressChanged;
    public event EventHandler<FileTransferResult>? TransferCompleted;

    public SmartFileTransferService(
        IFileStorageService fileStorage,
        HttpClient httpClient,
        IAgentRegistry agentRegistry,
        IJobDispatcher jobDispatcher,
        IOptions<SmartFileTransferOptions> options,
        ILogger<SmartFileTransferService> logger,
        IPeerCoordinator? peerCoordinator = null)
    {
        _fileStorage = fileStorage;
        _httpClient = httpClient;
        _agentRegistry = agentRegistry;
        _jobDispatcher = jobDispatcher;
        _options = options.Value;
        _logger = logger;
        _peerCoordinator = peerCoordinator;
    }

    /// <inheritdoc />
    public async Task<FileTransferResult> TransferFileAsync(
        FileTransferRequest request,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var transferId = request.TransferId;

        _logger.LogInformation(
            "Starting file transfer {TransferId}: {Source} → {Target} (Mode: {Mode})",
            transferId, request.SourcePath, request.TargetAgentId, request.Mode);

        try
        {
            // Get file info
            var fileInfo = await GetFileInfoAsync(request.SourcePath, cancellationToken);
            if (fileInfo == null)
            {
                return CreateFailedResult(request, stopwatch.Elapsed, "Source file not found");
            }

            // Determine transfer method
            var (method, p2pAttempted, p2pFailureReason) = await DetermineTransferMethodAsync(
                request, fileInfo.Size, cancellationToken);

            ReportProgress(progress, transferId, FileTransferStatus.InProgress, method, 0, fileInfo.Size);

            FileTransferResult result;

            if (method == FileTransferMethod.P2P)
            {
                result = await TransferViaP2PAsync(request, fileInfo, progress, cancellationToken);
            }
            else
            {
                result = await TransferViaHttpAsync(request, fileInfo, progress, cancellationToken);
            }

            // Update result with P2P attempt info
            result = result with
            {
                P2PAttempted = p2pAttempted,
                P2PFailureReason = p2pFailureReason,
                Duration = stopwatch.Elapsed,
                BytesPerSecond = result.BytesTransferred / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)
            };

            // Update statistics
            UpdateStatistics(request.TargetAgentId, result);

            _logger.LogInformation(
                "Transfer {TransferId} completed: {Status}, Method: {Method}, Size: {Size}, Duration: {Duration:F2}s",
                transferId, result.Success ? "Success" : "Failed", result.Method,
                FormatSize(result.BytesTransferred), stopwatch.Elapsed.TotalSeconds);

            TransferCompleted?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            return CreateCancelledResult(request, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer {TransferId} failed with exception", transferId);
            return CreateFailedResult(request, stopwatch.Elapsed, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<BatchFileTransferResult> TransferBatchAsync(
        BatchFileTransferRequest request,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchId = request.BatchId;

        _logger.LogInformation(
            "Starting batch transfer {BatchId}: {FileCount} files to {Target}",
            batchId, request.Files.Count, request.TargetAgentId);

        var results = new List<FileTransferResult>();
        using var semaphore = new SemaphoreSlim(request.MaxConcurrency);

        var tasks = request.Files.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var fileRequest = new FileTransferRequest
                {
                    SourcePath = file.RelativePath,
                    DestinationPath = file.RelativePath,
                    TargetAgentId = request.TargetAgentId,
                    Checksum = file.Checksum,
                    FileSize = file.Size,
                    Mode = request.Mode
                };

                return await TransferFileAsync(fileRequest, null, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        var batchResult = new BatchFileTransferResult
        {
            BatchId = batchId,
            Success = results.All(r => r.Success),
            FilesTransferred = results.Count(r => r.Success),
            FilesFailed = results.Count(r => !r.Success),
            FilesUnchanged = 0, // TODO: Implement checksum-based skip
            BytesTransferred = results.Sum(r => r.BytesTransferred),
            Duration = stopwatch.Elapsed,
            TransfersViaP2P = results.Count(r => r.Method == FileTransferMethod.P2P),
            TransfersViaHttp = results.Count(r => r.Method == FileTransferMethod.Http),
            FileResults = results.Where(r => !r.Success).ToList()
        };

        _logger.LogInformation(
            "Batch {BatchId} completed: {Success}/{Total} files, P2P: {P2P}, HTTP: {Http}, Duration: {Duration:F2}s",
            batchId, batchResult.FilesTransferred, request.Files.Count,
            batchResult.TransfersViaP2P, batchResult.TransfersViaHttp, stopwatch.Elapsed.TotalSeconds);

        return batchResult;
    }

    /// <inheritdoc />
    public async Task<bool> IsP2PAvailableAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (_peerCoordinator == null || !_options.EnableP2P)
        {
            return false;
        }

        // Check if agent is connected and has P2P capability
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);
        if (agent == null || (agent.Status != Core.Enums.AgentStatus.Ready && agent.Status != Core.Enums.AgentStatus.Running))
        {
            return false;
        }

        // Check if there's an existing P2P connection
        var peerInfo = _peerCoordinator.GetPeerInfo(agentId);
        return peerInfo?.IsConnected == true;
    }

    /// <inheritdoc />
    public async Task<FileTransferMode> GetRecommendedModeAsync(
        string agentId,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        // Small files are faster via HTTP (no P2P connection overhead)
        if (fileSize < FileTransferProtocol.MinP2PFileSize)
        {
            return FileTransferMode.Http;
        }

        // Check P2P availability
        if (!await IsP2PAvailableAsync(agentId, cancellationToken))
        {
            return FileTransferMode.Http;
        }

        // Check historical performance
        if (_transferStats.TryGetValue(agentId, out var stats))
        {
            // If P2P consistently fails, prefer HTTP
            if (stats.P2PFailureRate > _options.P2PFailureThreshold)
            {
                return FileTransferMode.Http;
            }

            // If P2P is significantly faster, use it
            if (stats.AverageP2PSpeed > stats.AverageHttpSpeed * 1.5)
            {
                return FileTransferMode.P2PDirect;
            }
        }

        // Default to Auto (will attempt P2P first)
        return FileTransferMode.Auto;
    }

    private async Task<(FileTransferMethod Method, bool P2PAttempted, string? P2PFailureReason)>
        DetermineTransferMethodAsync(
            FileTransferRequest request,
            long fileSize,
            CancellationToken cancellationToken)
    {
        // If explicitly set to HTTP, use HTTP
        if (request.Mode == FileTransferMode.Http)
        {
            return (FileTransferMethod.Http, false, null);
        }

        // If explicitly set to P2P or Auto, try P2P first
        if (request.Mode == FileTransferMode.P2PDirect || request.Mode == FileTransferMode.P2PTurn || request.Mode == FileTransferMode.Auto)
        {
            // Check if file is too small for P2P
            if (fileSize < FileTransferProtocol.MinP2PFileSize)
            {
                return (FileTransferMethod.Http, false, "File too small for P2P");
            }

            // Check P2P availability
            var p2pAvailable = await IsP2PAvailableAsync(request.TargetAgentId, cancellationToken);

            if (p2pAvailable)
            {
                return (FileTransferMethod.P2P, true, null);
            }

            // P2P not available
            if (request.Mode == FileTransferMode.P2PDirect || request.Mode == FileTransferMode.P2PTurn)
            {
                // Explicitly requested P2P but not available
                _logger.LogWarning(
                    "P2P requested but not available for agent {AgentId}, falling back to HTTP",
                    request.TargetAgentId);
            }

            return (FileTransferMethod.Http, true, "P2P connection not available");
        }

        return (FileTransferMethod.Http, false, null);
    }

    private async Task<FileTransferResult> TransferViaP2PAsync(
        FileTransferRequest request,
        FileMetadata fileInfo,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var transferId = request.TransferId;

        _logger.LogDebug("Transferring {TransferId} via P2P", transferId);

        try
        {
            // Read file content
            var (stream, _, _) = await _fileStorage.GetFileAsync(request.SourcePath, cancellationToken);
            if (stream is null)
            {
                throw new FileNotFoundException($"File not found: {request.SourcePath}");
            }

            await using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            await stream.DisposeAsync();
            var fileContent = memoryStream.ToArray();

            // Calculate chunks
            var chunkSize = Math.Min(FileTransferProtocol.DefaultChunkSize, _options.ChunkSize);
            var totalChunks = (int)Math.Ceiling((double)fileInfo.Size / chunkSize);

            // Send transfer request to agent
            var p2pRequest = new P2PTransferRequest
            {
                TransferId = transferId,
                FileName = Path.GetFileName(request.SourcePath),
                FileSize = fileInfo.Size,
                Checksum = fileInfo.Checksum ?? await ComputeChecksumAsync(fileContent),
                ChunkSize = chunkSize,
                TotalChunks = totalChunks,
                DestinationPath = request.DestinationPath,
                Overwrite = request.Overwrite
            };

            // Send file in chunks
            var bytesTransferred = 0L;
            for (var i = 0; i < totalChunks; i++)
            {
                var offset = i * chunkSize;
                var length = (int)Math.Min(chunkSize, fileContent.Length - offset);
                var chunkData = new byte[length];
                Array.Copy(fileContent, offset, chunkData, 0, length);

                var chunk = new P2PFileChunk
                {
                    TransferId = transferId,
                    ChunkIndex = i,
                    Data = chunkData,
                    ChunkChecksum = ComputeChunkChecksum(chunkData),
                    IsLastChunk = i == totalChunks - 1
                };

                // Send via peer coordinator
                await _peerCoordinator!.SendFileChunkAsync(
                    request.TargetAgentId,
                    chunk,
                    cancellationToken);

                bytesTransferred += length;

                ReportProgress(progress, transferId, FileTransferStatus.InProgress,
                    FileTransferMethod.P2P, bytesTransferred, fileInfo.Size, i + 1, totalChunks);
            }

            return new FileTransferResult
            {
                TransferId = transferId,
                Success = true,
                Status = FileTransferStatus.Completed,
                Method = FileTransferMethod.P2P,
                DestinationPath = request.DestinationPath,
                BytesTransferred = bytesTransferred,
                Checksum = p2pRequest.Checksum,
                ChecksumVerified = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "P2P transfer {TransferId} failed, falling back to HTTP", transferId);

            // Fall back to HTTP
            return await TransferViaHttpAsync(request, fileInfo, progress, cancellationToken);
        }
    }

    private async Task<FileTransferResult> TransferViaHttpAsync(
        FileTransferRequest request,
        FileMetadata fileInfo,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var transferId = request.TransferId;

        _logger.LogDebug("Transferring {TransferId} via HTTP", transferId);

        try
        {
            // Create file sync job for the agent
            var jobRequest = JobRequest.Create("orbit:file.download") with
            {
                TargetAgentId = request.TargetAgentId,
                Parameters = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                    sourcePath = $"{_options.ServerBaseUrl}/api/files/file/{request.SourcePath}",
                    destinationPath = request.DestinationPath,
                    checksum = fileInfo.Checksum,
                    overwrite = request.Overwrite,
                    createDirectories = request.CreateDirectories
                }),
                Timeout = request.Timeout ?? TimeSpan.FromMinutes(10)
            };

            await _jobDispatcher.EnqueueAsync(jobRequest, cancellationToken);

            ReportProgress(progress, transferId, FileTransferStatus.Completed,
                FileTransferMethod.Http, fileInfo.Size, fileInfo.Size);

            return new FileTransferResult
            {
                TransferId = transferId,
                Success = true,
                Status = FileTransferStatus.Completed,
                Method = FileTransferMethod.Http,
                DestinationPath = request.DestinationPath,
                BytesTransferred = fileInfo.Size,
                Checksum = fileInfo.Checksum,
                ChecksumVerified = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP transfer {TransferId} failed", transferId);

            return new FileTransferResult
            {
                TransferId = transferId,
                Success = false,
                Status = FileTransferStatus.Failed,
                Method = FileTransferMethod.Http,
                Error = ex.Message
            };
        }
    }

    private async Task<FileMetadata?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _fileStorage.ExistsAsync(path, cancellationToken);
            if (!exists)
            {
                return null;
            }

            var (stream, _, _) = await _fileStorage.GetFileAsync(path, cancellationToken);
            if (stream == null)
            {
                return null;
            }

            await using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            await stream.DisposeAsync();
            var content = memoryStream.ToArray();

            return new FileMetadata
            {
                Path = path,
                Size = content.Length,
                Checksum = await ComputeChecksumAsync(content)
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ComputeChecksumAsync(byte[] content)
    {
        await using var stream = new MemoryStream(content);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string ComputeChunkChecksum(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    private void ReportProgress(
        IProgress<FileTransferProgress>? progress,
        string transferId,
        FileTransferStatus status,
        FileTransferMethod method,
        long bytesTransferred,
        long? totalBytes,
        int currentChunk = 0,
        int totalChunks = 0)
    {
        var progressInfo = new FileTransferProgress
        {
            TransferId = transferId,
            Status = status,
            Method = method,
            BytesTransferred = bytesTransferred,
            TotalBytes = totalBytes,
            CurrentChunk = currentChunk,
            TotalChunks = totalChunks
        };

        progress?.Report(progressInfo);
        ProgressChanged?.Invoke(this, progressInfo);
    }

    private void UpdateStatistics(string agentId, FileTransferResult result)
    {
        var stats = _transferStats.GetOrAdd(agentId, _ => new TransferStatistics());

        if (result.Success)
        {
            if (result.Method == FileTransferMethod.P2P)
            {
                stats.P2PSuccessCount++;
                stats.TotalP2PBytes += result.BytesTransferred;
                stats.TotalP2PTime += result.Duration;
            }
            else
            {
                stats.HttpSuccessCount++;
                stats.TotalHttpBytes += result.BytesTransferred;
                stats.TotalHttpTime += result.Duration;
            }
        }
        else if (result.P2PAttempted)
        {
            stats.P2PFailureCount++;
        }
    }

    private static FileTransferResult CreateFailedResult(
        FileTransferRequest request, TimeSpan duration, string error)
    {
        return new FileTransferResult
        {
            TransferId = request.TransferId,
            Success = false,
            Status = FileTransferStatus.Failed,
            Method = FileTransferMethod.Unknown,
            Duration = duration,
            Error = error
        };
    }

    private static FileTransferResult CreateCancelledResult(
        FileTransferRequest request, TimeSpan duration)
    {
        return new FileTransferResult
        {
            TransferId = request.TransferId,
            Success = false,
            Status = FileTransferStatus.Cancelled,
            Method = FileTransferMethod.Unknown,
            Duration = duration,
            Error = "Transfer cancelled"
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F2} {sizes[order]}";
    }

    private sealed class TransferStatistics
    {
        public int P2PSuccessCount;
        public int P2PFailureCount;
        public long TotalP2PBytes;
        public TimeSpan TotalP2PTime;

        public int HttpSuccessCount;
        public long TotalHttpBytes;
        public TimeSpan TotalHttpTime;

        public double P2PFailureRate =>
            P2PSuccessCount + P2PFailureCount > 0
                ? (double)P2PFailureCount / (P2PSuccessCount + P2PFailureCount)
                : 0;

        public double AverageP2PSpeed =>
            TotalP2PTime.TotalSeconds > 0
                ? TotalP2PBytes / TotalP2PTime.TotalSeconds
                : 0;

        public double AverageHttpSpeed =>
            TotalHttpTime.TotalSeconds > 0
                ? TotalHttpBytes / TotalHttpTime.TotalSeconds
                : 0;
    }

    private sealed class FileMetadata
    {
        public required string Path { get; init; }
        public long Size { get; init; }
        public string? Checksum { get; init; }
    }
}

/// <summary>
/// Options for the smart file transfer service.
/// </summary>
#pragma warning disable CA1056 // URI-like properties should not be strings
public class SmartFileTransferOptions
{
    /// <summary>Whether P2P transfer is enabled.</summary>
    public bool EnableP2P { get; set; } = true;

    /// <summary>Server base URL for HTTP file serving.</summary>
    public string ServerBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>Chunk size for P2P transfers.</summary>
    public int ChunkSize { get; set; } = FileTransferProtocol.DefaultChunkSize;

    /// <summary>Threshold for P2P failure rate before preferring HTTP.</summary>
    public double P2PFailureThreshold { get; set; } = 0.3;

    /// <summary>Maximum concurrent transfers per agent.</summary>
    public int MaxConcurrentTransfers { get; set; } = 4;

    /// <summary>Default timeout for transfers.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
#pragma warning restore CA1056
