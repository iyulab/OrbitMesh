using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;

namespace OrbitMesh.Host.Services.Deployment;

/// <summary>
/// Background service that watches source folders for file changes and triggers deployments.
/// </summary>
public sealed class DeploymentProfileWatcherService : BackgroundService
{
    private readonly IDeploymentProfileStore _profileStore;
    private readonly IDeploymentService _deploymentService;
    private readonly ILogger<DeploymentProfileWatcherService> _logger;

    private readonly ConcurrentDictionary<string, ProfileWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _debounceTimers = new();
    private readonly object _debounceLock = new();

    private Timer? _refreshTimer;

    public DeploymentProfileWatcherService(
        IDeploymentProfileStore profileStore,
        IDeploymentService deploymentService,
        ILogger<DeploymentProfileWatcherService> logger)
    {
        _profileStore = profileStore;
        _deploymentService = deploymentService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment profile watcher service starting");

        // Initial load of watchers
        await RefreshWatchersAsync(stoppingToken);

        // Periodically refresh watchers (in case profiles are added/removed)
        _refreshTimer = new Timer(
            async _ => await RefreshWatchersAsync(stoppingToken),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deployment profile watcher service stopping");

        // Dispose timer
        if (_refreshTimer is not null)
        {
            await _refreshTimer.DisposeAsync();
            _refreshTimer = null;
        }

        // Dispose all watchers
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();

        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _refreshTimer?.Dispose();
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
        base.Dispose();
    }

    private async Task RefreshWatchersAsync(CancellationToken ct)
    {
        try
        {
            var profiles = await _profileStore.GetWatchingAsync(ct);
            var profileIds = profiles.Select(p => p.Id).ToHashSet();

            // Remove watchers for profiles that no longer exist or have watching disabled
            var toRemove = _watchers.Keys.Except(profileIds).ToList();
            foreach (var profileId in toRemove)
            {
                if (_watchers.TryRemove(profileId, out var watcher))
                {
                    _logger.LogDebug("Removing watcher for profile '{ProfileId}'", profileId);
                    watcher.Dispose();
                }
            }

            // Add watchers for new profiles
            foreach (var profile in profiles)
            {
                if (!_watchers.ContainsKey(profile.Id))
                {
                    await AddWatcherAsync(profile, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing deployment watchers");
        }
    }

    private Task AddWatcherAsync(DeploymentProfile profile, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(profile.SourcePath))
        {
            _logger.LogWarning(
                "Cannot create watcher for profile '{ProfileName}': SourcePath is empty",
                profile.Name);
            return Task.CompletedTask;
        }

        if (!Directory.Exists(profile.SourcePath))
        {
            _logger.LogWarning(
                "Cannot create watcher for profile '{ProfileName}': SourcePath '{SourcePath}' does not exist",
                profile.Name, profile.SourcePath);
            return Task.CompletedTask;
        }

        try
        {
            var watcher = new FileSystemWatcher(profile.SourcePath)
            {
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += (_, e) => OnFileChanged(profile.Id, e);
            watcher.Created += (_, e) => OnFileChanged(profile.Id, e);
            watcher.Deleted += (_, e) => OnFileChanged(profile.Id, e);
            watcher.Renamed += (_, e) => OnFileChanged(profile.Id, e);
            watcher.Error += (_, e) => OnWatcherError(profile.Id, e);

            var profileWatcher = new ProfileWatcher(profile.Id, profile.DebounceMs, watcher);
            _watchers.TryAdd(profile.Id, profileWatcher);

            _logger.LogInformation(
                "Started watching '{SourcePath}' for profile '{ProfileName}'",
                profile.SourcePath, profile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create watcher for profile '{ProfileName}' at '{SourcePath}'",
                profile.Name, profile.SourcePath);
        }

        return Task.CompletedTask;
    }

    private void OnFileChanged(string profileId, FileSystemEventArgs e)
    {
        if (!_watchers.TryGetValue(profileId, out var profileWatcher))
        {
            return;
        }

        lock (_debounceLock)
        {
            var now = DateTimeOffset.UtcNow;
            _debounceTimers[profileId] = now;

            // Schedule debounced deployment
            _ = Task.Run(async () =>
            {
                await Task.Delay(profileWatcher.DebounceMs);

                // Check if this is still the latest change
                if (_debounceTimers.TryGetValue(profileId, out var lastChange) && lastChange == now)
                {
                    await TriggerDeploymentAsync(profileId, e.FullPath);
                }
            });
        }
    }

    private void OnWatcherError(string profileId, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(),
            "File watcher error for profile '{ProfileId}'",
            profileId);

        // Try to recreate the watcher
        if (_watchers.TryRemove(profileId, out var watcher))
        {
            watcher.Dispose();
        }
    }

    private async Task TriggerDeploymentAsync(string profileId, string changedPath)
    {
        try
        {
            _logger.LogInformation(
                "File change detected at '{Path}', triggering deployment for profile '{ProfileId}'",
                changedPath, profileId);

            await _deploymentService.DeployAsync(profileId, DeploymentTrigger.FileWatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to trigger deployment for profile '{ProfileId}' after file change",
                profileId);
        }
    }

    private sealed class ProfileWatcher : IDisposable
    {
        public string ProfileId { get; }
        public int DebounceMs { get; }
        private readonly FileSystemWatcher _watcher;

        public ProfileWatcher(string profileId, int debounceMs, FileSystemWatcher watcher)
        {
            ProfileId = profileId;
            DebounceMs = debounceMs;
            _watcher = watcher;
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
