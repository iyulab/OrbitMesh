using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Platform;
using OrbitMesh.Update.Models;

namespace OrbitMesh.Update.Services;

/// <summary>
/// Manages application updates via GitHub Releases.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IGitHubReleaseService _releaseService;
    private readonly IPlatformPaths _paths;
    private readonly UpdateOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpdateService> _logger;

    private const string ManifestFileName = "manifest.json";
    private const string UpdateScriptWindows = "apply-update.ps1";
    private const string UpdateScriptUnix = "apply-update.sh";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UpdateService(
        IGitHubReleaseService releaseService,
        IPlatformPaths paths,
        IOptions<UpdateOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<UpdateService> logger)
    {
        _releaseService = releaseService;
        _paths = paths;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    public Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <inheritdoc />
    public async Task<UpdateResult> CheckAndApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipUpdateCheck)
        {
            _logger.LogDebug("Update check skipped (SkipUpdateCheck=true)");
            return UpdateResult.NoUpdate(CurrentVersion);
        }

        try
        {
            _logger.LogInformation("Checking for updates. Current version: {Version}", CurrentVersion);

            var release = await CheckForUpdateAsync(cancellationToken);
            if (release is null)
            {
                _logger.LogInformation("No update available");
                return UpdateResult.NoUpdate(CurrentVersion);
            }

            if (!_options.AutoUpdate)
            {
                _logger.LogInformation("Update available: {Version}. Auto-update disabled.", release.Version);
                return new UpdateResult
                {
                    UpdateAvailable = true,
                    CurrentVersion = CurrentVersion,
                    NewVersion = release.Version,
                    Success = true
                };
            }

            _logger.LogInformation("Update available: {Version}. Downloading...", release.Version);

            // Download update
            var zipPath = Path.Combine(_paths.UpdatePath, $"{_options.ProductName}-{release.Version}.zip");
            await _releaseService.DownloadReleaseAsync(release, zipPath, null, cancellationToken);

            // Extract to staging
            var stagingPath = Path.Combine(_paths.UpdatePath, "staging");
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, stagingPath), cancellationToken);

            // Backup current version
            await BackupCurrentVersionAsync(cancellationToken);

            // Create update script
            var scriptPath = CreateUpdateScript(stagingPath);

            // Clean up downloaded zip
            File.Delete(zipPath);

            _logger.LogInformation("Update prepared. Restarting to apply...");

            // Launch update script and exit
            LaunchUpdateScript(scriptPath);

            // Request graceful shutdown
            _lifetime.StopApplication();

            return UpdateResult.Pending(CurrentVersion, release.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");
            return UpdateResult.Failed(ex.Message, CurrentVersion);
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var release = await _releaseService.GetLatestReleaseAsync(cancellationToken);

        if (release is null)
            return null;

        // Compare versions
        if (release.Version <= CurrentVersion)
        {
            _logger.LogDebug("Current version {Current} is up to date (latest: {Latest})",
                CurrentVersion, release.Version);
            return null;
        }

        return release;
    }

    /// <inheritdoc />
    public async Task<UpdateResult> RollbackAsync(Version? version = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var backups = GetAvailableBackups();
            if (backups.Count == 0)
            {
                return UpdateResult.Failed("No backup versions available");
            }

            var targetVersion = version ?? backups[0];
            var backupPath = Path.Combine(_paths.BackupsPath, $"v{targetVersion}");

            if (!Directory.Exists(backupPath))
            {
                return UpdateResult.Failed($"Backup for version {targetVersion} not found");
            }

            _logger.LogInformation("Rolling back to version {Version}", targetVersion);

            // Backup current before rollback
            await BackupCurrentVersionAsync(cancellationToken);

            // Create rollback script (similar to update)
            var scriptPath = CreateUpdateScript(backupPath);

            // Launch script and exit
            LaunchUpdateScript(scriptPath);
            _lifetime.StopApplication();

            return UpdateResult.Pending(CurrentVersion, targetVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback");
            return UpdateResult.Failed(ex.Message, CurrentVersion);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Version> GetAvailableBackups()
    {
        if (!Directory.Exists(_paths.BackupsPath))
            return [];

        var versions = new List<Version>();
        foreach (var dir in Directory.GetDirectories(_paths.BackupsPath))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith('v') && Version.TryParse(name[1..], out var version))
            {
                versions.Add(version);
            }
        }

        return versions.OrderByDescending(v => v).ToList();
    }

    /// <summary>
    /// Backs up the current version to the backups directory.
    /// </summary>
    private async Task BackupCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath))
        {
            _logger.LogWarning("Could not determine current executable path");
            return;
        }

        var currentDir = Path.GetDirectoryName(currentExePath)!;
        var backupDir = Path.Combine(_paths.BackupsPath, $"v{CurrentVersion}");

        // Skip if already backed up
        if (Directory.Exists(backupDir))
        {
            _logger.LogDebug("Backup for version {Version} already exists", CurrentVersion);
            return;
        }

        _logger.LogInformation("Creating backup of version {Version}", CurrentVersion);

        Directory.CreateDirectory(backupDir);

        // Copy all files from current directory to backup
        foreach (var file in Directory.EnumerateFiles(currentDir))
        {
            var fileName = Path.GetFileName(file);
            var destPath = Path.Combine(backupDir, fileName);
            File.Copy(file, destPath, overwrite: true);
        }

        // Update manifest
        await UpdateManifestAsync(cancellationToken);

        // Cleanup old backups
        await CleanupOldBackupsAsync(cancellationToken);
    }

    /// <summary>
    /// Updates the backup manifest file.
    /// </summary>
    private async Task UpdateManifestAsync(CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(_paths.BackupsPath, ManifestFileName);
        var manifest = new BackupManifest
        {
            CurrentVersion = CurrentVersion.ToString(),
            PreviousVersions = GetAvailableBackups().Select(v => v.ToString()).ToList(),
            MaxBackups = _options.MaxBackupVersions
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    /// <summary>
    /// Removes old backups exceeding the maximum count.
    /// </summary>
    private Task CleanupOldBackupsAsync(CancellationToken cancellationToken)
    {
        var backups = GetAvailableBackups();
        if (backups.Count <= _options.MaxBackupVersions)
            return Task.CompletedTask;

        var toRemove = backups.Skip(_options.MaxBackupVersions);
        foreach (var version in toRemove)
        {
            var path = Path.Combine(_paths.BackupsPath, $"v{version}");
            if (Directory.Exists(path))
            {
                _logger.LogInformation("Removing old backup: v{Version}", version);
                Directory.Delete(path, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a platform-specific update script.
    /// </summary>
    private string CreateUpdateScript(string sourcePath)
    {
        var currentExePath = Environment.ProcessPath!;
        var currentDir = Path.GetDirectoryName(currentExePath)!;
        var exeName = Path.GetFileName(currentExePath);

        string scriptPath;
        string scriptContent;

        if (OperatingSystem.IsWindows())
        {
            scriptPath = Path.Combine(_paths.UpdatePath, UpdateScriptWindows);
            scriptContent = GenerateWindowsScript(sourcePath, currentDir, exeName);
        }
        else
        {
            scriptPath = Path.Combine(_paths.UpdatePath, UpdateScriptUnix);
            scriptContent = GenerateUnixScript(sourcePath, currentDir, exeName);
        }

        File.WriteAllText(scriptPath, scriptContent);

        if (!OperatingSystem.IsWindows())
        {
            // Make script executable on Unix
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return scriptPath;
    }

    /// <summary>
    /// Generates a PowerShell update script for Windows.
    /// </summary>
    private static string GenerateWindowsScript(string sourcePath, string targetDir, string exeName)
    {
        return $@"
# OrbitMesh Update Script
# Wait for the application to exit
Start-Sleep -Seconds 2

# Copy new files
$source = ""{sourcePath}""
$target = ""{targetDir}""

# Find the product directory inside staging (orbit-host or orbit-node)
$productDir = Get-ChildItem -Path $source -Directory | Select-Object -First 1

if ($productDir) {{
    Copy-Item -Path ""$($productDir.FullName)\*"" -Destination $target -Recurse -Force
}} else {{
    Copy-Item -Path ""$source\*"" -Destination $target -Recurse -Force
}}

# Cleanup staging
Remove-Item -Path ""{Path.GetDirectoryName(sourcePath)}"" -Recurse -Force -ErrorAction SilentlyContinue

# Restart application
Start-Process -FilePath ""$target\{exeName}""

# Self-delete
Remove-Item -Path $MyInvocation.MyCommand.Path -Force
";
    }

    /// <summary>
    /// Generates a Bash update script for Unix systems.
    /// </summary>
    private static string GenerateUnixScript(string sourcePath, string targetDir, string exeName)
    {
        return $@"#!/bin/bash
# OrbitMesh Update Script

# Wait for the application to exit
sleep 2

SOURCE=""{sourcePath}""
TARGET=""{targetDir}""

# Find the product directory inside staging
PRODUCT_DIR=$(find ""$SOURCE"" -maxdepth 1 -type d | tail -1)

if [ -d ""$PRODUCT_DIR"" ] && [ ""$PRODUCT_DIR"" != ""$SOURCE"" ]; then
    cp -rf ""$PRODUCT_DIR/""* ""$TARGET/""
else
    cp -rf ""$SOURCE/""* ""$TARGET/""
fi

# Cleanup staging
rm -rf ""{Path.GetDirectoryName(sourcePath)}""

# Restart application
""$TARGET/{exeName}"" &

# Self-delete
rm -- ""$0""
";
    }

    /// <summary>
    /// Launches the update script as a detached process.
    /// </summary>
    private static void LaunchUpdateScript(string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"";
        }
        else
        {
            startInfo.FileName = "/bin/bash";
            startInfo.Arguments = $"\"{scriptPath}\"";
        }

        Process.Start(startInfo);
    }

    private sealed class BackupManifest
    {
        public string CurrentVersion { get; set; } = string.Empty;
        public List<string> PreviousVersions { get; set; } = [];
        public int MaxBackups { get; set; }
    }
}
