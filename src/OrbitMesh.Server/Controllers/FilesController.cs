using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Controllers;

/// <summary>
/// REST API controller for file transfer operations.
/// Supports file upload from agents and file download for sync operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<FilesController> _logger;

    /// <summary>
    /// Creates a new files controller.
    /// </summary>
    public FilesController(IFileStorageService storageService, ILogger<FilesController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a file from an agent to the server.
    /// </summary>
    /// <param name="path">Destination path within storage.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <param name="checksum">Optional checksum for verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result with file metadata.</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [RequestSizeLimit(500 * 1024 * 1024)] // 500MB max
    public async Task<IActionResult> Upload(
        [FromQuery] string path,
        IFormFile file,
        [FromForm] bool overwrite = true,
        [FromForm] string? checksum = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Error = "No file uploaded" });
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { Error = "Path is required" });
        }

        _logger.LogInformation("Receiving file upload: {FileName} ({Size} bytes) to {Path}",
            file.FileName, file.Length, path);

        try
        {
            var result = await _storageService.SaveFileAsync(
                path,
                file.OpenReadStream(),
                overwrite,
                checksum,
                cancellationToken);

            if (!result.Success)
            {
                return result.Error?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true
                    ? Conflict(new { Error = result.Error })
                    : BadRequest(new { Error = result.Error });
            }

            return Ok(new FileUploadResponse
            {
                Path = result.Path!,
                Size = result.Size,
                Checksum = result.Checksum,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed for {Path}", path);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Downloads a file from storage.
    /// </summary>
    /// <param name="path">File path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File stream.</returns>
    [HttpGet("download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromQuery] string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { Error = "Path is required" });
        }

        try
        {
            var (stream, contentType, fileName) = await _storageService.GetFileAsync(path, cancellationToken);

            if (stream == null)
            {
                return NotFound(new { Error = "File not found" });
            }

            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { Error = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File download failed for {Path}", path);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the sync manifest for a directory.
    /// Used by agents to determine which files need synchronization.
    /// </summary>
    /// <param name="path">Directory path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest with file list and checksums.</returns>
    [HttpGet("manifest")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SyncManifest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetManifest(
        [FromQuery] string? path,
        CancellationToken cancellationToken = default)
    {
        path ??= string.Empty;

        try
        {
            var manifest = await _storageService.GetManifestAsync(path, cancellationToken);

            if (manifest == null)
            {
                return NotFound(new { Error = "Directory not found" });
            }

            return Ok(manifest);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(new { Error = "Directory not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate manifest for {Path}", path);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Serves a file from a sync directory.
    /// </summary>
    /// <param name="path">File path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File stream.</returns>
    [HttpGet("file/{*path}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (stream, contentType, fileName) = await _storageService.GetFileAsync(path, cancellationToken);

            if (stream == null)
            {
                return NotFound(new { Error = "File not found" });
            }

            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { Error = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serve file {Path}", path);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="path">File path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delete result.</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromQuery] string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { Error = "Path is required" });
        }

        try
        {
            var success = await _storageService.DeleteFileAsync(path, cancellationToken);

            if (!success)
            {
                return NotFound(new { Error = "File not found" });
            }

            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File deletion failed for {Path}", path);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}

/// <summary>
/// Response from file upload operation.
/// </summary>
public sealed record FileUploadResponse
{
    /// <summary>
    /// Whether the upload was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Server-side path where file was stored.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Size of the uploaded file in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// SHA256 checksum of the stored file.
    /// </summary>
    public string? Checksum { get; init; }
}

/// <summary>
/// Sync manifest containing file list and checksums.
/// </summary>
public sealed class SyncManifest
{
    /// <summary>
    /// List of files in the directory.
    /// </summary>
    public IList<SyncFileEntry> Files { get; } = new List<SyncFileEntry>();
}

/// <summary>
/// Entry in sync manifest.
/// </summary>
public sealed class SyncFileEntry
{
    /// <summary>
    /// Relative path from manifest root.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// SHA256 checksum of the file.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }
}
