using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// File synchronization service that handles bidirectional sync between server and agents.
/// </summary>
public sealed class FileSyncService : IFileSyncService, IHostedService
{
    private readonly IServerFileWatcherService _fileWatcher;
    private readonly IFileStorageService _fileStorage;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<FileSyncService> _logger;
    private readonly FileSyncOptions _options;

    private bool _isRunning;
    private long _totalFilesSynced;
    private DateTimeOffset? _lastSyncTime;

    /// <inheritdoc />
    public event EventHandler<FileSyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// Creates a new file sync service.
    /// </summary>
    public FileSyncService(
        IServerFileWatcherService fileWatcher,
        IFileStorageService fileStorage,
        IJobDispatcher jobDispatcher,
        IAgentRegistry agentRegistry,
        ILogger<FileSyncService> logger,
        FileSyncOptions options)
    {
        _fileWatcher = fileWatcher;
        _fileStorage = fileStorage;
        _jobDispatcher = jobDispatcher;
        _agentRegistry = agentRegistry;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting file sync service...");

        // Subscribe to file watcher events
        _fileWatcher.FileChanged += OnServerFileChanged;

        // Start watching if configured
        if (_options.WatchEnabled && !string.IsNullOrEmpty(_options.WatchPath))
        {
            _fileWatcher.StartWatch(
                _options.WatchPath,
                _options.WatchPattern,
                _options.IncludeSubdirectories);
        }

        _isRunning = true;
        _logger.LogInformation("File sync service started. WatchPath: {WatchPath}", _options.WatchPath);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping file sync service...");

        _fileWatcher.FileChanged -= OnServerFileChanged;
        _fileWatcher.StopWatch();

        _isRunning = false;
        _logger.LogInformation("File sync service stopped");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<FileSyncResult> SyncToAgentsAsync(
        string agentPattern,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Syncing to agents. Pattern: {Pattern}, File: {File}",
            agentPattern, filePath ?? "all");

        try
        {
            // Get matching agents
            var agents = await GetMatchingAgentsAsync(agentPattern, cancellationToken);
            if (agents.Count == 0)
            {
                _logger.LogWarning("No agents matched pattern: {Pattern}", agentPattern);
                return new FileSyncResult
                {
                    Success = false,
                    Error = $"No agents matched pattern: {agentPattern}"
                };
            }

            var results = new List<FileSyncFileResult>();
            var successCount = 0;

            foreach (var agent in agents)
            {
                try
                {
                    // Create sync job for each agent
                    var payload = new
                    {
                        sourceUrl = $"{_options.ServerUrl}/api/files",
                        sourcePath = filePath ?? ".",
                        destinationPath = _options.AgentSyncPath,
                        deleteOrphans = _options.DeleteOrphans
                    };

                    var request = JobRequest.Create("orbit:file.sync") with
                    {
                        TargetAgentId = agent.Id,
                        Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                        Timeout = TimeSpan.FromMinutes(5)
                    };

                    await _jobDispatcher.EnqueueAsync(request, cancellationToken);
                    successCount++;

                    _logger.LogDebug("Sync job dispatched to agent: {AgentName}", agent.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch sync to agent: {AgentId}", agent.Id);
                    results.Add(new FileSyncFileResult
                    {
                        Path = filePath ?? "*",
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            Interlocked.Add(ref _totalFilesSynced, successCount);
            _lastSyncTime = DateTimeOffset.UtcNow;

            var result = new FileSyncResult
            {
                Success = successCount > 0,
                FilesSynced = successCount,
                AgentsSynced = successCount,
                Files = results
            };

            RaiseSyncCompleted(result, FileSyncDirection.ServerToAgents, "server");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync to agents");
            return new FileSyncResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<FileSyncResult> PropagateFromAgentAsync(
        string sourceAgentId,
        string filePath,
        string changeType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Propagating file change from agent. Source: {Agent}, File: {File}, Change: {Change}",
            sourceAgentId, filePath, changeType);

        try
        {
            // Get all agents except the source
            var agents = await GetMatchingAgentsAsync("*", cancellationToken);
            var targetAgents = agents.Where(a => a.Id != sourceAgentId).ToList();

            if (targetAgents.Count == 0)
            {
                _logger.LogDebug("No other agents to propagate to");
                return new FileSyncResult { Success = true, AgentsSynced = 0 };
            }

            var successCount = 0;
            var results = new List<FileSyncFileResult>();

            foreach (var agent in targetAgents)
            {
                try
                {
                    object payload;

                    if (changeType.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
                    {
                        // Send delete command
                        payload = new { path = Path.Combine(_options.AgentSyncPath, filePath) };

                        var deleteRequest = JobRequest.Create("orbit:file.delete") with
                        {
                            TargetAgentId = agent.Id,
                            Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                            Timeout = TimeSpan.FromMinutes(1)
                        };

                        await _jobDispatcher.EnqueueAsync(deleteRequest, cancellationToken);
                    }
                    else
                    {
                        // Send sync command for created/modified
                        payload = new
                        {
                            sourceUrl = $"{_options.ServerUrl}/api/files",
                            sourcePath = filePath,
                            destinationPath = Path.Combine(_options.AgentSyncPath, filePath),
                            singleFile = true
                        };

                        var syncRequest = JobRequest.Create("orbit:file.download") with
                        {
                            TargetAgentId = agent.Id,
                            Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                            Timeout = TimeSpan.FromMinutes(5)
                        };

                        await _jobDispatcher.EnqueueAsync(syncRequest, cancellationToken);
                    }

                    successCount++;
                    _logger.LogDebug("Propagation job dispatched to agent: {AgentName}", agent.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to propagate to agent: {AgentId}", agent.Id);
                    results.Add(new FileSyncFileResult
                    {
                        Path = filePath,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            Interlocked.Add(ref _totalFilesSynced, successCount);
            _lastSyncTime = DateTimeOffset.UtcNow;

            var result = new FileSyncResult
            {
                Success = successCount > 0 || targetAgents.Count == 0,
                FilesSynced = successCount > 0 ? 1 : 0,
                AgentsSynced = successCount,
                Files = results
            };

            RaiseSyncCompleted(result, FileSyncDirection.AgentToAll, sourceAgentId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to propagate from agent");
            return new FileSyncResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public FileSyncStatus GetStatus()
    {
        var agents = _agentRegistry.GetAllAsync().GetAwaiter().GetResult();
        return new FileSyncStatus
        {
            IsRunning = _isRunning,
            IsWatching = _fileWatcher.IsWatching,
            WatchPath = _fileWatcher.WatchPath,
            ConnectedAgents = agents.Count,
            LastSyncTime = _lastSyncTime,
            TotalFilesSynced = Interlocked.Read(ref _totalFilesSynced)
        };
    }

    private async void OnServerFileChanged(object? sender, ServerFileChangedEventArgs e)
    {
        _logger.LogInformation(
            "Server file changed: {ChangeType} - {Path}",
            e.ChangeType, e.RelativePath);

        try
        {
            // Propagate change to all agents
            var changeType = e.ChangeType switch
            {
                ServerFileChangeType.Created => "Created",
                ServerFileChangeType.Modified => "Modified",
                ServerFileChangeType.Deleted => "Deleted",
                ServerFileChangeType.Renamed => "Renamed",
                _ => "Modified"
            };

            await PropagateToAllAgentsAsync(e.RelativePath, changeType, default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle server file change: {Path}", e.RelativePath);
        }
    }

    private async Task PropagateToAllAgentsAsync(
        string filePath,
        string changeType,
        CancellationToken cancellationToken)
    {
        var agents = await GetMatchingAgentsAsync("*", cancellationToken);

        if (agents.Count == 0)
        {
            _logger.LogDebug("No agents connected to propagate to");
            return;
        }

        foreach (var agent in agents)
        {
            try
            {
                object payload;

                if (changeType.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
                {
                    payload = new { path = Path.Combine(_options.AgentSyncPath, filePath) };

                    var deleteRequest = JobRequest.Create("orbit:file.delete") with
                    {
                        TargetAgentId = agent.Id,
                        Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                        Timeout = TimeSpan.FromMinutes(1)
                    };

                    await _jobDispatcher.EnqueueAsync(deleteRequest, cancellationToken);
                }
                else
                {
                    payload = new
                    {
                        sourceUrl = $"{_options.ServerUrl}/api/files/file/{filePath}",
                        destinationPath = Path.Combine(_options.AgentSyncPath, filePath)
                    };

                    var downloadRequest = JobRequest.Create("orbit:file.download") with
                    {
                        TargetAgentId = agent.Id,
                        Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                        Timeout = TimeSpan.FromMinutes(5)
                    };

                    await _jobDispatcher.EnqueueAsync(downloadRequest, cancellationToken);
                }

                _logger.LogDebug(
                    "Server file change propagated to agent: {Agent}, File: {File}, Change: {Change}",
                    agent.Name, filePath, changeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to propagate to agent: {AgentId}", agent.Id);
            }
        }

        Interlocked.Add(ref _totalFilesSynced, agents.Count);
        _lastSyncTime = DateTimeOffset.UtcNow;
    }

    private async Task<IReadOnlyList<AgentInfo>> GetMatchingAgentsAsync(
        string pattern,
        CancellationToken cancellationToken)
    {
        var allAgents = await _agentRegistry.GetAllAsync(cancellationToken);

        if (pattern == "*")
        {
            return allAgents;
        }

        // Simple pattern matching (exact match or contains)
        var matched = allAgents
            .Where(a => a.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                       a.Id.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matched;
    }

    private void RaiseSyncCompleted(FileSyncResult result, FileSyncDirection direction, string source)
    {
        try
        {
            SyncCompleted?.Invoke(this, new FileSyncCompletedEventArgs
            {
                Result = result,
                Direction = direction,
                Source = source
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SyncCompleted event handler");
        }
    }
}

/// <summary>
/// Options for the file sync service.
/// </summary>
#pragma warning disable CA1056 // URI-like properties should not be strings
public sealed class FileSyncOptions
{
    /// <summary>Server base URL for file API.</summary>
    public string ServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>Path on agents where files should be synced to.</summary>
    public string AgentSyncPath { get; set; } = ".";

    /// <summary>Whether to watch for server file changes.</summary>
    public bool WatchEnabled { get; set; } = true;

    /// <summary>Path to watch on server (relative to storage root).</summary>
    public string WatchPath { get; set; } = ".";

    /// <summary>File pattern to watch.</summary>
    public string WatchPattern { get; set; } = "*.*";

    /// <summary>Whether to include subdirectories in watch.</summary>
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>Whether to delete files on target that don't exist on source.</summary>
    public bool DeleteOrphans { get; set; }

    /// <summary>Debounce delay in milliseconds.</summary>
    public int DebounceMs { get; set; } = 500;
}
#pragma warning restore CA1056
