using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Server.Hubs;

/// <summary>
/// SignalR hub for dashboard real-time updates.
/// Broadcasts events about agents, jobs, and workflows to connected dashboard clients.
/// </summary>
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected. ConnectionId: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected. ConnectionId: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Interface for dashboard notification service to send real-time updates.
/// </summary>
public interface IDashboardNotifier
{
    // Agent events
    Task NotifyAgentConnected(string agentId);
    Task NotifyAgentDisconnected(string agentId);
    Task NotifyAgentStatusChanged(string agentId, string status);

    // Job events
    Task NotifyJobCreated(string jobId);
    Task NotifyJobStatusChanged(string jobId, string status);
    Task NotifyJobProgress(string jobId, int progress, string? message);
    Task NotifyJobCompleted(string jobId);

    // Workflow events
    Task NotifyWorkflowInstanceStarted(string instanceId, string workflowId);
    Task NotifyWorkflowInstanceCompleted(string instanceId);
    Task NotifyWorkflowStepStarted(string instanceId, string stepId);
}

/// <summary>
/// Implementation of dashboard notifier using SignalR.
/// </summary>
public class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardNotifier> _logger;

    public DashboardNotifier(IHubContext<DashboardHub> hubContext, ILogger<DashboardNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAgentConnected(string agentId)
    {
        _logger.LogDebug("Notifying dashboard: Agent connected. AgentId: {AgentId}", agentId);
        await _hubContext.Clients.All.SendAsync("AgentConnected", agentId);
    }

    public async Task NotifyAgentDisconnected(string agentId)
    {
        _logger.LogDebug("Notifying dashboard: Agent disconnected. AgentId: {AgentId}", agentId);
        await _hubContext.Clients.All.SendAsync("AgentDisconnected", agentId);
    }

    public async Task NotifyAgentStatusChanged(string agentId, string status)
    {
        _logger.LogDebug("Notifying dashboard: Agent status changed. AgentId: {AgentId}, Status: {Status}", agentId, status);
        await _hubContext.Clients.All.SendAsync("AgentStatusChanged", agentId, status);
    }

    public async Task NotifyJobCreated(string jobId)
    {
        _logger.LogDebug("Notifying dashboard: Job created. JobId: {JobId}", jobId);
        await _hubContext.Clients.All.SendAsync("JobCreated", jobId);
    }

    public async Task NotifyJobStatusChanged(string jobId, string status)
    {
        _logger.LogDebug("Notifying dashboard: Job status changed. JobId: {JobId}, Status: {Status}", jobId, status);
        await _hubContext.Clients.All.SendAsync("JobStatusChanged", jobId, status);
    }

    public async Task NotifyJobProgress(string jobId, int progress, string? message)
    {
        _logger.LogDebug("Notifying dashboard: Job progress. JobId: {JobId}, Progress: {Progress}%", jobId, progress);
        await _hubContext.Clients.All.SendAsync("JobProgress", jobId, progress, message ?? "");
    }

    public async Task NotifyJobCompleted(string jobId)
    {
        _logger.LogDebug("Notifying dashboard: Job completed. JobId: {JobId}", jobId);
        await _hubContext.Clients.All.SendAsync("JobCompleted", jobId);
    }

    public async Task NotifyWorkflowInstanceStarted(string instanceId, string workflowId)
    {
        _logger.LogDebug("Notifying dashboard: Workflow instance started. InstanceId: {InstanceId}, WorkflowId: {WorkflowId}", instanceId, workflowId);
        await _hubContext.Clients.All.SendAsync("WorkflowInstanceStarted", instanceId, workflowId);
    }

    public async Task NotifyWorkflowInstanceCompleted(string instanceId)
    {
        _logger.LogDebug("Notifying dashboard: Workflow instance completed. InstanceId: {InstanceId}", instanceId);
        await _hubContext.Clients.All.SendAsync("WorkflowInstanceCompleted", instanceId);
    }

    public async Task NotifyWorkflowStepStarted(string instanceId, string stepId)
    {
        _logger.LogDebug("Notifying dashboard: Workflow step started. InstanceId: {InstanceId}, StepId: {StepId}", instanceId, stepId);
        await _hubContext.Clients.All.SendAsync("WorkflowStepStarted", instanceId, stepId);
    }
}
