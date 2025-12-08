using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Storage;

/// <summary>
/// Storage abstraction for job persistence.
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// Creates a new job in the store.
    /// </summary>
    Task<Job> CreateAsync(Job job, CancellationToken ct = default);

    /// <summary>
    /// Gets a job by its ID.
    /// </summary>
    Task<Job?> GetAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing job.
    /// </summary>
    Task<Job> UpdateAsync(Job job, CancellationToken ct = default);

    /// <summary>
    /// Deletes a job by its ID.
    /// </summary>
    Task<bool> DeleteAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Gets jobs by status.
    /// </summary>
    Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken ct = default);

    /// <summary>
    /// Gets jobs assigned to a specific agent.
    /// </summary>
    Task<IReadOnlyList<Job>> GetByAgentAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Gets jobs with pagination and optional filtering.
    /// </summary>
    Task<PagedResult<Job>> GetPagedAsync(
        JobQueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Gets pending jobs ordered by priority and creation time.
    /// </summary>
    Task<IReadOnlyList<Job>> GetPendingJobsAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Gets jobs that have timed out.
    /// </summary>
    Task<IReadOnlyList<Job>> GetTimedOutJobsAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts jobs by status.
    /// </summary>
    Task<Dictionary<JobStatus, int>> GetStatusCountsAsync(CancellationToken ct = default);
}

/// <summary>
/// Query options for job retrieval.
/// </summary>
public sealed record JobQueryOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public JobStatus? Status { get; init; }
    public string? AgentId { get; init; }
    public string? Command { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public DateTimeOffset? CreatedBefore { get; init; }
    public JobSortField SortBy { get; init; } = JobSortField.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Sort fields for job queries.
/// </summary>
public enum JobSortField
{
    CreatedAt,
    AssignedAt,
    CompletedAt,
    Priority,
    Status
}
