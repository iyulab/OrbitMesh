using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Update.Models;

namespace OrbitMesh.Update.Services;

/// <summary>
/// GitHub Releases API client for checking and downloading updates.
/// </summary>
public sealed class GitHubReleaseService : IGitHubReleaseService
{
    private readonly HttpClient _httpClient;
    private readonly UpdateOptions _options;
    private readonly ILogger<GitHubReleaseService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubReleaseService(
        HttpClient httpClient,
        IOptions<UpdateOptions> options,
        ILogger<GitHubReleaseService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Set required headers for GitHub API
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OrbitMesh-Updater/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = new Uri($"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/releases/latest");

            _logger.LogDebug("Checking for updates at {Url}", url);

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(content, JsonOptions);

            if (release is null)
            {
                _logger.LogWarning("Failed to parse release response");
                return null;
            }

            // Skip pre-releases if not configured
            if (release.Prerelease && !_options.IncludePrerelease)
            {
                _logger.LogDebug("Skipping pre-release {Tag}", release.TagName);
                return null;
            }

            // Find matching asset for current platform
            var assetName = GetExpectedAssetName();
            var asset = release.Assets?.FirstOrDefault(a =>
                a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                _logger.LogWarning("No matching asset found for {AssetName}", assetName);
                return null;
            }

            // Parse version from tag (remove 'v' prefix if present)
            var versionString = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(versionString, out var version))
            {
                _logger.LogWarning("Failed to parse version from tag {Tag}", release.TagName);
                return null;
            }

            return new ReleaseInfo
            {
                TagName = release.TagName,
                Version = version,
                DownloadUrl = asset.BrowserDownloadUrl,
                Size = asset.Size,
                PublishedAt = release.PublishedAt,
                Name = release.Name,
                IsPrerelease = release.Prerelease
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DownloadReleaseAsync(
        ReleaseInfo release,
        string destinationPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading update {Version} from {Url}", release.Version, release.DownloadUrl);

        using var response = await _httpClient.GetAsync(
            release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? release.Size;
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalBytesRead = 0;
        int bytesRead;
        var lastProgress = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            if (totalBytes > 0)
            {
                var currentProgress = (int)(totalBytesRead * 100 / totalBytes);
                if (currentProgress != lastProgress)
                {
                    lastProgress = currentProgress;
                    progress?.Report(currentProgress);
                }
            }
        }

        _logger.LogInformation("Download complete: {Bytes} bytes", totalBytesRead);
    }

    /// <summary>
    /// Gets the expected asset name for the current platform.
    /// </summary>
    private string GetExpectedAssetName()
    {
        var rid = GetRuntimeIdentifier();
        return $"{_options.ProductName}-{rid}.zip";
    }

    /// <summary>
    /// Gets the runtime identifier for the current platform.
    /// </summary>
    private static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx-x64";

        return "unknown";
    }

    #region GitHub API Models

    private sealed class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool Prerelease { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public Uri BrowserDownloadUrl { get; set; } = null!;
        public long Size { get; set; }
    }

    #endregion
}
