using OrbitMesh.Update.Models;

namespace OrbitMesh.Update.Services;

/// <summary>
/// Service for checking GitHub releases.
/// </summary>
public interface IGitHubReleaseService
{
    /// <summary>
    /// Gets the latest release information for the configured product.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Release information, or null if no release found.</returns>
    Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a release asset to the specified path.
    /// </summary>
    /// <param name="release">Release to download.</param>
    /// <param name="destinationPath">Path to save the downloaded file.</param>
    /// <param name="progress">Optional progress callback (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadReleaseAsync(
        ReleaseInfo release,
        string destinationPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
