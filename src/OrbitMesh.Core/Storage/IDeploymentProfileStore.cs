using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for deployment profile persistence.
/// </summary>
public interface IDeploymentProfileStore
{
    /// <summary>
    /// Creates a new deployment profile.
    /// </summary>
    Task<DeploymentProfile> CreateAsync(DeploymentProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Gets a deployment profile by its ID.
    /// </summary>
    Task<DeploymentProfile?> GetAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing deployment profile.
    /// </summary>
    Task<DeploymentProfile> UpdateAsync(DeploymentProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Deletes a deployment profile by its ID.
    /// </summary>
    Task<bool> DeleteAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Gets all deployment profiles.
    /// </summary>
    Task<IReadOnlyList<DeploymentProfile>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets enabled deployment profiles.
    /// </summary>
    Task<IReadOnlyList<DeploymentProfile>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets deployment profiles that are watching for file changes.
    /// </summary>
    Task<IReadOnlyList<DeploymentProfile>> GetWatchingAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets deployment profiles matching an agent pattern.
    /// </summary>
    Task<IReadOnlyList<DeploymentProfile>> GetByAgentPatternAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Updates the last deployed timestamp for a profile.
    /// </summary>
    Task UpdateLastDeployedAsync(string profileId, DateTimeOffset deployedAt, CancellationToken ct = default);

    /// <summary>
    /// Gets profiles with pagination.
    /// </summary>
    Task<PagedResult<DeploymentProfile>> GetPagedAsync(
        DeploymentProfileQueryOptions options,
        CancellationToken ct = default);
}

/// <summary>
/// Query options for deployment profile retrieval.
/// </summary>
public sealed record DeploymentProfileQueryOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool? IsEnabled { get; init; }
    public bool? WatchForChanges { get; init; }
    public string? NameContains { get; init; }
    public string? TargetAgentPattern { get; init; }
    public DeploymentProfileSortField SortBy { get; init; } = DeploymentProfileSortField.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Sort fields for deployment profile queries.
/// </summary>
public enum DeploymentProfileSortField
{
    CreatedAt,
    Name,
    LastDeployedAt
}
