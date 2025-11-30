using System.Collections.Concurrent;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// In-memory implementation of the job manager.
/// Suitable for single-server deployments and testing.
/// </summary>
public class InMemoryJobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();
    private readonly object _dequeueLock = new();

    #region Job Lifecycle

    /// <inheritdoc />
    public Task<Job> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        // Check idempotency key first
        if (_idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingJobId))
        {
            if (_jobs.TryGetValue(existingJobId, out var existingJob))
            {
                return Task.FromResult(existingJob);
            }
        }

        var job = Job.FromRequest(request);
        _jobs[job.Id] = job;
        _idempotencyIndex[request.IdempotencyKey] = job.Id;

        return Task.FromResult(job);
    }

    /// <inheritdoc />
    public Task<Job?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    /// <inheritdoc />
    public Task<bool> AssignAsync(string jobId, string agentId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Pending)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Assigned,
            AssignedAgentId = agentId,
            AssignedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> AcknowledgeAsync(string jobId, string agentId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Assigned || job.AssignedAgentId != agentId)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> CompleteAsync(string jobId, JobResult result, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Running)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Completed,
            Result = result,
            CompletedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> FailAsync(string jobId, string errorMessage, string? errorCode = null, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Running && job.Status != JobStatus.Assigned)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Failed,
            Error = errorMessage,
            ErrorCode = errorCode,
            CompletedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(string jobId, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        // Can only cancel pending, assigned, or running jobs
        if (job.Status != JobStatus.Pending &&
            job.Status != JobStatus.Assigned &&
            job.Status != JobStatus.Running)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Cancelled,
            CancellationReason = reason,
            CompletedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> RequeueAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        // Can only requeue failed jobs that haven't exceeded max retries
        if (job.Status != JobStatus.Failed)
        {
            return Task.FromResult(false);
        }

        if (!job.CanRetry)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Pending,
            AssignedAgentId = null,
            AssignedAt = null,
            StartedAt = null,
            CompletedAt = null,
            Error = null,
            ErrorCode = null,
            RetryCount = job.RetryCount + 1
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    /// <inheritdoc />
    public Task<bool> RequeueForTimeoutAsync(string jobId, int maxTimeoutRetries, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        // Can only requeue assigned or running jobs that haven't exceeded max timeout retries
        if (job.Status != JobStatus.Assigned && job.Status != JobStatus.Running)
        {
            return Task.FromResult(false);
        }

        if (job.TimeoutCount >= maxTimeoutRetries)
        {
            return Task.FromResult(false);
        }

        var updatedJob = job with
        {
            Status = JobStatus.Pending,
            AssignedAgentId = null,
            AssignedAt = null,
            StartedAt = null,
            CompletedAt = null,
            Error = null,
            ErrorCode = null,
            TimeoutCount = job.TimeoutCount + 1
        };

        return Task.FromResult(_jobs.TryUpdate(jobId, updatedJob, job));
    }

    #endregion

    #region Progress Tracking

    /// <inheritdoc />
    public Task UpdateProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(progress.JobId, out var job))
        {
            return Task.CompletedTask;
        }

        if (job.Status != JobStatus.Running)
        {
            return Task.CompletedTask;
        }

        var updatedJob = job with { LastProgress = progress };
        _jobs.TryUpdate(progress.JobId, updatedJob, job);

        return Task.CompletedTask;
    }

    #endregion

    #region Queue Operations

    /// <inheritdoc />
    public Task<Job?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        return DequeueNextAsync(null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Job?> DequeueNextAsync(IReadOnlyList<string>? capabilities, CancellationToken cancellationToken = default)
    {
        lock (_dequeueLock)
        {
            var query = _jobs.Values
                .Where(j => j.Status == JobStatus.Pending);

            // Filter by capabilities if specified
            if (capabilities is { Count: > 0 })
            {
                query = query.Where(j =>
                    j.Request.RequiredCapabilities == null ||
                    j.Request.RequiredCapabilities.Count == 0 ||
                    j.Request.RequiredCapabilities.All(rc =>
                        capabilities.Any(c => c.Equals(rc, StringComparison.OrdinalIgnoreCase))));
            }

            // Order by priority (descending) then by creation time (ascending, FIFO)
            var nextJob = query
                .OrderByDescending(j => j.Request.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefault();

            if (nextJob == null)
            {
                return Task.FromResult<Job?>(null);
            }

            // Mark as assigned (without agent) to remove from pending queue
            // This prevents the same job from being dequeued again
            var dequeuedJob = nextJob with
            {
                Status = JobStatus.Assigned,
                AssignedAt = DateTimeOffset.UtcNow
            };
            _jobs.TryUpdate(nextJob.Id, dequeuedJob, nextJob);

            return Task.FromResult<Job?>(dequeuedJob);
        }
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = _jobs.Values
            .Where(j => j.Status == JobStatus.Pending)
            .OrderByDescending(j => j.Request.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Job>>(pending);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Where(j => j.AssignedAgentId == agentId)
            .ToList();

        return Task.FromResult<IReadOnlyList<Job>>(jobs);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Where(j => j.Status == status)
            .ToList();

        return Task.FromResult<IReadOnlyList<Job>>(jobs);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetTimedOutJobsAsync(CancellationToken cancellationToken = default)
    {
        var timedOut = _jobs.Values
            .Where(j => j.IsTimedOut)
            .ToList();

        return Task.FromResult<IReadOnlyList<Job>>(timedOut);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetJobsAsync(JobStatus? status = null, string? agentId = null, CancellationToken cancellationToken = default)
    {
        var query = _jobs.Values.AsEnumerable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(agentId))
        {
            query = query.Where(j => j.AssignedAgentId == agentId);
        }

        var jobs = query.ToList();
        return Task.FromResult<IReadOnlyList<Job>>(jobs);
    }

    #endregion
}
