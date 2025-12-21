using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for deployment execution persistence.
/// </summary>
public interface IDeploymentExecutionStore
{
    /// <summary>
    /// Creates a new deployment execution record.
    /// </summary>
    Task<DeploymentExecution> CreateAsync(DeploymentExecution execution, CancellationToken ct = default);

    /// <summary>
    /// Gets a deployment execution by its ID.
    /// </summary>
    Task<DeploymentExecution?> GetAsync(string executionId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing deployment execution.
    /// </summary>
    Task<DeploymentExecution> UpdateAsync(DeploymentExecution execution, CancellationToken ct = default);

    /// <summary>
    /// Deletes a deployment execution by its ID.
    /// </summary>
    Task<bool> DeleteAsync(string executionId, CancellationToken ct = default);

    /// <summary>
    /// Gets executions for a specific profile.
    /// </summary>
    Task<IReadOnlyList<DeploymentExecution>> GetByProfileAsync(
        string profileId,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Gets executions by status.
    /// </summary>
    Task<IReadOnlyList<DeploymentExecution>> GetByStatusAsync(
        DeploymentStatus status,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the latest execution for a profile.
    /// </summary>
    Task<DeploymentExecution?> GetLatestByProfileAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Gets in-progress executions.
    /// </summary>
    Task<IReadOnlyList<DeploymentExecution>> GetInProgressAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets executions with pagination.
    /// </summary>
    Task<PagedResult<DeploymentExecution>> GetPagedAsync(
        DeploymentExecutionQueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Counts executions by status.
    /// </summary>
    Task<Dictionary<DeploymentStatus, int>> GetStatusCountsAsync(
        string? profileId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes old executions for cleanup.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}

/// <summary>
/// Query options for deployment execution retrieval.
/// </summary>
public sealed record DeploymentExecutionQueryOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? ProfileId { get; init; }
    public DeploymentStatus? Status { get; init; }
    public DeploymentTrigger? Trigger { get; init; }
    public DateTimeOffset? StartedAfter { get; init; }
    public DateTimeOffset? StartedBefore { get; init; }
    public DeploymentExecutionSortField SortBy { get; init; } = DeploymentExecutionSortField.StartedAt;
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Sort fields for deployment execution queries.
/// </summary>
public enum DeploymentExecutionSortField
{
    StartedAt,
    CompletedAt,
    Status
}
