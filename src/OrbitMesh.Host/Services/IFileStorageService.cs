using OrbitMesh.Host.Controllers;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Service interface for file storage operations.
/// Provides file upload, download, and manifest generation for sync operations.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves a file to storage.
    /// </summary>
    /// <param name="path">Destination path within storage.</param>
    /// <param name="content">File content stream.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <param name="expectedChecksum">Optional expected checksum for verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing file metadata.</returns>
    Task<FileSaveResult> SaveFileAsync(
        string path,
        Stream content,
        bool overwrite = true,
        string? expectedChecksum = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a file from storage.
    /// </summary>
    /// <param name="path">File path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (stream, content type, file name), or (null, null, null) if not found.</returns>
    Task<(Stream? Stream, string ContentType, string FileName)> GetFileAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="path">File path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteFileAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a sync manifest for a directory.
    /// </summary>
    /// <param name="path">Directory path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest containing file list and checksums.</returns>
    Task<SyncManifest?> GetManifestAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file or directory exists.
    /// </summary>
    /// <param name="path">Path within storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists.</returns>
    Task<bool> ExistsAsync(
        string path,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a file save operation.
/// </summary>
public sealed record FileSaveResult
{
    /// <summary>
    /// Whether the save was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Server-side path where file was stored.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Size of the saved file in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// SHA256 checksum of the saved file.
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Error message if save failed.
    /// </summary>
    public string? Error { get; init; }
}
