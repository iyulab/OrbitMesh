using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for agent persistence.
/// </summary>
public interface IAgentStore
{
    /// <summary>
    /// Registers or updates an agent.
    /// </summary>
    Task<AgentInfo> UpsertAsync(AgentInfo agent, CancellationToken ct = default);

    /// <summary>
    /// Gets an agent by its ID.
    /// </summary>
    Task<AgentInfo?> GetAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Gets an agent by its connection ID.
    /// </summary>
    Task<AgentInfo?> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default);

    /// <summary>
    /// Removes an agent from the store.
    /// </summary>
    Task<bool> RemoveAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Gets all agents.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets agents by status.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetByStatusAsync(AgentStatus status, CancellationToken ct = default);

    /// <summary>
    /// Gets agents by group.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetByGroupAsync(string group, CancellationToken ct = default);

    /// <summary>
    /// Gets agents that have a specific capability.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetByCapabilityAsync(string capability, CancellationToken ct = default);

    /// <summary>
    /// Gets agents with pagination and optional filtering.
    /// </summary>
    Task<PagedResult<AgentInfo>> GetPagedAsync(
        AgentQueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the heartbeat timestamp for an agent.
    /// </summary>
    Task UpdateHeartbeatAsync(string agentId, DateTimeOffset timestamp, CancellationToken ct = default);

    /// <summary>
    /// Updates the status of an agent.
    /// </summary>
    Task UpdateStatusAsync(string agentId, AgentStatus status, CancellationToken ct = default);

    /// <summary>
    /// Gets agents that haven't sent a heartbeat within the timeout period.
    /// </summary>
    Task<IReadOnlyList<AgentInfo>> GetStaleAgentsAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Counts agents by status.
    /// </summary>
    Task<Dictionary<AgentStatus, int>> GetStatusCountsAsync(CancellationToken ct = default);
}

/// <summary>
/// Query options for agent retrieval.
/// </summary>
public sealed record AgentQueryOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public AgentStatus? Status { get; init; }
    public string? Group { get; init; }
    public string? Capability { get; init; }
    public string? SearchTerm { get; init; }
    public AgentSortField SortBy { get; init; } = AgentSortField.Name;
    public bool SortDescending { get; init; }
}

/// <summary>
/// Sort fields for agent queries.
/// </summary>
public enum AgentSortField
{
    Name,
    RegisteredAt,
    LastHeartbeat,
    Status,
    Group
}
