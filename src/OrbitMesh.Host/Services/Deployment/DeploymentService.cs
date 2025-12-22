using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.FileTransfer;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Host.Controllers;

namespace OrbitMesh.Host.Services.Deployment;

/// <summary>
/// Service for orchestrating deployments to agents.
/// Handles the full deployment lifecycle: Pre-Script → File Sync → Post-Script.
/// </summary>
public sealed class DeploymentService : IDeploymentService
{
    private readonly IDeploymentProfileStore _profileStore;
    private readonly IDeploymentExecutionStore _executionStore;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IJobManager _jobManager;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileTransferService? _fileTransferService;
    private readonly DeploymentOptions _options;
    private readonly ILogger<DeploymentService> _logger;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellations = new();

    private static readonly TimeSpan JobPollInterval = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public event EventHandler<DeploymentStatusChangedEventArgs>? StatusChanged;

    /// <inheritdoc />
    public event EventHandler<DeploymentProgressEventArgs>? ProgressUpdated;

    public DeploymentService(
        IDeploymentProfileStore profileStore,
        IDeploymentExecutionStore executionStore,
        IJobDispatcher jobDispatcher,
        IJobManager jobManager,
        IAgentRegistry agentRegistry,
        IFileStorageService fileStorage,
        IOptions<DeploymentOptions> options,
        ILogger<DeploymentService> logger,
        IFileTransferService? fileTransferService = null)
    {
        _profileStore = profileStore;
        _executionStore = executionStore;
        _jobDispatcher = jobDispatcher;
        _jobManager = jobManager;
        _agentRegistry = agentRegistry;
        _fileStorage = fileStorage;
        _fileTransferService = fileTransferService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeploymentExecution> DeployAsync(
        string profileId,
        DeploymentTrigger trigger = DeploymentTrigger.Manual,
        CancellationToken ct = default)
    {
        var profile = await _profileStore.GetAsync(profileId, ct)
            ?? throw new InvalidOperationException($"Deployment profile {profileId} not found");

        if (!profile.IsEnabled)
        {
            throw new InvalidOperationException($"Deployment profile {profileId} is disabled");
        }

        _logger.LogInformation(
            "Starting deployment for profile '{ProfileName}' (ID: {ProfileId}), Trigger: {Trigger}",
            profile.Name, profileId, trigger);

        // Create execution record
        var execution = new DeploymentExecution
        {
            Id = DeploymentExecution.GenerateId(),
            ProfileId = profileId,
            Status = DeploymentStatus.Pending,
            Trigger = trigger,
            StartedAt = DateTimeOffset.UtcNow
        };

        execution = await _executionStore.CreateAsync(execution, ct);

        // Get matching agents
        var matchingAgents = await GetMatchingAgentsInternalAsync(profile.TargetAgentPattern, ct);

        if (matchingAgents.Count == 0)
        {
            _logger.LogWarning(
                "No agents matched pattern '{Pattern}' for profile '{ProfileName}'",
                profile.TargetAgentPattern, profile.Name);

            execution = execution with
            {
                Status = DeploymentStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = $"No agents matched pattern: {profile.TargetAgentPattern}"
            };

            await _executionStore.UpdateAsync(execution, ct);
            return execution;
        }

        // Update execution with agent count
        execution = execution with
        {
            Status = DeploymentStatus.InProgress,
            TotalAgents = matchingAgents.Count
        };
        await _executionStore.UpdateAsync(execution, ct);

        RaiseStatusChanged(execution, DeploymentStatus.Pending, DeploymentStatus.InProgress);

        // Create cancellation source for this execution
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeCancellations.TryAdd(execution.Id, cts);

        try
        {
            // Execute deployment to all agents in parallel
            var agentResults = await DeployToAgentsAsync(
                profile, execution, matchingAgents, cts.Token);

            // Calculate final status
            var successCount = agentResults.Count(r => r.Status == AgentDeploymentStatus.Succeeded);
            var failedCount = agentResults.Count(r =>
                r.Status == AgentDeploymentStatus.Failed ||
                r.Status == AgentDeploymentStatus.Unreachable);

            var finalStatus = successCount == matchingAgents.Count
                ? DeploymentStatus.Succeeded
                : failedCount == matchingAgents.Count
                    ? DeploymentStatus.Failed
                    : DeploymentStatus.PartialSuccess;

            var totalBytes = agentResults.Sum(r => r.FileSyncResult?.BytesTransferred ?? 0);
            var totalFiles = agentResults.Sum(r =>
                (r.FileSyncResult?.FilesCreated ?? 0) +
                (r.FileSyncResult?.FilesUpdated ?? 0));

            // Update execution with results
            var previousStatus = execution.Status;
            execution = execution with
            {
                Status = finalStatus,
                CompletedAt = DateTimeOffset.UtcNow,
                SuccessfulAgents = successCount,
                FailedAgents = failedCount,
                AgentResults = agentResults,
                BytesTransferred = totalBytes,
                FilesTransferred = totalFiles
            };

            await _executionStore.UpdateAsync(execution, ct);

            // Update profile's last deployed timestamp
            await _profileStore.UpdateLastDeployedAsync(profileId, DateTimeOffset.UtcNow, ct);

            RaiseStatusChanged(execution, previousStatus, finalStatus);

            _logger.LogInformation(
                "Deployment '{ExecutionId}' completed. Status: {Status}, Success: {Success}/{Total}",
                execution.Id, finalStatus, successCount, matchingAgents.Count);

            return execution;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Deployment '{ExecutionId}' was cancelled", execution.Id);

            var previousStatus = execution.Status;
            execution = execution with
            {
                Status = DeploymentStatus.Cancelled,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = "Deployment was cancelled"
            };

            await _executionStore.UpdateAsync(execution, CancellationToken.None);
            RaiseStatusChanged(execution, previousStatus, DeploymentStatus.Cancelled);

            return execution;
        }
        finally
        {
            _activeCancellations.TryRemove(execution.Id, out _);
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(string executionId, CancellationToken ct = default)
    {
        if (_activeCancellations.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancellation requested for deployment '{ExecutionId}'", executionId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<DeploymentExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
    {
        return _executionStore.GetAsync(executionId, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeploymentExecution>> GetInProgressAsync(CancellationToken ct = default)
    {
        return _executionStore.GetInProgressAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string Id, string Name)>> GetMatchingAgentsAsync(
        string profileId,
        CancellationToken ct = default)
    {
        var profile = await _profileStore.GetAsync(profileId, ct);
        if (profile is null) return [];

        return await GetMatchingAgentsInternalAsync(profile.TargetAgentPattern, ct);
    }

    // ─────────────────────────────────────────────────────────────
    // Private implementation
    // ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<(string Id, string Name)>> GetMatchingAgentsInternalAsync(
        string pattern,
        CancellationToken ct)
    {
        var allAgents = await _agentRegistry.GetAllAsync(ct);
        var regex = PatternToRegex(pattern);

        return allAgents
            .Where(a => a.Status == AgentStatus.Ready && regex.IsMatch(a.Id))
            .Select(a => (a.Id, a.Name ?? a.Id))
            .ToList();
    }

    private static Regex PatternToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private async Task<IReadOnlyList<AgentDeploymentResult>> DeployToAgentsAsync(
        DeploymentProfile profile,
        DeploymentExecution execution,
        IReadOnlyList<(string Id, string Name)> agents,
        CancellationToken ct)
    {
        var tasks = agents.Select(async agent =>
        {
            var result = new AgentDeploymentResult
            {
                AgentId = agent.Id,
                AgentName = agent.Name,
                StartedAt = DateTimeOffset.UtcNow
            };

            try
            {
                ct.ThrowIfCancellationRequested();

                // Phase 1: Pre-deploy script
                if (profile.PreDeployScript is not null)
                {
                    RaiseProgress(execution, agent.Id, DeploymentPhase.PreScript, "Running pre-deploy script");

                    result = result with
                    {
                        Status = AgentDeploymentStatus.RunningPreScript
                    };

                    var preResult = await ExecuteScriptAsync(
                        agent.Id, profile.PreDeployScript, ct);

                    result = result with { PreDeployResult = preResult };

                    if (!preResult.Success && !profile.PreDeployScript.ContinueOnError)
                    {
                        result = result with
                        {
                            Status = AgentDeploymentStatus.Failed,
                            CompletedAt = DateTimeOffset.UtcNow,
                            ErrorMessage = $"Pre-deploy script failed: {preResult.StandardError}"
                        };
                        return result;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // Phase 2: File sync
                RaiseProgress(execution, agent.Id, DeploymentPhase.FileSync, "Syncing files");

                result = result with
                {
                    Status = AgentDeploymentStatus.SyncingFiles
                };

                var syncResult = await SyncFilesToAgentAsync(
                    agent.Id, profile, ct);

                result = result with { FileSyncResult = syncResult };

                if (!syncResult.Success)
                {
                    result = result with
                    {
                        Status = AgentDeploymentStatus.Failed,
                        CompletedAt = DateTimeOffset.UtcNow,
                        ErrorMessage = $"File sync failed: {syncResult.ErrorMessage}"
                    };
                    return result;
                }

                ct.ThrowIfCancellationRequested();

                // Phase 3: Post-deploy script
                if (profile.PostDeployScript is not null)
                {
                    RaiseProgress(execution, agent.Id, DeploymentPhase.PostScript, "Running post-deploy script");

                    result = result with
                    {
                        Status = AgentDeploymentStatus.RunningPostScript
                    };

                    var postResult = await ExecuteScriptAsync(
                        agent.Id, profile.PostDeployScript, ct);

                    result = result with { PostDeployResult = postResult };

                    if (!postResult.Success && !profile.PostDeployScript.ContinueOnError)
                    {
                        result = result with
                        {
                            Status = AgentDeploymentStatus.Failed,
                            CompletedAt = DateTimeOffset.UtcNow,
                            ErrorMessage = $"Post-deploy script failed: {postResult.StandardError}"
                        };
                        return result;
                    }
                }

                // Success!
                RaiseProgress(execution, agent.Id, DeploymentPhase.Completed, "Deployment completed");

                result = result with
                {
                    Status = AgentDeploymentStatus.Succeeded,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                return result;
            }
            catch (OperationCanceledException)
            {
                result = result with
                {
                    Status = AgentDeploymentStatus.Skipped,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "Deployment cancelled"
                };
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment to agent '{AgentId}' failed", agent.Id);

                RaiseProgress(execution, agent.Id, DeploymentPhase.Failed, ex.Message);

                result = result with
                {
                    Status = AgentDeploymentStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = ex.Message
                };
                return result;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<ScriptExecutionResult> ExecuteScriptAsync(
        string agentId,
        DeploymentScript script,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var payload = new
            {
                command = script.Command,
                arguments = script.Arguments ?? [],
                workingDirectory = script.WorkingDirectory,
                timeoutSeconds = script.TimeoutSeconds
            };

            var request = JobRequest.Create("orbit:system:exec") with
            {
                TargetAgentId = agentId,
                Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
                Timeout = TimeSpan.FromSeconds(script.TimeoutSeconds + 10) // Buffer for network
            };

            var job = await EnqueueAndWaitAsync(request, ct);

            var duration = DateTimeOffset.UtcNow - startTime;

            if (job.Status == JobStatus.Completed && job.Result is not null)
            {
                // Parse result from job
                var resultData = job.Result.Data;
                if (resultData is not null)
                {
                    using var doc = JsonDocument.Parse(resultData);
                    var root = doc.RootElement;

                    return new ScriptExecutionResult
                    {
                        Success = root.TryGetProperty("exitCode", out var ec) && ec.GetInt32() == 0,
                        ExitCode = root.TryGetProperty("exitCode", out var exitCode) ? exitCode.GetInt32() : -1,
                        StandardOutput = root.TryGetProperty("stdout", out var stdout) ? stdout.GetString() : null,
                        StandardError = root.TryGetProperty("stderr", out var stderr) ? stderr.GetString() : null,
                        Duration = duration
                    };
                }
            }

            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = job.Error ?? "Script execution failed",
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            return new ScriptExecutionResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = ex.Message,
                Duration = DateTimeOffset.UtcNow - startTime
            };
        }
    }

    private async Task<FileSyncExecutionResult> SyncFilesToAgentAsync(
        string agentId,
        DeploymentProfile profile,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Build manifest from source path
            var manifest = await _fileStorage.GetManifestAsync(profile.SourcePath, ct);

            if (manifest is null)
            {
                return new FileSyncExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Source path not found or empty: {profile.SourcePath}",
                    TransferMode = profile.TransferMode,
                    Duration = DateTimeOffset.UtcNow - startTime
                };
            }

            // Filter files based on include/exclude patterns
            if (profile.IncludePatterns is { Count: > 0 } || profile.ExcludePatterns is { Count: > 0 })
            {
                manifest = FilterManifest(manifest, profile.IncludePatterns, profile.ExcludePatterns);
            }

            // Use centralized file transfer service when available
            if (_fileTransferService is not null && manifest.Files is { Count: > 0 })
            {
                return await SyncFilesViaTransferServiceAsync(
                    agentId, profile, manifest, startTime, ct);
            }

            // Fallback to job-based transfer
            return await SyncFilesViaJobAsync(
                agentId, profile, manifest, startTime, ct);
        }
        catch (Exception ex)
        {
            return new FileSyncExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                TransferMode = profile.TransferMode,
                Duration = DateTimeOffset.UtcNow - startTime
            };
        }
    }

    private async Task<FileSyncExecutionResult> SyncFilesViaTransferServiceAsync(
        string agentId,
        DeploymentProfile profile,
        SyncManifest manifest,
        DateTimeOffset startTime,
        CancellationToken ct)
    {
        // Create batch transfer request using the profile's transfer mode directly
        var batchRequest = new BatchFileTransferRequest
        {
            TargetAgentId = agentId,
            Mode = profile.TransferMode,
            DeleteOrphans = profile.DeleteOrphans,
            MaxConcurrency = 4,
            Files = manifest.Files.Select(f => new FileTransferItem
            {
                RelativePath = f.Path,
                Checksum = f.Checksum,
                Size = f.Size,
                LastModified = DateTimeOffset.UtcNow // SyncFileEntry doesn't track modification time
            }).ToList()
        };

        _logger.LogInformation(
            "Transferring {FileCount} files to agent {AgentId} via {Mode}",
            manifest.Files.Count, agentId, profile.TransferMode);

        var result = await _fileTransferService!.TransferBatchAsync(batchRequest, null, ct);

        var duration = DateTimeOffset.UtcNow - startTime;

        // Determine the primary transfer mode based on which method transferred more files
        var actualMode = result.TransfersViaP2P > result.TransfersViaHttp
            ? FileTransferMode.P2PDirect
            : FileTransferMode.Http;

        return new FileSyncExecutionResult
        {
            Success = result.Success,
            FilesCreated = result.FilesTransferred,
            FilesUpdated = 0,
            FilesDeleted = result.FilesDeleted,
            BytesTransferred = result.BytesTransferred,
            TransferMode = actualMode,
            Duration = duration,
            ErrorMessage = result.Error
        };
    }

    private async Task<FileSyncExecutionResult> SyncFilesViaJobAsync(
        string agentId,
        DeploymentProfile profile,
        SyncManifest manifest,
        DateTimeOffset startTime,
        CancellationToken ct)
    {
        var payload = new
        {
            sourceUrl = $"{_options.ServerUrl}/api/files",
            sourcePath = profile.SourcePath,
            destinationPath = profile.TargetPath,
            manifest,
            deleteOrphans = profile.DeleteOrphans
        };

        var request = JobRequest.Create("orbit:file.sync") with
        {
            TargetAgentId = agentId,
            Parameters = JsonSerializer.SerializeToUtf8Bytes(payload),
            Timeout = TimeSpan.FromMinutes(30) // File sync can take longer
        };

        var job = await EnqueueAndWaitAsync(request, ct);

        var duration = DateTimeOffset.UtcNow - startTime;

        if (job.Status == JobStatus.Completed && job.Result is not null)
        {
            var resultData = job.Result.Data;
            if (resultData is not null)
            {
                using var doc = JsonDocument.Parse(resultData);
                var root = doc.RootElement;

                return new FileSyncExecutionResult
                {
                    Success = true,
                    FilesCreated = root.TryGetProperty("filesCreated", out var fc) ? fc.GetInt32() : 0,
                    FilesUpdated = root.TryGetProperty("filesUpdated", out var fu) ? fu.GetInt32() : 0,
                    FilesDeleted = root.TryGetProperty("filesDeleted", out var fd) ? fd.GetInt32() : 0,
                    BytesTransferred = root.TryGetProperty("bytesTransferred", out var bt) ? bt.GetInt64() : 0,
                    TransferMode = profile.TransferMode,
                    Duration = duration
                };
            }
        }

        return new FileSyncExecutionResult
        {
            Success = false,
            ErrorMessage = job.Error ?? "File sync failed",
            TransferMode = profile.TransferMode,
            Duration = duration
        };
    }

    private static SyncManifest FilterManifest(
        SyncManifest manifest,
        IReadOnlyList<string>? includePatterns,
        IReadOnlyList<string>? excludePatterns)
    {
        // For now, pass manifest as-is; filtering would be done based on patterns
        // This can be enhanced later with actual glob matching
        return manifest;
    }

    private async Task<Job> EnqueueAndWaitAsync(JobRequest request, CancellationToken ct)
    {
        var job = await _jobDispatcher.EnqueueAsync(request, ct);

        // Poll for job completion
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(JobPollInterval, ct);

            var currentJob = await _jobManager.GetAsync(job.Id, ct)
                ?? throw new InvalidOperationException($"Job {job.Id} was not found");

            // Check if job is in a terminal state
            if (currentJob.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
            {
                return currentJob;
            }
        }

        ct.ThrowIfCancellationRequested();
        return job;
    }

    private void RaiseStatusChanged(
        DeploymentExecution execution,
        DeploymentStatus previousStatus,
        DeploymentStatus newStatus)
    {
        StatusChanged?.Invoke(this, new DeploymentStatusChangedEventArgs
        {
            ExecutionId = execution.Id,
            ProfileId = execution.ProfileId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus
        });
    }

    private void RaiseProgress(
        DeploymentExecution execution,
        string agentId,
        DeploymentPhase phase,
        string? message)
    {
        ProgressUpdated?.Invoke(this, new DeploymentProgressEventArgs
        {
            ExecutionId = execution.Id,
            ProfileId = execution.ProfileId,
            AgentId = agentId,
            Phase = phase,
            Message = message
        });
    }
}
