using MessagePack;

namespace OrbitMesh.Node.BuiltIn.Models;

/// <summary>
/// Update package information.
/// </summary>
[MessagePackObject]
public sealed record UpdatePackage
{
    /// <summary>
    /// Package identifier.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Package version.
    /// </summary>
    [Key(1)]
    public required string Version { get; init; }

    /// <summary>
    /// Download URL.
    /// </summary>
    [Key(2)]
#pragma warning disable CA1056 // URI properties should not be strings (MessagePack serialization)
    public required string DownloadUrl { get; init; }
#pragma warning restore CA1056

    /// <summary>
    /// SHA256 checksum.
    /// </summary>
    [Key(3)]
    public required string Checksum { get; init; }

    /// <summary>
    /// Package size in bytes.
    /// </summary>
    [Key(4)]
    public long Size { get; init; }

    /// <summary>
    /// Release notes.
    /// </summary>
    [Key(5)]
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Minimum required version for upgrade.
    /// </summary>
    [Key(6)]
    public string? MinimumVersion { get; init; }

    /// <summary>
    /// Whether this is a critical update.
    /// </summary>
    [Key(7)]
    public bool IsCritical { get; init; }

    /// <summary>
    /// Release date.
    /// </summary>
    [Key(8)]
    public DateTimeOffset ReleasedAt { get; init; }
}

/// <summary>
/// Check update request.
/// </summary>
[MessagePackObject]
public sealed record CheckUpdateRequest
{
    /// <summary>
    /// Current installed version.
    /// </summary>
    [Key(0)]
    public required string CurrentVersion { get; init; }

    /// <summary>
    /// Platform identifier.
    /// </summary>
    [Key(1)]
    public required string Platform { get; init; }

    /// <summary>
    /// Include pre-release versions.
    /// </summary>
    [Key(2)]
    public bool IncludePreRelease { get; init; }

    /// <summary>
    /// Update channel (stable, beta, etc.).
    /// </summary>
    [Key(3)]
    public string Channel { get; init; } = "stable";
}

/// <summary>
/// Check update result.
/// </summary>
[MessagePackObject]
public sealed record CheckUpdateResult
{
    /// <summary>
    /// Whether an update is available.
    /// </summary>
    [Key(0)]
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Available package.
    /// </summary>
    [Key(1)]
    public UpdatePackage? Package { get; init; }

    /// <summary>
    /// Whether the update is compatible.
    /// </summary>
    [Key(2)]
    public bool IsCompatible { get; init; }

    /// <summary>
    /// Incompatibility reason if not compatible.
    /// </summary>
    [Key(3)]
    public string? IncompatibilityReason { get; init; }
}

/// <summary>
/// Download update request.
/// </summary>
[MessagePackObject]
public sealed record DownloadUpdateRequest
{
    /// <summary>
    /// Package to download.
    /// </summary>
    [Key(0)]
    public required UpdatePackage Package { get; init; }

    /// <summary>
    /// Target directory for download.
    /// </summary>
    [Key(1)]
    public string? TargetDirectory { get; init; }
}

/// <summary>
/// Download update result.
/// </summary>
[MessagePackObject]
public sealed record DownloadUpdateResult
{
    /// <summary>
    /// Whether download succeeded.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Local path to downloaded file.
    /// </summary>
    [Key(1)]
    public string? LocalPath { get; init; }

    /// <summary>
    /// Whether checksum was verified.
    /// </summary>
    [Key(2)]
    public bool ChecksumVerified { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }
}

/// <summary>
/// Apply update request.
/// </summary>
[MessagePackObject]
public sealed record ApplyUpdateRequest
{
    /// <summary>
    /// Path to the downloaded package.
    /// </summary>
    [Key(0)]
    public required string PackagePath { get; init; }

    /// <summary>
    /// Target version being installed.
    /// </summary>
    [Key(1)]
    public required string TargetVersion { get; init; }

    /// <summary>
    /// Whether to create a backup.
    /// </summary>
    [Key(2)]
    public bool CreateBackup { get; init; } = true;

    /// <summary>
    /// Whether to restart after applying.
    /// </summary>
    [Key(3)]
    public bool RestartAfterApply { get; init; } = true;
}

/// <summary>
/// Apply update result.
/// </summary>
[MessagePackObject]
public sealed record ApplyUpdateResult
{
    /// <summary>
    /// Whether apply succeeded.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// New version after apply.
    /// </summary>
    [Key(1)]
    public string? NewVersion { get; init; }

    /// <summary>
    /// Previous version.
    /// </summary>
    [Key(2)]
    public string? PreviousVersion { get; init; }

    /// <summary>
    /// Backup path if created.
    /// </summary>
    [Key(3)]
    public string? BackupPath { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(4)]
    public string? Error { get; init; }
}

/// <summary>
/// Rollback request.
/// </summary>
[MessagePackObject]
public sealed record RollbackRequest
{
    /// <summary>
    /// Backup path to restore from.
    /// </summary>
    [Key(0)]
    public string? BackupPath { get; init; }

    /// <summary>
    /// Target version to rollback to.
    /// </summary>
    [Key(1)]
    public string? TargetVersion { get; init; }

    /// <summary>
    /// Reason for rollback.
    /// </summary>
    [Key(2)]
    public string? Reason { get; init; }
}

/// <summary>
/// Rollback result.
/// </summary>
[MessagePackObject]
public sealed record RollbackResult
{
    /// <summary>
    /// Whether rollback succeeded.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Restored version.
    /// </summary>
    [Key(1)]
    public string? RestoredVersion { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(2)]
    public string? Error { get; init; }
}

/// <summary>
/// Update status.
/// </summary>
[MessagePackObject]
public sealed record UpdateStatus
{
    /// <summary>
    /// Current update phase.
    /// </summary>
    [Key(0)]
    public UpdatePhase Phase { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [Key(1)]
    public int Progress { get; init; }

    /// <summary>
    /// Status message.
    /// </summary>
    [Key(2)]
    public string? Message { get; init; }

    /// <summary>
    /// Current version.
    /// </summary>
    [Key(3)]
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Target version being installed.
    /// </summary>
    [Key(4)]
    public string? TargetVersion { get; init; }

    /// <summary>
    /// When update started.
    /// </summary>
    [Key(5)]
    public DateTimeOffset? StartedAt { get; init; }
}

/// <summary>
/// Update phase.
/// </summary>
public enum UpdatePhase
{
    Idle = 0,
    CheckingForUpdates = 1,
    Downloading = 2,
    Verifying = 3,
    StoppingService = 4,
    CreatingBackup = 5,
    Applying = 6,
    StartingService = 7,
    VerifyingHealth = 8,
    Completed = 9,
    Failed = 10,
    RollingBack = 11
}
