using OrbitMesh.Update.Models;

namespace OrbitMesh.Update.Services;

/// <summary>
/// Service for managing application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Gets the current application version.
    /// </summary>
    Version CurrentVersion { get; }

    /// <summary>
    /// Checks for updates and applies them if available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether an update is pending (requires restart).</returns>
    Task<UpdateResult> CheckAndApplyUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an update is available without applying it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Release information if an update is available.</returns>
    Task<ReleaseInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to a previous version.
    /// </summary>
    /// <param name="version">Version to roll back to, or null for previous version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the rollback operation.</returns>
    Task<UpdateResult> RollbackAsync(Version? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available backup versions for rollback.
    /// </summary>
    /// <returns>List of available versions.</returns>
    IReadOnlyList<Version> GetAvailableBackups();
}
