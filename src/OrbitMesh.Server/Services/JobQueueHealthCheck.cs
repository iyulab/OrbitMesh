using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Health check for job queue status.
/// </summary>
public sealed class JobQueueHealthCheck : IHealthCheck
{
    private readonly IJobManager _jobManager;
    private readonly int _pendingThreshold;

    /// <summary>
    /// Creates a new job queue health check.
    /// </summary>
    /// <param name="jobManager">The job manager.</param>
    /// <param name="pendingThreshold">Threshold for degraded status (default: 100).</param>
    public JobQueueHealthCheck(IJobManager jobManager, int pendingThreshold = 100)
    {
        _jobManager = jobManager;
        _pendingThreshold = pendingThreshold;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingJobs = await _jobManager.GetJobsAsync(JobStatus.Pending, null, cancellationToken);
            var runningJobs = await _jobManager.GetJobsAsync(JobStatus.Running, null, cancellationToken);

            var pendingCount = pendingJobs.Count;
            var runningCount = runningJobs.Count;

            var data = new Dictionary<string, object>
            {
                ["PendingJobs"] = pendingCount,
                ["RunningJobs"] = runningCount,
                ["PendingThreshold"] = _pendingThreshold
            };

            if (pendingCount >= _pendingThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"High number of pending jobs: {pendingCount} (threshold: {_pendingThreshold})",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Job queue healthy ({pendingCount} pending, {runningCount} running)",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check job queue health",
                ex);
        }
    }
}
