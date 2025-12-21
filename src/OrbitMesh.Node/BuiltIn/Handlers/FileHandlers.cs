using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Models;
using OrbitMesh.Node.BuiltIn.Models;

namespace OrbitMesh.Node.BuiltIn.Handlers;

/// <summary>
/// Handler for file download command.
/// </summary>
public sealed class FileDownloadHandler : IRequestResponseHandler<FileDownloadResult>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileDownloadHandler> _logger;

    public string Command => Commands.File.Download;

    public FileDownloadHandler(HttpClient httpClient, ILogger<FileDownloadHandler> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FileDownloadResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileDownloadRequest>();

        _logger.LogInformation("Downloading file from {Source} to {Destination}",
            request.SourcePath, request.DestinationPath);

        try
        {
            // Create parent directories if needed
            if (request.CreateDirectories)
            {
                var dir = Path.GetDirectoryName(request.DestinationPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            // Check if file exists and overwrite is disabled
            if (File.Exists(request.DestinationPath) && !request.Overwrite)
            {
                return new FileDownloadResult
                {
                    Success = false,
                    Error = "File already exists and overwrite is disabled"
                };
            }

            // Download the file
            using var response = await _httpClient.GetAsync(
                new Uri(request.SourcePath),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(request.DestinationPath);
            await stream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            var fileInfo = new FileInfo(request.DestinationPath);
            var checksum = await ComputeChecksumAsync(request.DestinationPath, cancellationToken);

            // Verify checksum if provided
            var checksumVerified = string.IsNullOrEmpty(request.Checksum) ||
                                   string.Equals(request.Checksum, checksum, StringComparison.OrdinalIgnoreCase);

            if (!checksumVerified)
            {
                _logger.LogWarning("Checksum mismatch. Expected: {Expected}, Actual: {Actual}",
                    request.Checksum, checksum);
            }

            return new FileDownloadResult
            {
                Success = checksumVerified,
                LocalPath = request.DestinationPath,
                Size = fileInfo.Length,
                Checksum = checksum,
                ChecksumVerified = checksumVerified,
                Error = checksumVerified ? null : "Checksum verification failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from {Source}", request.SourcePath);
            return new FileDownloadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Handler for file upload command.
/// </summary>
public sealed class FileUploadHandler : IRequestResponseHandler<FileUploadResult>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileUploadHandler> _logger;

    public string Command => Commands.File.Upload;

    public FileUploadHandler(HttpClient httpClient, ILogger<FileUploadHandler> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FileUploadResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileUploadRequest>();

        _logger.LogInformation("Uploading file from {Source} to {Destination}",
            request.SourcePath, request.DestinationUrl);

        try
        {
            // Check if source file exists
            if (!File.Exists(request.SourcePath))
            {
                return new FileUploadResult
                {
                    Success = false,
                    Error = $"Source file not found: {request.SourcePath}"
                };
            }

            var fileInfo = new FileInfo(request.SourcePath);

            // Compute checksum if requested
            string? checksum = null;
            if (request.IncludeChecksum)
            {
                checksum = await ComputeChecksumAsync(request.SourcePath, cancellationToken);
            }

            // Create multipart form data
            using var content = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(request.SourcePath);
            using var streamContent = new StreamContent(fileStream);

            // Set content headers
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", Path.GetFileName(request.SourcePath));

            // Add metadata
            // Note: StringContent instances are owned and disposed by MultipartFormDataContent
#pragma warning disable CA2000 // Dispose objects before losing scope
            content.Add(new StringContent(request.Overwrite.ToString()), "overwrite");
            if (checksum != null)
            {
                content.Add(new StringContent(checksum), "checksum");
            }
            if (request.Metadata != null)
            {
                foreach (var (key, value) in request.Metadata)
                {
                    content.Add(new StringContent(value), $"metadata_{key}");
                }
            }
#pragma warning restore CA2000

            // Upload the file
            using var response = await _httpClient.PostAsync(
                new Uri(request.DestinationUrl),
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FileUploadResult
                {
                    Success = false,
                    Error = $"Upload failed: {response.StatusCode} - {errorContent}"
                };
            }

            // Try to get server path from response
            string? serverPath = null;
            try
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseObj = System.Text.Json.JsonSerializer.Deserialize<UploadResponse>(responseJson);
                serverPath = responseObj?.Path;
            }
            catch
            {
                // Ignore deserialization errors
            }

            return new FileUploadResult
            {
                Success = true,
                ServerPath = serverPath,
                Size = fileInfo.Length,
                Checksum = checksum
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to {Destination}", request.DestinationUrl);
            return new FileUploadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Response from file upload endpoint.
/// </summary>
internal sealed class UploadResponse
{
    public string? Path { get; set; }
    public string? Checksum { get; set; }
    public long? Size { get; set; }
}

/// <summary>
/// Handler for file delete command.
/// </summary>
public sealed class FileDeleteHandler : IRequestResponseHandler<FileDeleteResult>
{
    private readonly ILogger<FileDeleteHandler> _logger;

    public string Command => Commands.File.Delete;

    public FileDeleteHandler(ILogger<FileDeleteHandler> logger)
    {
        _logger = logger;
    }

    public Task<FileDeleteResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileDeleteRequest>();

        _logger.LogInformation("Deleting {Path} (recursive: {Recursive})",
            request.Path, request.Recursive);

        try
        {
            if (Directory.Exists(request.Path))
            {
                if (request.Recursive)
                {
                    Directory.Delete(request.Path, recursive: true);
                }
                else
                {
                    Directory.Delete(request.Path);
                }
            }
            else if (File.Exists(request.Path))
            {
                File.Delete(request.Path);
            }
            else
            {
                return Task.FromResult(new FileDeleteResult
                {
                    Success = false,
                    Error = "File or directory not found"
                });
            }

            return Task.FromResult(new FileDeleteResult { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Path}", request.Path);
            return Task.FromResult(new FileDeleteResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}

/// <summary>
/// Handler for file list command.
/// </summary>
public sealed class FileListHandler : IRequestResponseHandler<FileListResult>
{
    public string Command => Commands.File.List;

    public Task<FileListResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileListRequest>();

        try
        {
            if (!Directory.Exists(request.Path))
            {
                return Task.FromResult(new FileListResult
                {
                    Success = false,
                    Error = "Directory not found"
                });
            }

            var searchOption = request.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var entries = new List<FileEntry>();

            // Add files
            foreach (var file in Directory.EnumerateFiles(request.Path, request.Pattern, searchOption))
            {
                var info = new FileInfo(file);
                entries.Add(new FileEntry
                {
                    Name = info.Name,
                    Path = info.FullName,
                    IsDirectory = false,
                    Size = info.Length,
                    ModifiedAt = info.LastWriteTimeUtc,
                    CreatedAt = info.CreationTimeUtc
                });
            }

            // Add directories if requested
            if (request.IncludeDirectories)
            {
                foreach (var dir in Directory.EnumerateDirectories(request.Path, "*", searchOption))
                {
                    var info = new DirectoryInfo(dir);
                    entries.Add(new FileEntry
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        IsDirectory = true,
                        Size = 0,
                        ModifiedAt = info.LastWriteTimeUtc,
                        CreatedAt = info.CreationTimeUtc
                    });
                }
            }

            return Task.FromResult(new FileListResult
            {
                Success = true,
                Entries = entries.OrderBy(e => e.Path).ToList()
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileListResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}

/// <summary>
/// Handler for file info command.
/// </summary>
public sealed class FileInfoHandler : IRequestResponseHandler<FileInfoResult>
{
    public string Command => Commands.File.Info;

    public async Task<FileInfoResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileInfoRequest>();

        try
        {
            if (File.Exists(request.Path))
            {
                var info = new FileInfo(request.Path);
                string? checksum = null;

                if (request.ComputeChecksum)
                {
                    await using var stream = info.OpenRead();
                    var hash = await SHA256.HashDataAsync(stream, cancellationToken);
                    checksum = Convert.ToHexString(hash);
                }

                return new FileInfoResult
                {
                    Success = true,
                    Exists = true,
                    Entry = new FileEntry
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        IsDirectory = false,
                        Size = info.Length,
                        ModifiedAt = info.LastWriteTimeUtc,
                        CreatedAt = info.CreationTimeUtc
                    },
                    Checksum = checksum
                };
            }
            else if (Directory.Exists(request.Path))
            {
                var info = new DirectoryInfo(request.Path);
                return new FileInfoResult
                {
                    Success = true,
                    Exists = true,
                    Entry = new FileEntry
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        IsDirectory = true,
                        Size = 0,
                        ModifiedAt = info.LastWriteTimeUtc,
                        CreatedAt = info.CreationTimeUtc
                    }
                };
            }
            else
            {
                return new FileInfoResult
                {
                    Success = true,
                    Exists = false
                };
            }
        }
        catch (Exception ex)
        {
            return new FileInfoResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Handler for file exists command.
/// </summary>
public sealed class FileExistsHandler : IRequestResponseHandler<bool>
{
    public string Command => Commands.File.Exists;

    public Task<bool> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var path = context.GetRequiredParameter<string>();
        return Task.FromResult(File.Exists(path) || Directory.Exists(path));
    }
}

/// <summary>
/// Handler for file sync command.
/// </summary>
public sealed class FileSyncHandler : IRequestResponseHandler<FileSyncResult>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileSyncHandler> _logger;

    public string Command => Commands.File.Sync;

    public FileSyncHandler(HttpClient httpClient, ILogger<FileSyncHandler> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FileSyncResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<FileSyncRequest>();

        _logger.LogInformation("Syncing from {Source} to {Destination}",
            request.Source, request.Destination);

        // Create destination directory
        Directory.CreateDirectory(request.Destination);

        var downloaded = 0;
        var deleted = 0;
        var unchanged = 0;
        long bytesTransferred = 0;
        var errors = new List<string>();

        try
        {
            // Fetch manifest from source
            var manifestUrl = new Uri(request.Source.TrimEnd('/') + "/manifest.json");
            var manifestResponse = await _httpClient.GetAsync(manifestUrl, cancellationToken);

            if (!manifestResponse.IsSuccessStatusCode)
            {
                return new FileSyncResult
                {
                    Success = false,
                    Errors = [$"Failed to fetch manifest: {manifestResponse.StatusCode}"]
                };
            }

            var manifestJson = await manifestResponse.Content.ReadAsStringAsync(cancellationToken);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<SyncManifest>(manifestJson);

            if (manifest?.Files == null)
            {
                return new FileSyncResult
                {
                    Success = false,
                    Errors = ["Invalid manifest format"]
                };
            }

            var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in manifest.Files)
            {
                // Check include/exclude patterns
                if (!ShouldInclude(file.Path, request.IncludePatterns, request.ExcludePatterns))
                {
                    continue;
                }

                sourceFiles.Add(file.Path);
                var localPath = Path.Combine(request.Destination, file.Path);
                var localDir = Path.GetDirectoryName(localPath);

                if (!string.IsNullOrEmpty(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                // Check if file needs update
                var needsUpdate = true;
                if (File.Exists(localPath) && !string.IsNullOrEmpty(file.Checksum))
                {
                    await using var stream = File.OpenRead(localPath);
                    var hash = await SHA256.HashDataAsync(stream, cancellationToken);
                    var localChecksum = Convert.ToHexString(hash);

                    if (string.Equals(localChecksum, file.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        needsUpdate = false;
                        unchanged++;
                    }
                }

                if (needsUpdate)
                {
                    try
                    {
                        var fileUrl = new Uri(request.Source.TrimEnd('/') + "/" + file.Path.Replace('\\', '/'));
                        using var response = await _httpClient.GetAsync(
                            fileUrl,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken);
                        response.EnsureSuccessStatusCode();

                        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        await using var fileStream = File.Create(localPath);
                        await contentStream.CopyToAsync(fileStream, cancellationToken);

                        downloaded++;
                        bytesTransferred += new FileInfo(localPath).Length;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to download {file.Path}: {ex.Message}");
                    }
                }
            }

            // Delete orphaned files if requested
            // Safety: Skip files modified within the last 5 seconds to avoid race conditions
            // with concurrent file operations from other agents or processes
            if (request.DeleteOrphans)
            {
                var safetyThreshold = DateTime.UtcNow.AddSeconds(-5);

                foreach (var localFile in Directory.EnumerateFiles(request.Destination, "*",
                             SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(request.Destination, localFile);
                    if (!sourceFiles.Contains(relativePath))
                    {
                        try
                        {
                            // Safety check: don't delete recently modified files
                            // This prevents race conditions in multi-agent sync scenarios
                            var fileInfo = new FileInfo(localFile);
                            if (fileInfo.LastWriteTimeUtc > safetyThreshold)
                            {
                                _logger.LogDebug(
                                    "Skipping orphan deletion for recently modified file: {Path}",
                                    relativePath);
                                continue;
                            }

                            File.Delete(localFile);
                            deleted++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to delete {relativePath}: {ex.Message}");
                        }
                    }
                }
            }

            return new FileSyncResult
            {
                Success = errors.Count == 0,
                FilesDownloaded = downloaded,
                FilesDeleted = deleted,
                FilesUnchanged = unchanged,
                BytesTransferred = bytesTransferred,
                Errors = errors.Count > 0 ? errors : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File sync failed");
            return new FileSyncResult
            {
                Success = false,
                FilesDownloaded = downloaded,
                FilesDeleted = deleted,
                FilesUnchanged = unchanged,
                BytesTransferred = bytesTransferred,
                Errors = [ex.Message]
            };
        }
    }

    private static bool ShouldInclude(string path,
        IReadOnlyList<string>? includePatterns,
        IReadOnlyList<string>? excludePatterns)
    {
        // If include patterns specified, file must match at least one
        if (includePatterns is { Count: > 0 })
        {
            var matched = includePatterns.Any(p => MatchPattern(path, p));
            if (!matched) return false;
        }

        // If exclude patterns specified, file must not match any
        if (excludePatterns is { Count: > 0 })
        {
            var excluded = excludePatterns.Any(p => MatchPattern(path, p));
            if (excluded) return false;
        }

        return true;
    }

    private static bool MatchPattern(string path, string pattern)
    {
        // Simple glob-style matching
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

// SyncManifest and SyncFileEntry are now defined in OrbitMesh.Core.Models
