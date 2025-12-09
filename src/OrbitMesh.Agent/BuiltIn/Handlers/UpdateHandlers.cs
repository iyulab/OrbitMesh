using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OrbitMesh.Agent.BuiltIn.Models;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Agent.BuiltIn.Handlers;

/// <summary>
/// Handler for checking available updates.
/// </summary>
public sealed class UpdateCheckHandler : IRequestResponseHandler<CheckUpdateResult>
{
    private readonly HttpClient _httpClient;
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateCheckHandler> _logger;

    public string Command => Commands.Update.Check;

    public UpdateCheckHandler(
        HttpClient httpClient,
        IUpdateService updateService,
        ILogger<UpdateCheckHandler> logger)
    {
        _httpClient = httpClient;
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<CheckUpdateResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<CheckUpdateRequest>();

        _logger.LogInformation("Checking for updates. Current version: {Version}, Platform: {Platform}",
            request.CurrentVersion, request.Platform);

        try
        {
            var package = await _updateService.CheckForUpdateAsync(
                request.CurrentVersion,
                request.Platform,
                request.Channel,
                request.IncludePreRelease,
                cancellationToken);

            if (package == null)
            {
                return new CheckUpdateResult
                {
                    UpdateAvailable = false,
                    IsCompatible = true
                };
            }

            // Check compatibility
            var isCompatible = true;
            string? incompatibilityReason = null;

            if (!string.IsNullOrEmpty(package.MinimumVersion))
            {
                var current = Version.Parse(request.CurrentVersion);
                var minimum = Version.Parse(package.MinimumVersion);

                if (current < minimum)
                {
                    isCompatible = false;
                    incompatibilityReason = $"Current version {request.CurrentVersion} is below minimum required {package.MinimumVersion}";
                }
            }

            return new CheckUpdateResult
            {
                UpdateAvailable = true,
                Package = package,
                IsCompatible = isCompatible,
                IncompatibilityReason = incompatibilityReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return new CheckUpdateResult
            {
                UpdateAvailable = false,
                IsCompatible = false,
                IncompatibilityReason = ex.Message
            };
        }
    }
}

/// <summary>
/// Handler for downloading update packages.
/// </summary>
public sealed class UpdateDownloadHandler : IRequestResponseHandler<DownloadUpdateResult>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateDownloadHandler> _logger;

    public string Command => Commands.Update.Download;

    public UpdateDownloadHandler(HttpClient httpClient, ILogger<UpdateDownloadHandler> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DownloadUpdateResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<DownloadUpdateRequest>();
        var package = request.Package;

        _logger.LogInformation("Downloading update package {Id} v{Version} from {Url}",
            package.Id, package.Version, package.DownloadUrl);

        try
        {
            // Determine target directory
            var targetDir = request.TargetDirectory
                ?? Path.Combine(Path.GetTempPath(), "orbit-updates", package.Id);

            Directory.CreateDirectory(targetDir);

            var fileName = $"{package.Id}-{package.Version}.zip";
            var localPath = Path.Combine(targetDir, fileName);

            // Download the package
            using var response = await _httpClient.GetAsync(
                new Uri(package.DownloadUrl),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(localPath);
            await stream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            // Verify checksum
            var actualChecksum = await ComputeChecksumAsync(localPath, cancellationToken);
            var checksumVerified = string.Equals(package.Checksum, actualChecksum, StringComparison.OrdinalIgnoreCase);

            if (!checksumVerified)
            {
                _logger.LogWarning("Checksum mismatch for {PackageId}. Expected: {Expected}, Actual: {Actual}",
                    package.Id, package.Checksum, actualChecksum);

                // Delete corrupted file
                File.Delete(localPath);

                return new DownloadUpdateResult
                {
                    Success = false,
                    ChecksumVerified = false,
                    Error = "Checksum verification failed"
                };
            }

            _logger.LogInformation("Successfully downloaded update to {Path}", localPath);

            return new DownloadUpdateResult
            {
                Success = true,
                LocalPath = localPath,
                ChecksumVerified = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update package {PackageId}", package.Id);
            return new DownloadUpdateResult
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
/// Handler for applying downloaded updates.
/// </summary>
public sealed class UpdateApplyHandler : IRequestResponseHandler<ApplyUpdateResult>
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateApplyHandler> _logger;

    public string Command => Commands.Update.Apply;

    public UpdateApplyHandler(IUpdateService updateService, ILogger<UpdateApplyHandler> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<ApplyUpdateResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<ApplyUpdateRequest>();

        _logger.LogInformation("Applying update from {Path} to version {Version}",
            request.PackagePath, request.TargetVersion);

        string? backupPath = null;

        try
        {
            // Get current version
            var currentVersion = _updateService.GetCurrentVersion();

            // Create backup if requested
            if (request.CreateBackup)
            {
                backupPath = await _updateService.CreateBackupAsync(cancellationToken);
                _logger.LogInformation("Backup created at {BackupPath}", backupPath);
            }

            // Apply the update
            await _updateService.ApplyUpdateAsync(request.PackagePath, cancellationToken);

            _logger.LogInformation("Update applied successfully. Version: {OldVersion} -> {NewVersion}",
                currentVersion, request.TargetVersion);

            // Restart if requested
            if (request.RestartAfterApply)
            {
                _logger.LogInformation("Scheduling restart...");
                _updateService.ScheduleRestart();
            }

            return new ApplyUpdateResult
            {
                Success = true,
                NewVersion = request.TargetVersion,
                PreviousVersion = currentVersion,
                BackupPath = backupPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");

            // Attempt rollback if backup exists
            if (backupPath != null)
            {
                try
                {
                    _logger.LogWarning("Attempting automatic rollback from {BackupPath}", backupPath);
                    await _updateService.RollbackAsync(backupPath, cancellationToken);
                    _logger.LogInformation("Rollback completed successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Automatic rollback also failed");
                }
            }

            return new ApplyUpdateResult
            {
                Success = false,
                BackupPath = backupPath,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Handler for rollback to previous version.
/// </summary>
public sealed class UpdateRollbackHandler : IRequestResponseHandler<RollbackResult>
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateRollbackHandler> _logger;

    public string Command => Commands.Update.Rollback;

    public UpdateRollbackHandler(IUpdateService updateService, ILogger<UpdateRollbackHandler> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<RollbackResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<RollbackRequest>();

        _logger.LogInformation("Rolling back. Reason: {Reason}", request.Reason ?? "Manual rollback");

        try
        {
            string backupPath;

            if (!string.IsNullOrEmpty(request.BackupPath))
            {
                backupPath = request.BackupPath;
            }
            else if (!string.IsNullOrEmpty(request.TargetVersion))
            {
                backupPath = _updateService.GetBackupPath(request.TargetVersion);
            }
            else
            {
                backupPath = _updateService.GetLatestBackupPath();
            }

            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
            {
                return new RollbackResult
                {
                    Success = false,
                    Error = "No backup available for rollback"
                };
            }

            await _updateService.RollbackAsync(backupPath, cancellationToken);

            var restoredVersion = _updateService.GetVersionFromBackup(backupPath);

            _logger.LogInformation("Rollback completed. Restored version: {Version}", restoredVersion);

            return new RollbackResult
            {
                Success = true,
                RestoredVersion = restoredVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed");
            return new RollbackResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Handler for getting update status.
/// </summary>
public sealed class UpdateStatusHandler : IRequestResponseHandler<UpdateStatus>
{
    private readonly IUpdateService _updateService;

    public string Command => Commands.Update.Status;

    public UpdateStatusHandler(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public Task<UpdateStatus> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var status = _updateService.GetCurrentStatus();
        return Task.FromResult(status);
    }
}

/// <summary>
/// Interface for update service operations.
/// Implement this interface to provide custom update logic.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Check if an update is available.
    /// </summary>
    Task<UpdatePackage?> CheckForUpdateAsync(
        string currentVersion,
        string platform,
        string channel,
        bool includePreRelease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current installed version.
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Create a backup of the current installation.
    /// </summary>
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply an update from the specified package path.
    /// </summary>
    Task ApplyUpdateAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to a backup.
    /// </summary>
    Task RollbackAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule a restart of the application.
    /// </summary>
    void ScheduleRestart();

    /// <summary>
    /// Get the backup path for a specific version.
    /// </summary>
    string GetBackupPath(string version);

    /// <summary>
    /// Get the path to the latest backup.
    /// </summary>
    string GetLatestBackupPath();

    /// <summary>
    /// Get version information from a backup.
    /// </summary>
    string GetVersionFromBackup(string backupPath);

    /// <summary>
    /// Get the current update status.
    /// </summary>
    UpdateStatus GetCurrentStatus();
}

/// <summary>
/// Default implementation of IUpdateService for file-based updates.
/// </summary>
public class DefaultUpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _updateServerUrl;
    private readonly string _applicationPath;
    private readonly string _backupBasePath;
    private readonly ILogger<DefaultUpdateService> _logger;

    private UpdateStatus _currentStatus = new()
    {
        Phase = UpdatePhase.Idle,
        Progress = 0,
        CurrentVersion = "1.0.0"
    };

    public DefaultUpdateService(
        HttpClient httpClient,
        string updateServerUrl,
        string applicationPath,
        ILogger<DefaultUpdateService> logger)
    {
        _httpClient = httpClient;
        _updateServerUrl = updateServerUrl;
        _applicationPath = applicationPath;
        _backupBasePath = Path.Combine(applicationPath, ".backups");
        _logger = logger;
    }

    public async Task<UpdatePackage?> CheckForUpdateAsync(
        string currentVersion,
        string platform,
        string channel,
        bool includePreRelease,
        CancellationToken cancellationToken = default)
    {
        _currentStatus = _currentStatus with
        {
            Phase = UpdatePhase.CheckingForUpdates,
            CurrentVersion = currentVersion
        };

        try
        {
            var manifestUrl = new Uri($"{_updateServerUrl}/updates/{channel}/{platform}/manifest.json");
            var response = await _httpClient.GetAsync(manifestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<UpdateManifest>(json);

            if (manifest?.LatestVersion == null)
            {
                return null;
            }

            var current = Version.Parse(currentVersion);
            var latest = Version.Parse(manifest.LatestVersion);

            if (latest <= current)
            {
                return null;
            }

            // Find the package for the latest version
            var package = manifest.Packages?.FirstOrDefault(p => p.Version == manifest.LatestVersion);

            return package;
        }
        finally
        {
            _currentStatus = _currentStatus with { Phase = UpdatePhase.Idle };
        }
    }

    public string GetCurrentVersion()
    {
        var versionFile = Path.Combine(_applicationPath, "version.txt");
        if (File.Exists(versionFile))
        {
            return File.ReadAllText(versionFile).Trim();
        }

        return typeof(DefaultUpdateService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        _currentStatus = _currentStatus with { Phase = UpdatePhase.CreatingBackup };

        try
        {
            var version = GetCurrentVersion();
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var backupPath = Path.Combine(_backupBasePath, $"{version}_{timestamp}");

            Directory.CreateDirectory(backupPath);

            // Copy all files to backup
            foreach (var file in Directory.EnumerateFiles(_applicationPath, "*", SearchOption.AllDirectories))
            {
                if (file.StartsWith(_backupBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(_applicationPath, file);
                var destPath = Path.Combine(backupPath, relativePath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                await Task.Run(() => File.Copy(file, destPath, true), cancellationToken);
            }

            // Save version info
            await File.WriteAllTextAsync(
                Path.Combine(backupPath, "version.txt"),
                version,
                cancellationToken);

            return backupPath;
        }
        finally
        {
            _currentStatus = _currentStatus with { Phase = UpdatePhase.Idle };
        }
    }

    public async Task ApplyUpdateAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        _currentStatus = _currentStatus with { Phase = UpdatePhase.Applying };

        try
        {
            var extractPath = Path.Combine(Path.GetTempPath(), "orbit-update-extract", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractPath);

            // Extract package
            await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, extractPath, overwriteFiles: true), cancellationToken);

            // Copy files to application directory
            foreach (var file in Directory.EnumerateFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(extractPath, file);
                var destPath = Path.Combine(_applicationPath, relativePath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                await Task.Run(() => File.Copy(file, destPath, true), cancellationToken);
            }

            // Cleanup
            Directory.Delete(extractPath, recursive: true);

            _currentStatus = _currentStatus with { Phase = UpdatePhase.Completed };
        }
        catch
        {
            _currentStatus = _currentStatus with { Phase = UpdatePhase.Failed };
            throw;
        }
    }

    public async Task RollbackAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        _currentStatus = _currentStatus with { Phase = UpdatePhase.RollingBack };

        try
        {
            foreach (var file in Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith("version.txt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(backupPath, file);
                var destPath = Path.Combine(_applicationPath, relativePath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                await Task.Run(() => File.Copy(file, destPath, true), cancellationToken);
            }

            _currentStatus = _currentStatus with { Phase = UpdatePhase.Completed };
        }
        catch
        {
            _currentStatus = _currentStatus with { Phase = UpdatePhase.Failed };
            throw;
        }
    }

    public void ScheduleRestart()
    {
        // Schedule a restart in 5 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(exePath);
            }

            Environment.Exit(0);
        });
    }

    public string GetBackupPath(string version)
    {
        if (!Directory.Exists(_backupBasePath))
        {
            return string.Empty;
        }

        var backups = Directory.EnumerateDirectories(_backupBasePath)
            .Where(d => Path.GetFileName(d).StartsWith(version, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d)
            .FirstOrDefault();

        return backups ?? string.Empty;
    }

    public string GetLatestBackupPath()
    {
        if (!Directory.Exists(_backupBasePath))
        {
            return string.Empty;
        }

        return Directory.EnumerateDirectories(_backupBasePath)
            .OrderByDescending(d => d)
            .FirstOrDefault() ?? string.Empty;
    }

    public string GetVersionFromBackup(string backupPath)
    {
        var versionFile = Path.Combine(backupPath, "version.txt");
        if (File.Exists(versionFile))
        {
            return File.ReadAllText(versionFile).Trim();
        }

        // Try to parse from directory name
        var dirName = Path.GetFileName(backupPath);
        var parts = dirName?.Split('_');
        return parts?.FirstOrDefault() ?? "unknown";
    }

    public UpdateStatus GetCurrentStatus()
    {
        return _currentStatus;
    }
}

/// <summary>
/// Update manifest structure.
/// </summary>
internal sealed class UpdateManifest
{
    public string? LatestVersion { get; set; }
    public List<UpdatePackage>? Packages { get; set; }
}
