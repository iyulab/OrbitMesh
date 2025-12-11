using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Configuration options for the WorkItemProcessor.
/// </summary>
public class WorkItemProcessorOptions
{
    /// <summary>
    /// Maximum number of concurrent job dispatches.
    /// Default: 10.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Interval for polling pending jobs from the job manager.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Delay before retrying after a dispatch failure.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of dispatch retries for a single job.
    /// Default: 3.
    /// </summary>
    public int MaxDispatchRetries { get; set; } = 3;
}

/// <summary>
/// Background service that processes jobs from the queue and dispatches them to agents.
/// Implements push-based job distribution with configurable concurrency.
/// </summary>
public class WorkItemProcessor : BackgroundService
{
    private readonly IJobManager _jobManager;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IDeadLetterService _deadLetterService;
    private readonly ILogger<WorkItemProcessor> _logger;
    private readonly WorkItemProcessorOptions _options;
    private readonly Channel<Job> _workChannel;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public WorkItemProcessor(
        IJobManager jobManager,
        IJobDispatcher jobDispatcher,
        IAgentRegistry agentRegistry,
        IDeadLetterService deadLetterService,
        IOptions<WorkItemProcessorOptions> options,
        ILogger<WorkItemProcessor> logger)
    {
        _jobManager = jobManager;
        _jobDispatcher = jobDispatcher;
        _agentRegistry = agentRegistry;
        _deadLetterService = deadLetterService;
        _logger = logger;
        _options = options.Value;

        // Create bounded channel with backpressure
        _workChannel = Channel.CreateBounded<Job>(new BoundedChannelOptions(_options.MaxConcurrency * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WorkItemProcessor starting. MaxConcurrency: {MaxConcurrency}, PollingInterval: {PollingInterval}",
            _options.MaxConcurrency,
            _options.PollingInterval);

        // Start the job producer and consumers
        var producerTask = ProduceJobsAsync(stoppingToken);
        var consumerTasks = Enumerable
            .Range(0, _options.MaxConcurrency)
            .Select(_ => ConsumeJobsAsync(stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll([producerTask, .. consumerTasks]);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("WorkItemProcessor shutting down gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WorkItemProcessor encountered an error");
            throw;
        }
    }

    /// <summary>
    /// Producer task that polls pending jobs and writes them to the channel.
    /// </summary>
    private async Task ProduceJobsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get pending jobs
                var pendingJobs = await _jobManager.GetPendingAsync(stoppingToken);

                if (pendingJobs.Count > 0)
                {
                    _logger.LogDebug("Found {Count} pending jobs to process", pendingJobs.Count);

                    foreach (var job in pendingJobs)
                    {
                        // Write job to channel (will wait if channel is full due to backpressure)
                        await _workChannel.Writer.WriteAsync(job, stoppingToken);
                    }
                }

                // Wait before next poll
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job producer");
                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }

        // Signal completion
        _workChannel.Writer.Complete();
    }

    /// <summary>
    /// Consumer task that reads jobs from the channel and dispatches them.
    /// </summary>
    private async Task ConsumeJobsAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _workChannel.Reader.ReadAllAsync(stoppingToken))
        {
            await _concurrencySemaphore.WaitAsync(stoppingToken);

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Processes a single job by dispatching it to an appropriate agent.
    /// </summary>
    private async Task ProcessJobAsync(Job job, CancellationToken stoppingToken)
    {
        // Re-check job status (it may have been processed by another instance)
        var currentJob = await _jobManager.GetAsync(job.Id, stoppingToken);
        if (currentJob is null || currentJob.Status != JobStatus.Pending)
        {
            _logger.LogDebug("Job {JobId} is no longer pending, skipping", job.Id);
            return;
        }

        var dispatchAttempts = 0;
        DispatchResult? result = null;

        while (dispatchAttempts < _options.MaxDispatchRetries && !stoppingToken.IsCancellationRequested)
        {
            dispatchAttempts++;

            // Check if there are available agents
            var agents = await _agentRegistry.GetAllAsync(stoppingToken);
            var availableAgents = agents.Where(a => a.Status == AgentStatus.Ready).ToList();

            if (availableAgents.Count == 0)
            {
                _logger.LogWarning(
                    "No available agents for job {JobId}. Attempt {Attempt}/{MaxAttempts}",
                    job.Id,
                    dispatchAttempts,
                    _options.MaxDispatchRetries);

                if (dispatchAttempts < _options.MaxDispatchRetries)
                {
                    await Task.Delay(_options.RetryDelay, stoppingToken);
                    continue;
                }

                break;
            }

            // Dispatch the job
            result = await _jobDispatcher.DispatchAsync(currentJob, stoppingToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Job {JobId} dispatched to agent {AgentId}",
                    job.Id,
                    result.AgentId);
                return;
            }

            _logger.LogWarning(
                "Failed to dispatch job {JobId}. Attempt {Attempt}/{MaxAttempts}. Reason: {Reason}",
                job.Id,
                dispatchAttempts,
                _options.MaxDispatchRetries,
                result.FailureReason);

            if (dispatchAttempts < _options.MaxDispatchRetries)
            {
                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }

        // All dispatch attempts failed - move to dead letter queue
        if (result is not null && !result.IsSuccess)
        {
            await _deadLetterService.EnqueueAsync(
                currentJob,
                result.FailureReason ?? "Unknown dispatch failure",
                stoppingToken);

            await _jobManager.FailAsync(
                job.Id,
                $"Failed to dispatch after {dispatchAttempts} attempts: {result.FailureReason}",
                "DISPATCH_FAILED",
                stoppingToken);

            _logger.LogError(
                "Job {JobId} moved to dead letter queue after {Attempts} dispatch attempts",
                job.Id,
                dispatchAttempts);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _concurrencySemaphore.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
