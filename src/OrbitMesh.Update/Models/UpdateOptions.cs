namespace OrbitMesh.Update.Models;

/// <summary>
/// Configuration options for the update service.
/// </summary>
public sealed class UpdateOptions
{
    /// <summary>
    /// GitHub repository owner.
    /// </summary>
    public string Owner { get; set; } = "iyulab";

    /// <summary>
    /// GitHub repository name.
    /// </summary>
    public string Repository { get; set; } = "OrbitMesh";

    /// <summary>
    /// Product name for asset matching (e.g., "orbit-host" or "orbit-node").
    /// </summary>
    public string ProductName { get; set; } = "orbit-host";

    /// <summary>
    /// Whether to automatically apply updates on startup.
    /// </summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    /// Whether to check for pre-release versions.
    /// </summary>
    public bool IncludePrerelease { get; set; }

    /// <summary>
    /// Maximum number of backup versions to keep.
    /// </summary>
    public int MaxBackupVersions { get; set; } = 2;

    /// <summary>
    /// Timeout for download operations.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether to skip update check (for development).
    /// </summary>
    public bool SkipUpdateCheck { get; set; }
}
