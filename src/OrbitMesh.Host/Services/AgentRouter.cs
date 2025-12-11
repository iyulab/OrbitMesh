using System.Collections.Concurrent;
using System.Security.Cryptography;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Routes jobs to agents using configurable load balancing strategies.
/// </summary>
public class AgentRouter : IAgentRouter
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobManager _jobManager;

    // Round-robin state per capability group
    private readonly ConcurrentDictionary<string, int> _roundRobinIndex = new();

    /// <inheritdoc />
    public LoadBalancingStrategy Strategy { get; }

    public AgentRouter(
        IAgentRegistry agentRegistry,
        IJobManager jobManager,
        LoadBalancingStrategy strategy = LoadBalancingStrategy.RoundRobin)
    {
        _agentRegistry = agentRegistry;
        _jobManager = jobManager;
        Strategy = strategy;
    }

    /// <inheritdoc />
    public async Task<AgentInfo?> SelectAgentAsync(RoutingRequest request, CancellationToken cancellationToken = default)
    {
        // Get candidate agents
        var candidates = await GetCandidateAgentsAsync(request, cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        // Check for preferred agent first
        if (request.PreferredAgentId is not null)
        {
            var preferred = candidates.FirstOrDefault(a => a.Id == request.PreferredAgentId);
            if (preferred is not null && preferred.Status == AgentStatus.Ready)
            {
                return preferred;
            }
            // Fall through to load balancing if preferred is unavailable
        }

        // Filter to ready agents only
        var readyAgents = candidates.Where(a => a.Status == AgentStatus.Ready).ToList();

        if (readyAgents.Count == 0)
        {
            return null;
        }

        // Apply load balancing strategy
        return Strategy switch
        {
            LoadBalancingStrategy.RoundRobin => SelectRoundRobin(readyAgents, request),
            LoadBalancingStrategy.LeastConnections => await SelectLeastConnectionsAsync(readyAgents, cancellationToken),
            LoadBalancingStrategy.Random => SelectRandom(readyAgents),
            LoadBalancingStrategy.Weighted => SelectWeighted(readyAgents),
            _ => readyAgents[0]
        };
    }

    /// <summary>
    /// Gets candidate agents based on request requirements.
    /// </summary>
    private async Task<IReadOnlyList<AgentInfo>> GetCandidateAgentsAsync(
        RoutingRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentInfo> candidates;

        // Start with group filter if specified
        if (request.TargetGroup is not null)
        {
            candidates = await _agentRegistry.GetByGroupAsync(request.TargetGroup, cancellationToken);
        }
        else if (request.RequiredCapabilities is { Count: > 0 })
        {
            // Get agents with first capability, then filter by all
            candidates = await _agentRegistry.GetByCapabilityAsync(
                request.RequiredCapabilities[0], cancellationToken);
        }
        else
        {
            candidates = await _agentRegistry.GetAllAsync(cancellationToken);
        }

        // Apply all filters
        var filtered = candidates.AsEnumerable();

        // Filter by all required capabilities
        if (request.RequiredCapabilities is { Count: > 0 })
        {
            filtered = filtered.Where(a =>
                request.RequiredCapabilities.All(rc =>
                    a.Capabilities.Any(ac => ac.Name.Equals(rc, StringComparison.OrdinalIgnoreCase))));
        }

        // Filter by required tags
        if (request.RequiredTags is { Count: > 0 })
        {
            filtered = filtered.Where(a =>
                request.RequiredTags.All(rt => a.HasTag(rt)));
        }

        // Exclude specific agents
        if (request.ExcludedAgentIds is { Count: > 0 })
        {
            filtered = filtered.Where(a => !request.ExcludedAgentIds.Contains(a.Id));
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Round-robin selection across agents.
    /// </summary>
    private AgentInfo SelectRoundRobin(List<AgentInfo> agents, RoutingRequest request)
    {
        // Create a key for this capability group
        var groupKey = string.Join(",", request.RequiredCapabilities.OrderBy(c => c));
        if (string.IsNullOrEmpty(groupKey))
        {
            groupKey = "_all";
        }

        var index = _roundRobinIndex.AddOrUpdate(
            groupKey,
            0,
            (_, current) => current + 1);

        return agents[index % agents.Count];
    }

    /// <summary>
    /// Least connections selection - picks agent with fewest running jobs.
    /// </summary>
    private async Task<AgentInfo> SelectLeastConnectionsAsync(
        List<AgentInfo> agents,
        CancellationToken cancellationToken)
    {
        var agentLoads = new List<(AgentInfo Agent, int RunningJobs)>();

        foreach (var agent in agents)
        {
            var jobs = await _jobManager.GetByAgentAsync(agent.Id, cancellationToken);
            var runningCount = jobs.Count(j => j.Status == JobStatus.Running);
            agentLoads.Add((agent, runningCount));
        }

        return agentLoads
            .OrderBy(x => x.RunningJobs)
            .First()
            .Agent;
    }

    /// <summary>
    /// Random selection among agents.
    /// </summary>
    private static AgentInfo SelectRandom(List<AgentInfo> agents)
    {
        var index = RandomNumberGenerator.GetInt32(agents.Count);
        return agents[index];
    }

    /// <summary>
    /// Weighted selection based on agent metadata.
    /// Agents can specify a "weight" in their metadata.
    /// </summary>
    private static AgentInfo SelectWeighted(List<AgentInfo> agents)
    {
        var weightedAgents = agents.Select(a =>
        {
            var weight = 1;
            if (a.Metadata?.TryGetValue("weight", out var weightStr) == true &&
                int.TryParse(weightStr, out var parsedWeight))
            {
                weight = Math.Max(1, parsedWeight);
            }
            return (Agent: a, Weight: weight);
        }).ToList();

        var totalWeight = weightedAgents.Sum(x => x.Weight);
        var randomValue = RandomNumberGenerator.GetInt32(totalWeight);

        var cumulative = 0;
        foreach (var (agent, weight) in weightedAgents)
        {
            cumulative += weight;
            if (randomValue < cumulative)
            {
                return agent;
            }
        }

        return agents[0];
    }
}
