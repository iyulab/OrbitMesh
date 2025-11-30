using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Hubs;

namespace OrbitMesh.Server.Services;

/// <summary>
/// Service for invoking methods on agents and receiving results using SignalR Client Results.
/// </summary>
public class ClientResultsService : IClientResultsService
{
    private readonly IHubContext<AgentHub, IAgentClient> _hubContext;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<ClientResultsService> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

    public ClientResultsService(
        IHubContext<AgentHub, IAgentClient> hubContext,
        IAgentRegistry agentRegistry,
        ILogger<ClientResultsService> logger)
    {
        _hubContext = hubContext;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentHealthResponse> GetAgentHealthAsync(
        string agentId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);
        if (agent is null || agent.ConnectionId is null)
        {
            _logger.LogWarning("Agent not found or not connected. AgentId: {AgentId}", agentId);
            return new AgentHealthResponse
            {
                Status = AgentHealthStatus.Unhealthy,
                Message = "Agent not found or not connected"
            };
        }

        using var cts = CreateTimeoutCancellation(timeout, cancellationToken);

        try
        {
            _logger.LogDebug("Requesting health from agent. AgentId: {AgentId}", agentId);

            var response = await _hubContext.Clients
                .Client(agent.ConnectionId)
                .GetHealthAsync(cts.Token);

            _logger.LogDebug(
                "Health response received. AgentId: {AgentId}, Status: {Status}",
                agentId,
                response.Status);

            return response;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("Health check timed out. AgentId: {AgentId}", agentId);
            return new AgentHealthResponse
            {
                Status = AgentHealthStatus.Unhealthy,
                Message = "Health check timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health from agent. AgentId: {AgentId}", agentId);
            return new AgentHealthResponse
            {
                Status = AgentHealthStatus.Unhealthy,
                Message = $"Health check failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<AgentResourceUsage> GetAgentResourceUsageAsync(
        string agentId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);
        if (agent is null || agent.ConnectionId is null)
        {
            _logger.LogWarning("Agent not found or not connected. AgentId: {AgentId}", agentId);
            return new AgentResourceUsage
            {
                CpuPercentage = -1,
                MemoryBytes = -1
            };
        }

        using var cts = CreateTimeoutCancellation(timeout, cancellationToken);

        try
        {
            _logger.LogDebug("Requesting resource usage from agent. AgentId: {AgentId}", agentId);

            var response = await _hubContext.Clients
                .Client(agent.ConnectionId)
                .GetResourceUsageAsync(cts.Token);

            _logger.LogDebug(
                "Resource usage received. AgentId: {AgentId}, CPU: {Cpu}%, Memory: {Memory}MB",
                agentId,
                response.CpuPercentage,
                response.MemoryBytes / 1024.0 / 1024.0);

            return response;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("Resource usage request timed out. AgentId: {AgentId}", agentId);
            return new AgentResourceUsage
            {
                CpuPercentage = -1,
                MemoryBytes = -1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource usage from agent. AgentId: {AgentId}", agentId);
            return new AgentResourceUsage
            {
                CpuPercentage = -1,
                MemoryBytes = -1
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateJobWithAgentAsync(
        string agentId,
        JobRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);
        if (agent is null || agent.ConnectionId is null)
        {
            _logger.LogWarning("Agent not found or not connected. AgentId: {AgentId}", agentId);
            return false;
        }

        using var cts = CreateTimeoutCancellation(timeout, cancellationToken);

        try
        {
            _logger.LogDebug(
                "Validating job with agent. AgentId: {AgentId}, JobId: {JobId}, Command: {Command}",
                agentId,
                request.Id,
                request.Command);

            var isValid = await _hubContext.Clients
                .Client(agent.ConnectionId)
                .ValidateJobAsync(request, cts.Token);

            _logger.LogDebug(
                "Job validation result. AgentId: {AgentId}, JobId: {JobId}, IsValid: {IsValid}",
                agentId,
                request.Id,
                isValid);

            return isValid;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Job validation timed out. AgentId: {AgentId}, JobId: {JobId}",
                agentId,
                request.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to validate job with agent. AgentId: {AgentId}, JobId: {JobId}",
                agentId,
                request.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AgentCallbackResponse> SendCallbackAsync(
        string agentId,
        AgentCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentRegistry.GetAsync(agentId, cancellationToken);
        if (agent is null || agent.ConnectionId is null)
        {
            _logger.LogWarning("Agent not found or not connected. AgentId: {AgentId}", agentId);
            return AgentCallbackResponse.Failed(
                request.CallbackId,
                "Agent not found or not connected",
                "AGENT_NOT_FOUND");
        }

        using var cts = CreateTimeoutCancellation(request.Timeout, cancellationToken);

        try
        {
            _logger.LogDebug(
                "Sending callback to agent. AgentId: {AgentId}, CallbackId: {CallbackId}, Type: {Type}",
                agentId,
                request.CallbackId,
                request.Type);

            var response = await _hubContext.Clients
                .Client(agent.ConnectionId)
                .ProcessCallbackAsync(request, cts.Token);

            _logger.LogDebug(
                "Callback response received. AgentId: {AgentId}, CallbackId: {CallbackId}, Success: {Success}",
                agentId,
                request.CallbackId,
                response.Success);

            return response;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Callback timed out. AgentId: {AgentId}, CallbackId: {CallbackId}",
                agentId,
                request.CallbackId);
            return AgentCallbackResponse.Failed(
                request.CallbackId,
                "Callback timed out",
                "TIMEOUT");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send callback to agent. AgentId: {AgentId}, CallbackId: {CallbackId}",
                agentId,
                request.CallbackId);
            return AgentCallbackResponse.Failed(
                request.CallbackId,
                ex.Message,
                "CALLBACK_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, AgentHealthResponse>> GetAllAgentHealthAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentRegistry.GetAllAsync(cancellationToken);
        var readyAgents = agents.Where(a => a.Status == AgentStatus.Ready && a.ConnectionId is not null).ToList();

        var results = new Dictionary<string, AgentHealthResponse>();
        var tasks = new List<Task<(string AgentId, AgentHealthResponse Response)>>();

        foreach (var agent in readyAgents)
        {
            tasks.Add(GetHealthWithIdAsync(agent.Id, timeout, cancellationToken));
        }

        var responses = await Task.WhenAll(tasks);

        foreach (var (agentId, response) in responses)
        {
            results[agentId] = response;
        }

        _logger.LogInformation(
            "Health check completed for {Count} agents. Healthy: {Healthy}, Degraded: {Degraded}, Unhealthy: {Unhealthy}",
            results.Count,
            results.Values.Count(r => r.Status == AgentHealthStatus.Healthy),
            results.Values.Count(r => r.Status == AgentHealthStatus.Degraded),
            results.Values.Count(r => r.Status == AgentHealthStatus.Unhealthy));

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, AgentResourceUsage>> GetAllAgentResourceUsageAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentRegistry.GetAllAsync(cancellationToken);
        var readyAgents = agents.Where(a => a.Status == AgentStatus.Ready && a.ConnectionId is not null).ToList();

        var results = new Dictionary<string, AgentResourceUsage>();
        var tasks = new List<Task<(string AgentId, AgentResourceUsage Usage)>>();

        foreach (var agent in readyAgents)
        {
            tasks.Add(GetResourceUsageWithIdAsync(agent.Id, timeout, cancellationToken));
        }

        var responses = await Task.WhenAll(tasks);

        foreach (var (agentId, usage) in responses)
        {
            results[agentId] = usage;
        }

        _logger.LogInformation(
            "Resource usage collected from {Count} agents. Total CPU: {TotalCpu}%, Total Memory: {TotalMemory}MB",
            results.Count,
            results.Values.Where(r => r.CpuPercentage >= 0).Sum(r => r.CpuPercentage),
            results.Values.Where(r => r.MemoryBytes >= 0).Sum(r => r.MemoryBytes) / 1024.0 / 1024.0);

        return results;
    }

    private async Task<(string AgentId, AgentHealthResponse Response)> GetHealthWithIdAsync(
        string agentId,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var response = await GetAgentHealthAsync(agentId, timeout, cancellationToken);
        return (agentId, response);
    }

    private async Task<(string AgentId, AgentResourceUsage Usage)> GetResourceUsageWithIdAsync(
        string agentId,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var usage = await GetAgentResourceUsageAsync(agentId, timeout, cancellationToken);
        return (agentId, usage);
    }

    private CancellationTokenSource CreateTimeoutCancellation(
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);
        return cts;
    }
}
