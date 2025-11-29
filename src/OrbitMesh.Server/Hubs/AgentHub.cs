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
    private readonly ILogger<AgentHub> _logger;

    /// <summary>
    /// SignalR group for all connected agents.
    /// </summary>
    public const string AllAgentsGroup = "all-agents";

    public AgentHub(IAgentRegistry agentRegistry, ILogger<AgentHub> logger)
    {
        _agentRegistry = agentRegistry;
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
        _logger.LogInformation(
            "Job acknowledged. JobId: {JobId}, AgentId: {AgentId}",
            jobId,
            agentId);

        // Job acknowledgment will be handled by the job manager
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReportResultAsync(JobResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Job result received. JobId: {JobId}, Status: {Status}, Duration: {Duration}ms",
            result.JobId,
            result.Status,
            result.Duration.TotalMilliseconds);

        // Job result will be handled by the job manager
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Job progress received. JobId: {JobId}, Progress: {Progress}%",
            progress.JobId,
            progress.Percentage);

        // Job progress will be handled by the job manager
        await Task.CompletedTask;
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
}
