using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Server.Controllers;

/// <summary>
/// REST API controller for workflow management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowRegistry _registry;
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly IWorkflowEngine _engine;

    /// <summary>
    /// Creates a new workflows controller.
    /// </summary>
    public WorkflowsController(
        IWorkflowRegistry registry,
        IWorkflowInstanceStore instanceStore,
        IWorkflowEngine engine)
    {
        _registry = registry;
        _instanceStore = instanceStore;
        _engine = engine;
    }

    /// <summary>
    /// Lists all workflow definitions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of workflow definitions.</returns>
    /// <response code="200">Workflows retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowDefinition>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWorkflows(CancellationToken cancellationToken = default)
    {
        var workflows = await _registry.ListAsync(cancellationToken);
        return Ok(workflows);
    }

    /// <summary>
    /// Gets a workflow definition by ID.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workflow definition.</returns>
    /// <response code="200">Workflow found.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpGet("{workflowId}")]
    [ProducesResponseType(typeof(WorkflowDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkflow(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _registry.GetAsync(workflowId, null, cancellationToken);

        if (workflow is null)
        {
            return NotFound();
        }

        return Ok(workflow);
    }

    /// <summary>
    /// Lists workflow instances.
    /// </summary>
    /// <param name="workflowId">Optional workflow ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of workflow instances.</returns>
    /// <response code="200">Instances retrieved successfully.</response>
    [HttpGet("instances")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowInstance>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInstances(
        [FromQuery] string? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new WorkflowInstanceQuery { WorkflowId = workflowId };
        var instances = await _instanceStore.QueryAsync(query, cancellationToken);

        return Ok(instances);
    }

    /// <summary>
    /// Starts a new workflow instance.
    /// </summary>
    /// <param name="workflowId">The workflow ID to start.</param>
    /// <param name="request">The start request with optional input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created workflow instance.</returns>
    /// <response code="201">Workflow started successfully.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpPost("{workflowId}/start")]
    [ProducesResponseType(typeof(WorkflowInstance), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartWorkflow(
        string workflowId,
        [FromBody] StartWorkflowRequest? request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _registry.GetAsync(workflowId, null, cancellationToken);

        if (workflow is null)
        {
            return NotFound(new { Error = $"Workflow '{workflowId}' not found" });
        }

        var instance = await _engine.StartAsync(
            workflow,
            request?.Input,
            cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(ListInstances),
            new { workflowId },
            instance);
    }

    /// <summary>
    /// Registers a new workflow definition.
    /// </summary>
    /// <param name="definition">The workflow definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered workflow.</returns>
    /// <response code="201">Workflow registered successfully.</response>
    /// <response code="400">Invalid workflow definition.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WorkflowDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterWorkflow(
        [FromBody] WorkflowDefinition? definition,
        CancellationToken cancellationToken = default)
    {
        if (definition is null)
        {
            return BadRequest(new { Error = "Workflow definition is required" });
        }

        await _registry.RegisterAsync(definition, cancellationToken);

        return CreatedAtAction(
            nameof(GetWorkflow),
            new { workflowId = definition.Id },
            definition);
    }
}

/// <summary>
/// Request to start a workflow.
/// </summary>
public sealed record StartWorkflowRequest
{
    /// <summary>
    /// Optional input variables for the workflow.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Input { get; init; }
}
