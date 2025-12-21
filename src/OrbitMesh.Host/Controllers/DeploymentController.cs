using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Core.Models;
using OrbitMesh.Core.Storage;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Services.Deployment;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// REST API controller for deployment profile management.
/// Handles CRUD operations for deployment profiles and deployment execution.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AdminAuthorize]
public class DeploymentController : ControllerBase
{
    private readonly IDeploymentProfileStore _profileStore;
    private readonly IDeploymentExecutionStore _executionStore;
    private readonly IDeploymentService _deploymentService;

    /// <summary>
    /// Creates a new deployment controller.
    /// </summary>
    public DeploymentController(
        IDeploymentProfileStore profileStore,
        IDeploymentExecutionStore executionStore,
        IDeploymentService deploymentService)
    {
        _profileStore = profileStore;
        _executionStore = executionStore;
        _deploymentService = deploymentService;
    }

    // ─────────────────────────────────────────────────────────────
    // Profile CRUD
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all deployment profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of deployment profiles.</returns>
    /// <response code="200">Profiles retrieved successfully.</response>
    [HttpGet("profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<DeploymentProfile>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProfiles(CancellationToken cancellationToken = default)
    {
        var profiles = await _profileStore.GetAllAsync(cancellationToken);
        return Ok(profiles);
    }

    /// <summary>
    /// Gets a deployment profile by ID.
    /// </summary>
    /// <param name="id">The profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deployment profile.</returns>
    /// <response code="200">Profile found.</response>
    /// <response code="404">Profile not found.</response>
    [HttpGet("profiles/{id}")]
    [ProducesResponseType(typeof(DeploymentProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string id, CancellationToken cancellationToken = default)
    {
        var profile = await _profileStore.GetAsync(id, cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    /// <summary>
    /// Creates a new deployment profile.
    /// </summary>
    /// <param name="request">The profile creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created profile.</returns>
    /// <response code="201">Profile created successfully.</response>
    /// <response code="400">Invalid profile data.</response>
    [HttpPost("profiles")]
    [ProducesResponseType(typeof(DeploymentProfile), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required");
        }

        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            return BadRequest("SourcePath is required");
        }

        if (string.IsNullOrWhiteSpace(request.TargetAgentPattern))
        {
            return BadRequest("TargetAgentPattern is required");
        }

        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return BadRequest("TargetPath is required");
        }

        var profile = new DeploymentProfile
        {
            Id = DeploymentProfile.GenerateId(),
            Name = request.Name,
            Description = request.Description,
            SourcePath = request.SourcePath,
            TargetAgentPattern = request.TargetAgentPattern,
            TargetPath = request.TargetPath,
            WatchForChanges = request.WatchForChanges ?? true,
            DebounceMs = request.DebounceMs ?? 500,
            IncludePatterns = request.IncludePatterns,
            ExcludePatterns = request.ExcludePatterns,
            DeleteOrphans = request.DeleteOrphans ?? false,
            PreDeployScript = request.PreDeployScript,
            PostDeployScript = request.PostDeployScript,
            TransferMode = request.TransferMode ?? FileTransferMode.Auto,
            IsEnabled = request.IsEnabled ?? true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        profile = await _profileStore.CreateAsync(profile, cancellationToken);

        return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
    }

    /// <summary>
    /// Updates a deployment profile.
    /// </summary>
    /// <param name="id">The profile ID.</param>
    /// <param name="request">The profile update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated profile.</returns>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="404">Profile not found.</response>
    [HttpPut("profiles/{id}")]
    [ProducesResponseType(typeof(DeploymentProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        string id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _profileStore.GetAsync(id, cancellationToken);

        if (existing is null)
        {
            return NotFound();
        }

        var profile = existing with
        {
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            SourcePath = request.SourcePath ?? existing.SourcePath,
            TargetAgentPattern = request.TargetAgentPattern ?? existing.TargetAgentPattern,
            TargetPath = request.TargetPath ?? existing.TargetPath,
            WatchForChanges = request.WatchForChanges ?? existing.WatchForChanges,
            DebounceMs = request.DebounceMs ?? existing.DebounceMs,
            IncludePatterns = request.IncludePatterns ?? existing.IncludePatterns,
            ExcludePatterns = request.ExcludePatterns ?? existing.ExcludePatterns,
            DeleteOrphans = request.DeleteOrphans ?? existing.DeleteOrphans,
            PreDeployScript = request.PreDeployScript ?? existing.PreDeployScript,
            PostDeployScript = request.PostDeployScript ?? existing.PostDeployScript,
            TransferMode = request.TransferMode ?? existing.TransferMode,
            IsEnabled = request.IsEnabled ?? existing.IsEnabled
        };

        profile = await _profileStore.UpdateAsync(profile, cancellationToken);

        return Ok(profile);
    }

    /// <summary>
    /// Deletes a deployment profile.
    /// </summary>
    /// <param name="id">The profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Profile deleted successfully.</response>
    /// <response code="404">Profile not found.</response>
    [HttpDelete("profiles/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(string id, CancellationToken cancellationToken = default)
    {
        var deleted = await _profileStore.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────
    // Deployment Operations
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a deployment for a profile.
    /// </summary>
    /// <param name="id">The profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deployment execution.</returns>
    /// <response code="202">Deployment started successfully.</response>
    /// <response code="404">Profile not found.</response>
    /// <response code="400">Profile is disabled or invalid.</response>
    [HttpPost("profiles/{id}/deploy")]
    [ProducesResponseType(typeof(DeploymentExecution), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deploy(string id, CancellationToken cancellationToken = default)
    {
        var profile = await _profileStore.GetAsync(id, cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        try
        {
            var execution = await _deploymentService.DeployAsync(id, DeploymentTrigger.Manual, cancellationToken);
            return AcceptedAtAction(nameof(GetExecution), new { id = execution.Id }, execution);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets matching agents for a profile's target pattern.
    /// </summary>
    /// <param name="id">The profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching agent IDs and names.</returns>
    /// <response code="200">Agents retrieved successfully.</response>
    /// <response code="404">Profile not found.</response>
    [HttpGet("profiles/{id}/agents")]
    [ProducesResponseType(typeof(IReadOnlyList<MatchingAgentInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatchingAgents(string id, CancellationToken cancellationToken = default)
    {
        var profile = await _profileStore.GetAsync(id, cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        var agents = await _deploymentService.GetMatchingAgentsAsync(id, cancellationToken);
        var result = agents.Select(a => new MatchingAgentInfo { Id = a.Id, Name = a.Name }).ToList();

        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────
    // Execution History
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists deployment executions.
    /// </summary>
    /// <param name="profileId">Optional profile ID filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of deployment executions.</returns>
    /// <response code="200">Executions retrieved successfully.</response>
    [HttpGet("executions")]
    [ProducesResponseType(typeof(PagedResult<DeploymentExecution>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExecutions(
        [FromQuery] string? profileId = null,
        [FromQuery] DeploymentStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var options = new DeploymentExecutionQueryOptions
        {
            Page = page,
            PageSize = pageSize,
            ProfileId = profileId,
            Status = status
        };

        var result = await _executionStore.GetPagedAsync(options, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a deployment execution by ID.
    /// </summary>
    /// <param name="id">The execution ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deployment execution.</returns>
    /// <response code="200">Execution found.</response>
    /// <response code="404">Execution not found.</response>
    [HttpGet("executions/{id}")]
    [ProducesResponseType(typeof(DeploymentExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecution(string id, CancellationToken cancellationToken = default)
    {
        var execution = await _executionStore.GetAsync(id, cancellationToken);

        if (execution is null)
        {
            return NotFound();
        }

        return Ok(execution);
    }

    /// <summary>
    /// Cancels an in-progress deployment.
    /// </summary>
    /// <param name="id">The execution ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Cancellation requested.</response>
    /// <response code="404">Execution not found or not cancellable.</response>
    [HttpPost("executions/{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelExecution(string id, CancellationToken cancellationToken = default)
    {
        var cancelled = await _deploymentService.CancelAsync(id, cancellationToken);

        if (!cancelled)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Gets in-progress deployments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of in-progress executions.</returns>
    /// <response code="200">In-progress executions retrieved.</response>
    [HttpGet("executions/in-progress")]
    [ProducesResponseType(typeof(IReadOnlyList<DeploymentExecution>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInProgressExecutions(CancellationToken cancellationToken = default)
    {
        var executions = await _deploymentService.GetInProgressAsync(cancellationToken);
        return Ok(executions);
    }

    /// <summary>
    /// Gets execution status counts for dashboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status counts.</returns>
    /// <response code="200">Status counts retrieved.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DeploymentStatusCounts), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusCounts(CancellationToken cancellationToken = default)
    {
        var counts = await _executionStore.GetStatusCountsAsync(null, cancellationToken);

        return Ok(new DeploymentStatusCounts
        {
            Pending = counts.GetValueOrDefault(DeploymentStatus.Pending),
            InProgress = counts.GetValueOrDefault(DeploymentStatus.InProgress),
            Succeeded = counts.GetValueOrDefault(DeploymentStatus.Succeeded),
            Failed = counts.GetValueOrDefault(DeploymentStatus.Failed),
            PartialSuccess = counts.GetValueOrDefault(DeploymentStatus.PartialSuccess),
            Cancelled = counts.GetValueOrDefault(DeploymentStatus.Cancelled)
        });
    }
}

// ─────────────────────────────────────────────────────────────
// Request/Response DTOs
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Request to create a new deployment profile.
/// </summary>
public sealed class CreateProfileRequest
{
    /// <summary>Profile name.</summary>
    public required string Name { get; set; }

    /// <summary>Profile description.</summary>
    public string? Description { get; set; }

    /// <summary>Source path on the server.</summary>
    public required string SourcePath { get; set; }

    /// <summary>Target agent pattern (wildcards supported).</summary>
    public required string TargetAgentPattern { get; set; }

    /// <summary>Target path on the agents.</summary>
    public required string TargetPath { get; set; }

    /// <summary>Whether to watch for file changes.</summary>
    public bool? WatchForChanges { get; set; }

    /// <summary>Debounce delay in milliseconds.</summary>
    public int? DebounceMs { get; set; }

    /// <summary>File include patterns.</summary>
    public IReadOnlyList<string>? IncludePatterns { get; set; }

    /// <summary>File exclude patterns.</summary>
    public IReadOnlyList<string>? ExcludePatterns { get; set; }

    /// <summary>Whether to delete orphan files on target.</summary>
    public bool? DeleteOrphans { get; set; }

    /// <summary>Script to run before deployment.</summary>
    public DeploymentScript? PreDeployScript { get; set; }

    /// <summary>Script to run after deployment.</summary>
    public DeploymentScript? PostDeployScript { get; set; }

    /// <summary>File transfer mode.</summary>
    public FileTransferMode? TransferMode { get; set; }

    /// <summary>Whether the profile is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Request to update a deployment profile.
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>Profile name.</summary>
    public string? Name { get; set; }

    /// <summary>Profile description.</summary>
    public string? Description { get; set; }

    /// <summary>Source path on the server.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Target agent pattern (wildcards supported).</summary>
    public string? TargetAgentPattern { get; set; }

    /// <summary>Target path on the agents.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Whether to watch for file changes.</summary>
    public bool? WatchForChanges { get; set; }

    /// <summary>Debounce delay in milliseconds.</summary>
    public int? DebounceMs { get; set; }

    /// <summary>File include patterns.</summary>
    public IReadOnlyList<string>? IncludePatterns { get; set; }

    /// <summary>File exclude patterns.</summary>
    public IReadOnlyList<string>? ExcludePatterns { get; set; }

    /// <summary>Whether to delete orphan files on target.</summary>
    public bool? DeleteOrphans { get; set; }

    /// <summary>Script to run before deployment.</summary>
    public DeploymentScript? PreDeployScript { get; set; }

    /// <summary>Script to run after deployment.</summary>
    public DeploymentScript? PostDeployScript { get; set; }

    /// <summary>File transfer mode.</summary>
    public FileTransferMode? TransferMode { get; set; }

    /// <summary>Whether the profile is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Matching agent information.
/// </summary>
public sealed class MatchingAgentInfo
{
    /// <summary>Agent ID.</summary>
    public required string Id { get; set; }

    /// <summary>Agent name.</summary>
    public required string Name { get; set; }
}

/// <summary>
/// Deployment status counts for dashboard.
/// </summary>
public sealed class DeploymentStatusCounts
{
    /// <summary>Count of pending deployments.</summary>
    public int Pending { get; set; }

    /// <summary>Count of in-progress deployments.</summary>
    public int InProgress { get; set; }

    /// <summary>Count of succeeded deployments.</summary>
    public int Succeeded { get; set; }

    /// <summary>Count of failed deployments.</summary>
    public int Failed { get; set; }

    /// <summary>Count of partially succeeded deployments.</summary>
    public int PartialSuccess { get; set; }

    /// <summary>Count of cancelled deployments.</summary>
    public int Cancelled { get; set; }
}
