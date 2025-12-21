using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services.Deployment;

/// <summary>
/// Service for orchestrating deployments to agents.
/// Handles the full deployment lifecycle: Pre-Script → File Sync → Post-Script.
/// </summary>
public interface IDeploymentService
{
    /// <summary>
    /// Event raised when a deployment execution status changes.
    /// </summary>
    event EventHandler<DeploymentStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Event raised when deployment progress is updated.
    /// </summary>
    event EventHandler<DeploymentProgressEventArgs>? ProgressUpdated;

    /// <summary>
    /// Triggers a deployment for a profile.
    /// </summary>
    /// <param name="profileId">The deployment profile ID.</param>
    /// <param name="trigger">What triggered this deployment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deployment execution record.</returns>
    Task<DeploymentExecution> DeployAsync(
        string profileId,
        DeploymentTrigger trigger = DeploymentTrigger.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels an in-progress deployment.
    /// </summary>
    /// <param name="executionId">The execution ID to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if cancellation was successful.</returns>
    Task<bool> CancelAsync(string executionId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current status of a deployment execution.
    /// </summary>
    /// <param name="executionId">The execution ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deployment execution or null if not found.</returns>
    Task<DeploymentExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default);

    /// <summary>
    /// Gets all in-progress deployments.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of in-progress executions.</returns>
    Task<IReadOnlyList<DeploymentExecution>> GetInProgressAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets agents that match a profile's target pattern.
    /// </summary>
    /// <param name="profileId">The profile ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching agent IDs and names.</returns>
    Task<IReadOnlyList<(string Id, string Name)>> GetMatchingAgentsAsync(
        string profileId,
        CancellationToken ct = default);
}

/// <summary>
/// Event arguments for deployment status changes.
/// </summary>
public sealed class DeploymentStatusChangedEventArgs : EventArgs
{
    /// <summary>The execution ID.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The profile ID.</summary>
    public required string ProfileId { get; init; }

    /// <summary>Previous status.</summary>
    public required DeploymentStatus PreviousStatus { get; init; }

    /// <summary>New status.</summary>
    public required DeploymentStatus NewStatus { get; init; }

    /// <summary>Timestamp of the change.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event arguments for deployment progress updates.
/// </summary>
public sealed class DeploymentProgressEventArgs : EventArgs
{
    /// <summary>The execution ID.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The profile ID.</summary>
    public required string ProfileId { get; init; }

    /// <summary>Agent ID being processed.</summary>
    public required string AgentId { get; init; }

    /// <summary>Current phase of deployment.</summary>
    public required DeploymentPhase Phase { get; init; }

    /// <summary>Progress message.</summary>
    public string? Message { get; init; }

    /// <summary>Timestamp of the update.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Phases of a deployment operation.
/// </summary>
public enum DeploymentPhase
{
    /// <summary>Starting deployment.</summary>
    Starting,

    /// <summary>Running pre-deploy script.</summary>
    PreScript,

    /// <summary>Syncing files.</summary>
    FileSync,

    /// <summary>Running post-deploy script.</summary>
    PostScript,

    /// <summary>Completed successfully.</summary>
    Completed,

    /// <summary>Failed.</summary>
    Failed
}
