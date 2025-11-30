using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Health check for agent availability.
/// </summary>
public sealed class AgentHealthCheck : IHealthCheck
{
    private readonly IAgentRegistry _registry;

    /// <summary>
    /// Creates a new agent health check.
    /// </summary>
    public AgentHealthCheck(IAgentRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var agents = await _registry.GetAllAsync(cancellationToken);
            var agentList = agents.ToList();

            var totalCount = agentList.Count;
            var readyCount = agentList.Count(a => a.Status == AgentStatus.Ready);
            var runningCount = agentList.Count(a => a.Status == AgentStatus.Running);
            var disconnectedCount = agentList.Count(a => a.Status == AgentStatus.Disconnected);

            var data = new Dictionary<string, object>
            {
                ["TotalAgents"] = totalCount,
                ["ReadyAgents"] = readyCount,
                ["RunningAgents"] = runningCount,
                ["DisconnectedAgents"] = disconnectedCount
            };

            if (totalCount == 0)
            {
                return HealthCheckResult.Degraded(
                    "No agents registered in the mesh",
                    data: data);
            }

            var activeCount = readyCount + runningCount;
            if (activeCount == 0)
            {
                return HealthCheckResult.Degraded(
                    $"No active agents available (Total: {totalCount}, Disconnected: {disconnectedCount})",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"{activeCount} agent(s) available ({readyCount} ready, {runningCount} running)",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check agent health",
                ex);
        }
    }
}
