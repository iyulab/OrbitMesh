using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Hubs;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Dispatches jobs to agents using a Channel-based queue with backpressure support.
/// </summary>
public class JobDispatcher : IJobDispatcher
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobManager _jobManager;
    private readonly IHubContext<AgentHub, IAgentClient> _hubContext;
    private readonly ILogger<JobDispatcher> _logger;

    private readonly Channel<Job> _dispatchChannel;
    private long _totalDispatched;
    private long _totalFailed;

    public JobDispatcher(
        IAgentRegistry agentRegistry,
        IJobManager jobManager,
        IHubContext<AgentHub, IAgentClient> hubContext,
        ILogger<JobDispatcher> logger)
    {
        _agentRegistry = agentRegistry;
        _jobManager = jobManager;
        _hubContext = hubContext;
        _logger = logger;

        // Create bounded channel with backpressure
        _dispatchChannel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public async Task<Job> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        var job = await _jobManager.EnqueueAsync(request, cancellationToken);

        _logger.LogInformation(
            "Job enqueued. JobId: {JobId}, Command: {Command}, Priority: {Priority}",
            job.Id,
            job.Request.Command,
            job.Request.Priority);

        return job;
    }

    /// <inheritdoc />
    public async Task<DispatchResult> DispatchAsync(Job job, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find target agent
            AgentInfo? agent;

            if (job.Request.TargetAgentId is not null)
            {
                // Dispatch to specific agent
                agent = await _agentRegistry.GetAsync(job.Request.TargetAgentId, cancellationToken);

                if (agent is null)
                {
                    Interlocked.Increment(ref _totalFailed);
                    return DispatchResult.Failure($"Target agent '{job.Request.TargetAgentId}' not found or offline");
                }

                if (agent.Status != AgentStatus.Ready)
                {
                    Interlocked.Increment(ref _totalFailed);
                    return DispatchResult.Failure($"Target agent '{job.Request.TargetAgentId}' is not ready (status: {agent.Status})");
                }
            }
            else if (job.Request.RequiredCapabilities is { Count: > 0 })
            {
                // Find agent by capabilities
                agent = await SelectAgentByCapabilitiesAsync(job.Request.RequiredCapabilities, cancellationToken);

                if (agent is null)
                {
                    Interlocked.Increment(ref _totalFailed);
                    return DispatchResult.Failure(
                        $"No available agent with required capabilities: [{string.Join(", ", job.Request.RequiredCapabilities)}]");
                }
            }
            else
            {
                // Select any available agent
                agent = await SelectAnyAvailableAgentAsync(cancellationToken);

                if (agent is null)
                {
                    Interlocked.Increment(ref _totalFailed);
                    return DispatchResult.Failure("No available agents");
                }
            }

            // Assign job to agent
            var assigned = await _jobManager.AssignAsync(job.Id, agent.Id, cancellationToken);
            if (!assigned)
            {
                Interlocked.Increment(ref _totalFailed);
                return DispatchResult.Failure($"Failed to assign job to agent '{agent.Id}'");
            }

            // Send job to agent via SignalR
            if (agent.ConnectionId is null)
            {
                Interlocked.Increment(ref _totalFailed);
                return DispatchResult.Failure($"Agent '{agent.Id}' has no connection ID");
            }

            await _hubContext.Clients
                .Client(agent.ConnectionId)
                .ExecuteJobAsync(job.Request, cancellationToken);

            Interlocked.Increment(ref _totalDispatched);

            _logger.LogInformation(
                "Job dispatched. JobId: {JobId}, AgentId: {AgentId}, Command: {Command}",
                job.Id,
                agent.Id,
                job.Request.Command);

            return DispatchResult.Success(agent.Id);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalFailed);
            _logger.LogError(ex, "Failed to dispatch job. JobId: {JobId}", job.Id);
            return DispatchResult.Failure($"Dispatch error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<DispatchResult> DispatchAsync(Job job, AgentInfo agent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate agent is ready
            if (agent.Status != AgentStatus.Ready)
            {
                Interlocked.Increment(ref _totalFailed);
                return DispatchResult.Failure($"Agent '{agent.Id}' is not ready (status: {agent.Status})");
            }

            if (agent.ConnectionId is null)
            {
                Interlocked.Increment(ref _totalFailed);
                return DispatchResult.Failure($"Agent '{agent.Id}' has no connection ID");
            }

            // Send job to agent via SignalR
            await _hubContext.Clients
                .Client(agent.ConnectionId)
                .ExecuteJobAsync(job.Request, cancellationToken);

            Interlocked.Increment(ref _totalDispatched);

            _logger.LogInformation(
                "Job dispatched to specific agent. JobId: {JobId}, AgentId: {AgentId}, Command: {Command}",
                job.Id,
                agent.Id,
                job.Request.Command);

            return DispatchResult.Success(agent.Id);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalFailed);
            _logger.LogError(ex, "Failed to dispatch job to agent. JobId: {JobId}, AgentId: {AgentId}", job.Id, agent.Id);
            return DispatchResult.Failure($"Dispatch error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> CancelJobAsync(string jobId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobManager.GetAsync(jobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning("Cannot cancel non-existent job. JobId: {JobId}", jobId);
            return false;
        }

        // If job is running on an agent, send cancel signal
        if (job.Status == JobStatus.Running && job.AssignedAgentId is not null)
        {
            var agent = await _agentRegistry.GetAsync(job.AssignedAgentId, cancellationToken);

            if (agent?.ConnectionId is not null)
            {
                try
                {
                    await _hubContext.Clients
                        .Client(agent.ConnectionId)
                        .CancelJobAsync(jobId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send cancel signal to agent. JobId: {JobId}, AgentId: {AgentId}",
                        jobId, job.AssignedAgentId);
                }
            }
        }

        // Update job status
        var cancelled = await _jobManager.CancelAsync(jobId, reason, cancellationToken);

        if (cancelled)
        {
            _logger.LogInformation("Job cancelled. JobId: {JobId}, Reason: {Reason}", jobId, reason ?? "No reason provided");
        }

        return cancelled;
    }

    /// <inheritdoc />
    public async Task<bool> SendCancelToAgentAsync(string jobId, string agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);

        if (agent?.ConnectionId is null)
        {
            _logger.LogWarning("Cannot send cancel to agent without connection. JobId: {JobId}, AgentId: {AgentId}", jobId, agentId);
            return false;
        }

        try
        {
            await _hubContext.Clients
                .Client(agent.ConnectionId)
                .CancelJobAsync(jobId, cancellationToken);

            _logger.LogInformation("Cancel signal sent to agent. JobId: {JobId}, AgentId: {AgentId}", jobId, agentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send cancel signal to agent. JobId: {JobId}, AgentId: {AgentId}", jobId, agentId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetQueueDepthAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _jobManager.GetPendingAsync(cancellationToken);
        return pending.Count;
    }

    /// <inheritdoc />
    public async Task<DispatcherStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _jobManager.GetPendingAsync(cancellationToken);
        var running = await _jobManager.GetByStatusAsync(JobStatus.Running, cancellationToken);
        var agents = await _agentRegistry.GetAllAsync(cancellationToken);

        return new DispatcherStatistics
        {
            PendingJobs = pending.Count,
            RunningJobs = running.Count,
            ConnectedAgents = agents.Count,
            TotalDispatched = Interlocked.Read(ref _totalDispatched),
            TotalFailed = Interlocked.Read(ref _totalFailed)
        };
    }

    /// <summary>
    /// Selects an agent that has all required capabilities.
    /// Uses round-robin selection among capable agents.
    /// </summary>
    private async Task<AgentInfo?> SelectAgentByCapabilitiesAsync(
        IReadOnlyList<string> requiredCapabilities,
        CancellationToken cancellationToken)
    {
        // Start with agents that have the first capability
        var candidates = await _agentRegistry.GetByCapabilityAsync(requiredCapabilities[0], cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        // Filter to agents that have ALL required capabilities
        var qualifiedAgents = candidates
            .Where(a => a.Status == AgentStatus.Ready)
            .Where(a => requiredCapabilities.All(rc =>
                a.Capabilities.Any(ac => ac.Name.Equals(rc, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (qualifiedAgents.Count == 0)
        {
            return null;
        }

        // Simple selection: return first ready agent
        // TODO: Implement proper load balancing in IAgentRouter
        return qualifiedAgents[0];
    }

    /// <summary>
    /// Selects any available agent.
    /// </summary>
    private async Task<AgentInfo?> SelectAnyAvailableAgentAsync(CancellationToken cancellationToken)
    {
        var agents = await _agentRegistry.GetAllAsync(cancellationToken);
        return agents.FirstOrDefault(a => a.Status == AgentStatus.Ready);
    }
}
