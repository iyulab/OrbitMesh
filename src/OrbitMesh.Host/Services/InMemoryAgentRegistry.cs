using System.Collections.Concurrent;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// In-memory implementation of the agent registry.
/// Suitable for single-server deployments.
/// </summary>
public class InMemoryAgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();

    /// <inheritdoc />
    public Task RegisterAsync(AgentInfo agent, CancellationToken cancellationToken = default)
    {
        _agents.AddOrUpdate(agent.Id, agent, (_, _) => agent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default)
    {
        _agents.TryRemove(agentId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AgentInfo?> GetAsync(string agentId, CancellationToken cancellationToken = default)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AgentInfo>>(_agents.Values.ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentInfo>> GetByCapabilityAsync(
        string capability,
        CancellationToken cancellationToken = default)
    {
        var agents = _agents.Values
            .Where(a => a.HasCapability(capability))
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentInfo>>(agents);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentInfo>> GetByGroupAsync(
        string group,
        CancellationToken cancellationToken = default)
    {
        var agents = _agents.Values
            .Where(a => a.Group?.Equals(group, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentInfo>>(agents);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(
        string agentId,
        AgentStatus status,
        CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            _agents.TryUpdate(agentId, agent with { Status = status }, agent);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateHeartbeatAsync(
        string agentId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            _agents.TryUpdate(agentId, agent with { LastHeartbeat = timestamp }, agent);
        }

        return Task.CompletedTask;
    }
}
