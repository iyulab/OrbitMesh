using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using OrbitMesh.Server.Controllers;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Local file system implementation of file storage service.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    /// <summary>
    /// Creates a new local file storage service.
    /// </summary>
    /// <param name="rootPath">Root path for file storage.</param>
    /// <param name="logger">Logger instance.</param>
    public LocalFileStorageService(string rootPath, ILogger<LocalFileStorageService> logger)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _logger = logger;
        _contentTypeProvider = new FileExtensionContentTypeProvider();

        // Ensure root directory exists
        Directory.CreateDirectory(_rootPath);

        _logger.LogInformation("File storage initialized at {RootPath}", _rootPath);
    }

    /// <inheritdoc />
    public async Task<FileSaveResult> SaveFileAsync(
        string path,
        Stream content,
        bool overwrite = true,
        string? expectedChecksum = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        // Security check - prevent path traversal
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return new FileSaveResult
            {
                Success = false,
                Error = "Invalid path"
            };
        }

        // Check if file exists
        if (File.Exists(fullPath) && !overwrite)
        {
            return new FileSaveResult
            {
                Success = false,
                Error = "File already exists"
            };
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write file to temp location first
            var tempPath = fullPath + ".tmp";
            await using (var fileStream = File.Create(tempPath))
            {
                await content.CopyToAsync(fileStream, cancellationToken);
            }

            // Compute checksum
            var checksum = await ComputeChecksumAsync(tempPath, cancellationToken);

            // Verify checksum if provided
            if (!string.IsNullOrEmpty(expectedChecksum) &&
                !string.Equals(checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                return new FileSaveResult
                {
                    Success = false,
                    Error = $"Checksum mismatch. Expected: {expectedChecksum}, Actual: {checksum}"
                };
            }

            // Move temp file to final location
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            File.Move(tempPath, fullPath);

            var fileInfo = new FileInfo(fullPath);

            _logger.LogInformation("File saved: {Path} ({Size} bytes)", path, fileInfo.Length);

            return new FileSaveResult
            {
                Success = true,
                Path = path,
                Size = fileInfo.Length,
                Checksum = checksum
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {Path}", path);
            return new FileSaveResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public Task<(Stream? Stream, string ContentType, string FileName)> GetFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        // Security check
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid path");
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<(Stream?, string, string)>((null, "application/octet-stream", string.Empty));
        }

        var fileName = Path.GetFileName(fullPath);
        if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = File.OpenRead(fullPath);
        return Task.FromResult<(Stream?, string, string)>((stream, contentType, fileName));
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        // Security check
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {Path}", path);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {Path}", path);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task<SyncManifest?> GetManifestAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        // Security check
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Directory.Exists(fullPath))
        {
            return null;
        }

        var manifest = new SyncManifest();

        foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(fullPath, file).Replace('\\', '/');
            var fileInfo = new FileInfo(file);
            var checksum = await ComputeChecksumAsync(file, cancellationToken);

            manifest.Files.Add(new SyncFileEntry
            {
                Path = relativePath,
                Checksum = checksum,
                Size = fileInfo.Length
            });
        }

        return manifest;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        // Security check
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
    }

    private string GetFullPath(string relativePath)
    {
        // Normalize path separators and remove leading/trailing separators
        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(_rootPath, normalized));
    }

    private static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
