using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Configuration options for the JobTimeoutMonitor.
/// </summary>
public class JobTimeoutMonitorOptions
{
    /// <summary>
    /// Interval for checking job timeouts.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default timeout for jobs that don't specify one.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan DefaultJobTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for jobs in Assigned state waiting for ACK.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of times a job can be reassigned due to timeout.
    /// Default: 3.
    /// </summary>
    public int MaxTimeoutRetries { get; set; } = 3;
}

/// <summary>
/// Background service that monitors job timeouts and handles reassignment.
/// </summary>
public class JobTimeoutMonitor : BackgroundService
{
    private readonly IJobManager _jobManager;
    private readonly IDeadLetterService _deadLetterService;
    private readonly ILogger<JobTimeoutMonitor> _logger;
    private readonly JobTimeoutMonitorOptions _options;

    public JobTimeoutMonitor(
        IJobManager jobManager,
        IDeadLetterService deadLetterService,
        IOptions<JobTimeoutMonitorOptions> options,
        ILogger<JobTimeoutMonitor> logger)
    {
        _jobManager = jobManager;
        _deadLetterService = deadLetterService;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "JobTimeoutMonitor starting. CheckInterval: {CheckInterval}, AckTimeout: {AckTimeout}",
            _options.CheckInterval,
            _options.AckTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForTimeoutsAsync(stoppingToken);
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JobTimeoutMonitor");
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
        }

        _logger.LogInformation("JobTimeoutMonitor shutting down");
    }

    /// <summary>
    /// Checks for timed out jobs and handles them appropriately.
    /// </summary>
    private async Task CheckForTimeoutsAsync(CancellationToken stoppingToken)
    {
        // Check for ACK timeouts (Assigned but not ACKed)
        await CheckAckTimeoutsAsync(stoppingToken);

        // Check for execution timeouts (Running but timed out)
        await CheckExecutionTimeoutsAsync(stoppingToken);
    }

    /// <summary>
    /// Checks for jobs that were assigned but never acknowledged.
    /// </summary>
    private async Task CheckAckTimeoutsAsync(CancellationToken stoppingToken)
    {
        var assignedJobs = await _jobManager.GetByStatusAsync(JobStatus.Assigned, stoppingToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var job in assignedJobs)
        {
            if (job.AssignedAt is null)
            {
                continue;
            }

            var elapsed = now - job.AssignedAt.Value;

            if (elapsed > _options.AckTimeout)
            {
                _logger.LogWarning(
                    "Job {JobId} timed out waiting for ACK. Assigned to {AgentId} at {AssignedAt}. Elapsed: {Elapsed}",
                    job.Id,
                    job.AssignedAgentId,
                    job.AssignedAt,
                    elapsed);

                await HandleTimeoutAsync(job, "ACK timeout", stoppingToken);
            }
        }
    }

    /// <summary>
    /// Checks for jobs that have been running too long.
    /// </summary>
    private async Task CheckExecutionTimeoutsAsync(CancellationToken stoppingToken)
    {
        var runningJobs = await _jobManager.GetByStatusAsync(JobStatus.Running, stoppingToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var job in runningJobs)
        {
            if (job.StartedAt is null)
            {
                continue;
            }

            var timeout = job.Request.Timeout ?? _options.DefaultJobTimeout;
            var elapsed = now - job.StartedAt.Value;

            if (elapsed > timeout)
            {
                _logger.LogWarning(
                    "Job {JobId} timed out during execution. Started at {StartedAt}. Elapsed: {Elapsed}, Timeout: {Timeout}",
                    job.Id,
                    job.StartedAt,
                    elapsed,
                    timeout);

                await HandleTimeoutAsync(job, "Execution timeout", stoppingToken);
            }
        }
    }

    /// <summary>
    /// Handles a timed out job by either requeueing or moving to dead letter.
    /// </summary>
    private async Task HandleTimeoutAsync(
        Core.Models.Job job,
        string reason,
        CancellationToken stoppingToken)
    {
        // Try to requeue the job with timeout tracking
        var requeued = await _jobManager.RequeueForTimeoutAsync(
            job.Id,
            _options.MaxTimeoutRetries,
            stoppingToken);

        if (requeued)
        {
            _logger.LogInformation(
                "Job {JobId} requeued after timeout. Reason: {Reason}. Attempt {Attempt}/{MaxAttempts}",
                job.Id,
                reason,
                job.TimeoutCount + 1,
                _options.MaxTimeoutRetries);
        }
        else
        {
            // Failed to requeue - move to dead letter queue
            await _deadLetterService.EnqueueAsync(
                job,
                $"Job timed out after {_options.MaxTimeoutRetries} retries: {reason}",
                stoppingToken);

            await _jobManager.FailAsync(
                job.Id,
                $"Job timed out after {_options.MaxTimeoutRetries} retries: {reason}",
                "TIMEOUT_EXCEEDED",
                stoppingToken);

            _logger.LogError(
                "Job {JobId} moved to dead letter queue after {MaxRetries} timeout retries. Reason: {Reason}",
                job.Id,
                _options.MaxTimeoutRetries,
                reason);
        }
    }
}
