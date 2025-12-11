using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Core.Enums;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// REST API controller for server status information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusController : ControllerBase
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobOrchestrator _jobOrchestrator;
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new status controller.
    /// </summary>
    public StatusController(IAgentRegistry agentRegistry, IJobOrchestrator jobOrchestrator)
    {
        _agentRegistry = agentRegistry;
        _jobOrchestrator = jobOrchestrator;
    }

    /// <summary>
    /// Gets the current server status and statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server status information.</returns>
    /// <response code="200">Status retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ServerStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        var agents = await _agentRegistry.GetAllAsync(cancellationToken);
        var jobs = await _jobOrchestrator.GetJobsAsync(null, null, cancellationToken);

        var agentStats = new AgentStats
        {
            Total = agents.Count,
            Ready = agents.Count(a => a.Status == AgentStatus.Ready),
            Busy = agents.Count(a => a.Status == AgentStatus.Running),
            Disconnected = agents.Count(a => a.Status == AgentStatus.Disconnected),
        };

        var jobStats = new JobStats
        {
            Pending = jobs.Count(j => j.Status == JobStatus.Pending),
            Running = jobs.Count(j => j.Status is JobStatus.Running or JobStatus.Assigned),
            Completed = jobs.Count(j => j.Status == JobStatus.Completed),
            Failed = jobs.Count(j => j.Status == JobStatus.Failed),
        };

        var uptime = DateTimeOffset.UtcNow - StartTime;

        return Ok(new ServerStatusResponse
        {
            Name = "OrbitMesh Server",
            Version = typeof(StatusController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Status = "Running",
            Uptime = FormatUptime(uptime),
            Agents = agentStats,
            Jobs = jobStats,
        });
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }

        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }
}

/// <summary>
/// Server status response.
/// </summary>
public sealed record ServerStatusResponse
{
    /// <summary>
    /// Server name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Server version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Current server status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Server uptime.
    /// </summary>
    public required string Uptime { get; init; }

    /// <summary>
    /// Agent statistics.
    /// </summary>
    public required AgentStats Agents { get; init; }

    /// <summary>
    /// Job statistics.
    /// </summary>
    public required JobStats Jobs { get; init; }
}

/// <summary>
/// Agent statistics.
/// </summary>
public sealed record AgentStats
{
    /// <summary>
    /// Total number of agents.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Number of ready agents.
    /// </summary>
    public int Ready { get; init; }

    /// <summary>
    /// Number of busy agents.
    /// </summary>
    public int Busy { get; init; }

    /// <summary>
    /// Number of disconnected agents.
    /// </summary>
    public int Disconnected { get; init; }
}

/// <summary>
/// Job statistics.
/// </summary>
public sealed record JobStats
{
    /// <summary>
    /// Number of pending jobs.
    /// </summary>
    public int Pending { get; init; }

    /// <summary>
    /// Number of running jobs.
    /// </summary>
    public int Running { get; init; }

    /// <summary>
    /// Number of completed jobs.
    /// </summary>
    public int Completed { get; init; }

    /// <summary>
    /// Number of failed jobs.
    /// </summary>
    public int Failed { get; init; }
}
