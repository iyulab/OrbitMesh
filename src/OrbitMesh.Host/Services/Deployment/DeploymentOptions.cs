namespace OrbitMesh.Host.Services.Deployment;

/// <summary>
/// Configuration options for the deployment service.
/// </summary>
public sealed class DeploymentOptions
{
    /// <summary>
    /// The base URL of the server for file downloads.
    /// Example: "http://localhost:5000"
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String is more practical for IOptions binding")]
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Maximum concurrent deployments per profile.
    /// </summary>
    public int MaxConcurrentDeployments { get; set; } = 5;

    /// <summary>
    /// Default timeout for script execution in seconds.
    /// </summary>
    public int DefaultScriptTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Default timeout for file sync operations in minutes.
    /// </summary>
    public int DefaultFileSyncTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to enable the deployment profile watcher service.
    /// </summary>
    public bool EnableFileWatching { get; set; } = true;

    /// <summary>
    /// Interval in seconds for checking and refreshing profile watchers.
    /// </summary>
    public int WatcherRefreshIntervalSeconds { get; set; } = 60;
}
