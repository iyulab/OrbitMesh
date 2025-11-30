using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Routes jobs to appropriate agents based on capabilities and load balancing strategy.
/// </summary>
public interface IAgentRouter
{
    /// <summary>
    /// Selects an appropriate agent based on the routing request.
    /// </summary>
    /// <param name="request">The routing request with requirements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected agent or null if none available.</returns>
    Task<AgentInfo?> SelectAgentAsync(RoutingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current load balancing strategy.
    /// </summary>
    LoadBalancingStrategy Strategy { get; }
}

/// <summary>
/// Load balancing strategies for agent selection.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>
    /// Distributes jobs evenly across agents in round-robin fashion.
    /// </summary>
    RoundRobin = 0,

    /// <summary>
    /// Selects the agent with the fewest currently running jobs.
    /// </summary>
    LeastConnections = 1,

    /// <summary>
    /// Randomly selects an available agent.
    /// </summary>
    Random = 2,

    /// <summary>
    /// Selects agents based on weighted priority (from metadata).
    /// </summary>
    Weighted = 3
}

/// <summary>
/// Request for routing a job to an agent.
/// </summary>
public sealed record RoutingRequest
{
    /// <summary>
    /// Required capabilities for the agent.
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];

    /// <summary>
    /// Preferred agent ID (if available and capable).
    /// </summary>
    public string? PreferredAgentId { get; init; }

    /// <summary>
    /// Target agent group.
    /// </summary>
    public string? TargetGroup { get; init; }

    /// <summary>
    /// Required tags for the agent.
    /// </summary>
    public IReadOnlyList<string>? RequiredTags { get; init; }

    /// <summary>
    /// Excluded agent IDs (e.g., for retry on different agent).
    /// </summary>
    public IReadOnlyList<string>? ExcludedAgentIds { get; init; }

    /// <summary>
    /// Creates a routing request from a job request.
    /// </summary>
    public static RoutingRequest FromJobRequest(JobRequest jobRequest) =>
        new()
        {
            RequiredCapabilities = jobRequest.RequiredCapabilities ?? [],
            PreferredAgentId = jobRequest.TargetAgentId
        };
}
