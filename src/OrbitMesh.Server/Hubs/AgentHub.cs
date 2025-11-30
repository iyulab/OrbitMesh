using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Hubs;

/// <summary>
/// SignalR hub for agent communication.
/// Implements the server-side of the OrbitMesh protocol.
/// </summary>
public class AgentHub : Hub<IAgentClient>, IServerHub
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobManager _jobManager;
    private readonly IProgressService _progressService;
    private readonly IStreamingService _streamingService;
    private readonly ILogger<AgentHub> _logger;

    /// <summary>
    /// SignalR group for all connected agents.
    /// </summary>
    public const string AllAgentsGroup = "all-agents";

    public AgentHub(
        IAgentRegistry agentRegistry,
        IJobManager jobManager,
        IProgressService progressService,
        IStreamingService streamingService,
        ILogger<AgentHub> logger)
    {
        _agentRegistry = agentRegistry;
        _jobManager = jobManager;
        _progressService = progressService;
        _streamingService = streamingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Agent connection initiated. ConnectionId: {ConnectionId}",
            Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup);
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = Context.Items["AgentId"] as string;

        if (agentId is not null)
        {
            await _agentRegistry.UpdateStatusAsync(agentId, AgentStatus.Disconnected);

            _logger.LogInformation(
                "Agent disconnected. AgentId: {AgentId}, ConnectionId: {ConnectionId}, Reason: {Reason}",
                agentId,
                Context.ConnectionId,
                exception?.Message ?? "Normal disconnect");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllAgentsGroup);
        await base.OnDisconnectedAsync(exception);
    }

    /// <inheritdoc />
    public async Task<AgentRegistrationResult> RegisterAsync(
        AgentInfo agentInfo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Store agent ID in connection context
            Context.Items["AgentId"] = agentInfo.Id;

            // Update agent with connection ID
            var registeredAgent = agentInfo with
            {
                ConnectionId = Context.ConnectionId,
                Status = AgentStatus.Ready,
                LastHeartbeat = DateTimeOffset.UtcNow
            };

            await _agentRegistry.RegisterAsync(registeredAgent, cancellationToken);

            // Add to capability-based groups
            foreach (var capability in agentInfo.Capabilities)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"capability:{capability.Name}",
                    cancellationToken);
            }

            // Add to agent group if specified
            if (agentInfo.Group is not null)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"group:{agentInfo.Group}",
                    cancellationToken);
            }

            _logger.LogInformation(
                "Agent registered. AgentId: {AgentId}, Name: {Name}, Capabilities: {Capabilities}",
                agentInfo.Id,
                agentInfo.Name,
                string.Join(", ", agentInfo.Capabilities.Select(c => c.Name)));

            return AgentRegistrationResult.Succeeded(TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent. AgentId: {AgentId}", agentInfo.Id);
            return AgentRegistrationResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await _agentRegistry.UnregisterAsync(agentId, cancellationToken);

        _logger.LogInformation(
            "Agent unregistered. AgentId: {AgentId}, ConnectionId: {ConnectionId}",
            agentId,
            Context.ConnectionId);
    }

    /// <inheritdoc />
    public async Task HeartbeatAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await _agentRegistry.UpdateHeartbeatAsync(agentId, DateTimeOffset.UtcNow, cancellationToken);

        _logger.LogDebug("Heartbeat received. AgentId: {AgentId}", agentId);
    }

    /// <inheritdoc />
    public async Task AcknowledgeJobAsync(
        string jobId,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var acknowledged = await _jobManager.AcknowledgeAsync(jobId, agentId, cancellationToken);

        if (acknowledged)
        {
            _logger.LogInformation(
                "Job acknowledged. JobId: {JobId}, AgentId: {AgentId}",
                jobId,
                agentId);
        }
        else
        {
            _logger.LogWarning(
                "Job acknowledgment failed. JobId: {JobId}, AgentId: {AgentId}",
                jobId,
                agentId);
        }
    }

    /// <inheritdoc />
    public async Task ReportResultAsync(JobResult result, CancellationToken cancellationToken = default)
    {
        bool updated;

        if (result.IsSuccess)
        {
            updated = await _jobManager.CompleteAsync(result.JobId, result, cancellationToken);
        }
        else
        {
            updated = await _jobManager.FailAsync(
                result.JobId,
                result.Error ?? "Unknown error",
                result.ErrorCode,
                cancellationToken);
        }

        if (updated)
        {
            _logger.LogInformation(
                "Job result processed. JobId: {JobId}, Status: {Status}, Duration: {Duration}ms",
                result.JobId,
                result.Status,
                result.Duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Job result could not be processed (job may already be completed). JobId: {JobId}",
                result.JobId);
        }

        // Clear progress tracking for completed job
        await _progressService.ClearProgressAsync(result.JobId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        // Update progress in the service (notifies subscribers)
        await _progressService.ReportProgressAsync(progress, cancellationToken);

        // Also update the job manager for persistence
        await _jobManager.UpdateProgressAsync(progress, cancellationToken);

        _logger.LogDebug(
            "Job progress received. JobId: {JobId}, Progress: {Progress}%, Message: {Message}",
            progress.JobId,
            progress.Percentage,
            progress.Message ?? "N/A");
    }

    /// <inheritdoc />
    public async Task ReportStateAsync(
        string agentId,
        IReadOnlyDictionary<string, string> reportedState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Agent state reported. AgentId: {AgentId}, Properties: {Count}",
            agentId,
            reportedState.Count);

        // State reporting will be handled by the state manager
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReportStreamItemAsync(StreamItem item, CancellationToken cancellationToken = default)
    {
        // Publish to streaming service (notifies all subscribers)
        await _streamingService.PublishAsync(item, cancellationToken);

        _logger.LogDebug(
            "Stream item received. JobId: {JobId}, Sequence: {Sequence}, Size: {Size} bytes, IsEnd: {IsEnd}",
            item.JobId,
            item.SequenceNumber,
            item.Data.Length,
            item.IsEndOfStream);

        // If end of stream, mark stream as complete
        if (item.IsEndOfStream)
        {
            await _streamingService.CompleteStreamAsync(item.JobId, cancellationToken);
        }
    }

    /// <summary>
    /// SignalR streaming endpoint for clients to subscribe to job streams.
    /// Clients can call this method to receive streaming data as an IAsyncEnumerable.
    /// </summary>
    /// <param name="jobId">The job ID to subscribe to.</param>
    /// <param name="fromSequence">Optional sequence number to start from (for replay).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of stream items.</returns>
    public IAsyncEnumerable<StreamItem> SubscribeToStream(
        string jobId,
        long fromSequence = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Client subscribing to stream. JobId: {JobId}, FromSequence: {FromSequence}, ConnectionId: {ConnectionId}",
            jobId,
            fromSequence,
            Context.ConnectionId);

        return _streamingService.SubscribeAsync(jobId, cancellationToken);
    }
}
