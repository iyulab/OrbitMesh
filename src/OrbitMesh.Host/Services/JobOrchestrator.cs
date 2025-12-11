using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Orchestrates job lifecycle from submission to completion.
/// </summary>
public sealed class JobOrchestrator : IJobOrchestrator
{
    private readonly IJobManager _jobManager;
    private readonly IJobDispatcher _dispatcher;
    private readonly IAgentRouter _router;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IDeadLetterService _deadLetterService;
    private readonly IProgressService _progressService;
    private readonly IResilienceService _resilienceService;

    public JobOrchestrator(
        IJobManager jobManager,
        IJobDispatcher dispatcher,
        IAgentRouter router,
        IIdempotencyService idempotencyService,
        IDeadLetterService deadLetterService,
        IProgressService progressService,
        IResilienceService resilienceService)
    {
        _jobManager = jobManager;
        _dispatcher = dispatcher;
        _router = router;
        _idempotencyService = idempotencyService;
        _deadLetterService = deadLetterService;
        _progressService = progressService;
        _resilienceService = resilienceService;
    }

    /// <inheritdoc />
    public async Task<JobSubmissionResult> SubmitJobAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var acquired = await _idempotencyService.TryAcquireLockAsync(request.IdempotencyKey, cancellationToken);

            if (!acquired)
            {
                // Return cached result for duplicate request
                var cachedResult = await _idempotencyService.GetResultAsync<JobSubmissionResult>(
                    request.IdempotencyKey, cancellationToken);

                if (cachedResult is not null)
                {
                    return cachedResult;
                }

                return JobSubmissionResult.Failed("Duplicate request being processed");
            }
        }

        try
        {
            return await _resilienceService.ExecuteWithResilienceAsync(
                $"submit-job-{request.Id}",
                async ct => await SubmitJobInternalAsync(request, ct),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return JobSubmissionResult.Failed(ex.Message);
        }
        finally
        {
            // Store result for idempotency
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                await _idempotencyService.ReleaseLockAsync(request.IdempotencyKey, cancellationToken);
            }
        }
    }

    private async Task<JobSubmissionResult> SubmitJobInternalAsync(JobRequest request, CancellationToken cancellationToken)
    {
        // Create job in manager
        var job = await _jobManager.EnqueueAsync(request, cancellationToken);

        // Try to find an available agent
        var routingRequest = RoutingRequest.FromJobRequest(job.Request);
        var agent = await _router.SelectAgentAsync(routingRequest, cancellationToken);

        if (agent is not null)
        {
            // Dispatch immediately
            await _dispatcher.DispatchAsync(job, agent, cancellationToken);

            // Update job status
            await _jobManager.AssignAsync(job.Id, agent.Id, cancellationToken);

            // Cache result for idempotency
            var result = JobSubmissionResult.Succeeded(job.Id, JobStatus.Assigned);
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                await _idempotencyService.SetResultAsync(request.IdempotencyKey, result, cancellationToken);
            }

            return result;
        }

        // No agent available - job stays queued
        var pendingResult = JobSubmissionResult.Succeeded(job.Id, JobStatus.Pending);
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            await _idempotencyService.SetResultAsync(request.IdempotencyKey, pendingResult, cancellationToken);
        }

        return pendingResult;
    }

    /// <inheritdoc />
    public async Task HandleResultAsync(JobResult result, CancellationToken cancellationToken = default)
    {
        var job = await _jobManager.GetAsync(result.JobId, cancellationToken);

        if (job is null)
        {
            return;
        }

        if (result.Status == JobStatus.Completed)
        {
            await _jobManager.CompleteAsync(result.JobId, result, cancellationToken);
        }
        else if (result.Status == JobStatus.Failed)
        {
            await HandleFailedJobAsync(job, result, cancellationToken);
        }
        else if (result.Status == JobStatus.Cancelled)
        {
            await _jobManager.CancelAsync(result.JobId, "Cancelled by agent", cancellationToken);
        }

        // Clear progress tracking
        await _progressService.ClearProgressAsync(result.JobId, cancellationToken);
    }

    private async Task HandleFailedJobAsync(Job job, JobResult result, CancellationToken cancellationToken)
    {
        // Check if we should retry
        if (job.RetryCount < job.Request.MaxRetries)
        {
            await _jobManager.RequeueAsync(job.Id, cancellationToken);

            // Try to dispatch to another agent
            var updatedJob = await _jobManager.GetAsync(job.Id, cancellationToken);
            if (updatedJob is not null)
            {
                var routingRequest = RoutingRequest.FromJobRequest(updatedJob.Request);
                var agent = await _router.SelectAgentAsync(routingRequest, cancellationToken);
                if (agent is not null)
                {
                    await _dispatcher.DispatchAsync(updatedJob, agent, cancellationToken);
                    await _jobManager.AssignAsync(updatedJob.Id, agent.Id, cancellationToken);
                }
            }
        }
        else
        {
            // Max retries exceeded - send to DLQ
            var failedJob = job with
            {
                Status = JobStatus.Failed,
                Error = result.Error,
                ErrorCode = result.ErrorCode
            };

            await _deadLetterService.EnqueueAsync(
                failedJob,
                $"Max retries ({job.Request.MaxRetries}) exceeded. Last error: {result.Error}",
                cancellationToken);

            await _jobManager.FailAsync(job.Id, result.Error ?? "Unknown error", result.ErrorCode, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task HandleProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        await _progressService.ReportProgressAsync(progress, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobManager.GetAsync(jobId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        // Can only cancel pending, assigned, or running jobs
        if (job.Status is not (JobStatus.Pending or JobStatus.Assigned or JobStatus.Running))
        {
            return false;
        }

        // If assigned to an agent, send cancel request
        if (!string.IsNullOrEmpty(job.AssignedAgentId))
        {
            await _dispatcher.SendCancelToAgentAsync(jobId, job.AssignedAgentId, cancellationToken);
        }

        await _jobManager.CancelAsync(jobId, "Cancelled by user", cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return _jobManager.GetAsync(jobId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetJobsAsync(
        JobStatus? status = null,
        string? agentId = null,
        CancellationToken cancellationToken = default)
    {
        return _jobManager.GetJobsAsync(status, agentId, cancellationToken);
    }
}
