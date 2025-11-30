using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Controllers;

/// <summary>
/// REST API controller for agent management operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly IAgentRegistry _registry;

    /// <summary>
    /// Creates a new agents controller.
    /// </summary>
    public AgentsController(IAgentRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Lists all registered agents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of agents.</returns>
    /// <response code="200">Agents retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AgentInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAgents(CancellationToken cancellationToken = default)
    {
        var agents = await _registry.GetAllAsync(cancellationToken);
        return Ok(agents);
    }

    /// <summary>
    /// Gets an agent by its ID.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent details.</returns>
    /// <response code="200">Agent found.</response>
    /// <response code="400">Invalid agent ID.</response>
    /// <response code="404">Agent not found.</response>
    [HttpGet("{agentId}")]
    [ProducesResponseType(typeof(AgentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgent(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return BadRequest(new { Error = "Agent ID is required" });
        }

        var agent = await _registry.GetAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return NotFound();
        }

        return Ok(agent);
    }
}
