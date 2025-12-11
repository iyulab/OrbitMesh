using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Models;
using OrbitMesh.Workflows.Execution;
using ServerDispatcher = OrbitMesh.Host.Services.IJobDispatcher;
using WorkflowDispatcher = OrbitMesh.Workflows.Execution.IJobDispatcher;

namespace OrbitMesh.Host.Services.Adapters;

/// <summary>
/// Adapter that bridges OrbitMesh.Host.IJobDispatcher to OrbitMesh.Workflows.IJobDispatcher.
/// Enables workflow job steps to dispatch jobs through the server's job infrastructure.
/// </summary>
public sealed class WorkflowJobDispatcherAdapter : WorkflowDispatcher
{
    private readonly ServerDispatcher _serverDispatcher;
    private readonly IJobManager _jobManager;
    private readonly ILogger<WorkflowJobDispatcherAdapter> _logger;

    public WorkflowJobDispatcherAdapter(
        ServerDispatcher serverDispatcher,
        IJobManager jobManager,
        ILogger<WorkflowJobDispatcherAdapter> logger)
    {
        _serverDispatcher = serverDispatcher;
        _jobManager = jobManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JobDispatchResult> DispatchAsync(
        string command,
        string pattern,
        object? payload,
        int priority,
        IReadOnlyList<string>? requiredTags,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Workflow dispatching job. Command: {Command}, Pattern: {Pattern}, Priority: {Priority}",
                command, pattern, priority);

            // Serialize payload to bytes if provided
            byte[]? parameters = null;
            if (payload is not null)
            {
                parameters = JsonSerializer.SerializeToUtf8Bytes(payload);
            }

            // Create a job request from workflow parameters
            var request = JobRequest.Create(command) with
            {
                Parameters = parameters,
                Priority = priority,
                Timeout = timeout,
                RequiredCapabilities = requiredTags?.ToList()
            };

            // Handle pattern-based agent selection
            // Pattern "*" means any agent, otherwise it's an agent ID pattern
            if (!string.IsNullOrEmpty(pattern) && pattern != "*")
            {
                // If pattern looks like an exact agent ID, target that agent
                if (!pattern.Contains('*', StringComparison.Ordinal) && !pattern.Contains('?', StringComparison.Ordinal))
                {
                    request = request with { TargetAgentId = pattern };
                }
                // Otherwise, add pattern as a required capability
                else
                {
                    var capabilities = request.RequiredCapabilities?.ToList() ?? [];
                    capabilities.Add($"pattern:{pattern}");
                    request = request with { RequiredCapabilities = capabilities };
                }
            }

            // Enqueue the job
            var job = await _serverDispatcher.EnqueueAsync(request, cancellationToken);

            // Dispatch immediately
            var result = await _serverDispatcher.DispatchAsync(job, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Workflow job dispatch failed. JobId: {JobId}, Reason: {Reason}",
                    job.Id, result.FailureReason);

                return new JobDispatchResult
                {
                    Success = false,
                    JobId = job.Id,
                    Error = result.FailureReason
                };
            }

            _logger.LogInformation(
                "Workflow job dispatched. JobId: {JobId}, AgentId: {AgentId}",
                job.Id, result.AgentId);

            // Wait for job completion with timeout
            var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            var completedJob = await WaitForJobCompletionAsync(job.Id, cts.Token);

            if (completedJob is null)
            {
                return new JobDispatchResult
                {
                    Success = false,
                    JobId = job.Id,
                    Error = "Job timed out or was cancelled"
                };
            }

            // Deserialize result data if present
            object? jobResultData = null;
            if (completedJob.Result?.Data is { Length: > 0 })
            {
                try
                {
                    jobResultData = JsonSerializer.Deserialize<object>(completedJob.Result.Data);
                }
                catch
                {
                    // If deserialization fails, return raw bytes as string
                    jobResultData = System.Text.Encoding.UTF8.GetString(completedJob.Result.Data);
                }
            }

            return new JobDispatchResult
            {
                Success = completedJob.Status == Core.Enums.JobStatus.Completed,
                JobId = job.Id,
                JobResult = jobResultData,
                Error = completedJob.Status == Core.Enums.JobStatus.Failed
                    ? completedJob.Result?.Error
                    : null
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Workflow job dispatch was cancelled. Command: {Command}", command);
            return new JobDispatchResult
            {
                Success = false,
                Error = "Job dispatch was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow job dispatch failed. Command: {Command}", command);
            return new JobDispatchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Polls for job completion until the job reaches a terminal state.
    /// </summary>
    private async Task<Job?> WaitForJobCompletionAsync(string jobId, CancellationToken cancellationToken)
    {
        // Poll interval for job completion
        const int pollIntervalMs = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var job = await _jobManager.GetAsync(jobId, cancellationToken);

            if (job is null)
            {
                return null;
            }

            // Check if job reached terminal state
            if (job.Status is Core.Enums.JobStatus.Completed
                or Core.Enums.JobStatus.Failed
                or Core.Enums.JobStatus.Cancelled
                or Core.Enums.JobStatus.TimedOut)
            {
                return job;
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
        }

        return null;
    }
}
