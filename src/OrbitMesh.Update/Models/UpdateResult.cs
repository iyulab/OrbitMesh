namespace OrbitMesh.Update.Models;

/// <summary>
/// Result of an update check or application operation.
/// </summary>
public sealed record UpdateResult
{
    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Whether an update is pending application (requires restart).
    /// </summary>
    public bool UpdatePending { get; init; }

    /// <summary>
    /// Whether the update was successfully applied.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Current version of the application.
    /// </summary>
    public Version? CurrentVersion { get; init; }

    /// <summary>
    /// New version available (if any).
    /// </summary>
    public Version? NewVersion { get; init; }

    /// <summary>
    /// Error message if the update failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a result indicating no update is available.
    /// </summary>
    public static UpdateResult NoUpdate(Version currentVersion) => new()
    {
        UpdateAvailable = false,
        Success = true,
        CurrentVersion = currentVersion
    };

    /// <summary>
    /// Creates a result indicating an update is pending.
    /// </summary>
    public static UpdateResult Pending(Version currentVersion, Version newVersion) => new()
    {
        UpdateAvailable = true,
        UpdatePending = true,
        Success = true,
        CurrentVersion = currentVersion,
        NewVersion = newVersion
    };

    /// <summary>
    /// Creates a result indicating an error occurred.
    /// </summary>
    public static UpdateResult Failed(string error, Version? currentVersion = null) => new()
    {
        Success = false,
        Error = error,
        CurrentVersion = currentVersion
    };
}
