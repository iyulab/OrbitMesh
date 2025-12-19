using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// REST API controller for job management operations.
/// Requires admin authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AdminAuthorize]
public class JobsController : ControllerBase
{
    private readonly IJobOrchestrator _orchestrator;

    /// <summary>
    /// Creates a new jobs controller.
    /// </summary>
    public JobsController(IJobOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Submits a new job for execution.
    /// </summary>
    /// <param name="request">The job request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job submission result.</returns>
    /// <response code="201">Job submitted successfully.</response>
    /// <response code="400">Invalid request or submission failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(JobSubmissionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(JobSubmissionResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitJob(
        [FromBody] JobRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { Error = "Request body is required" });
        }

        var result = await _orchestrator.SubmitJobAsync(request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(
            nameof(GetJob),
            new { jobId = result.JobId },
            result);
    }

    /// <summary>
    /// Gets a job by its ID.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job details.</returns>
    /// <response code="200">Job found.</response>
    /// <response code="400">Invalid job ID.</response>
    /// <response code="404">Job not found.</response>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(Job), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest(new { Error = "Job ID is required" });
        }

        var job = await _orchestrator.GetJobAsync(jobId, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    /// <summary>
    /// Lists jobs with optional filtering.
    /// </summary>
    /// <param name="status">Filter by job status.</param>
    /// <param name="agentId">Filter by assigned agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of jobs.</returns>
    /// <response code="200">Jobs retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Job>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs(
        [FromQuery] JobStatus? status = null,
        [FromQuery] string? agentId = null,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _orchestrator.GetJobsAsync(status, agentId, cancellationToken);
        return Ok(jobs);
    }

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    /// <param name="jobId">The job ID to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    /// <response code="200">Cancellation attempted.</response>
    /// <response code="400">Invalid job ID.</response>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(typeof(CancelJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelJob(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest(new { Error = "Job ID is required" });
        }

        var cancelled = await _orchestrator.CancelJobAsync(jobId, cancellationToken);

        return Ok(new CancelJobResponse
        {
            JobId = jobId,
            Cancelled = cancelled
        });
    }
}

/// <summary>
/// Response for job cancellation requests.
/// </summary>
public sealed class CancelJobResponse
{
    /// <summary>
    /// The job ID that was requested for cancellation.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Whether the job was successfully cancelled.
    /// </summary>
    public required bool Cancelled { get; init; }
}
