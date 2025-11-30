using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Agent;

/// <summary>
/// Default implementation of IMeshAgent.
/// Manages the SignalR connection to the OrbitMesh server.
/// </summary>
public sealed class MeshAgent : IMeshAgent
{
    private readonly HubConnection _connection;
    private readonly AgentInfo _agentInfo;
    private readonly ILogger<MeshAgent> _logger;
    private readonly IHandlerRegistry _handlerRegistry;
    private readonly JobCancellationManager _cancellationManager = new();
    private readonly TaskCompletionSource _shutdownTcs = new();
    private readonly CancellationTokenSource _heartbeatCts = new();
    private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private bool _disposed;

    /// <inheritdoc />
    public string Id => _agentInfo.Id;

    /// <inheritdoc />
    public string Name => _agentInfo.Name;

    /// <inheritdoc />
    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    internal MeshAgent(
        HubConnection connection,
        AgentInfo agentInfo,
        IHandlerRegistry handlerRegistry,
        ILogger<MeshAgent> logger)
    {
        _connection = connection;
        _agentInfo = agentInfo;
        _handlerRegistry = handlerRegistry;
        _logger = logger;

        ConfigureConnection();
    }

    private void ConfigureConnection()
    {
        _connection.Closed += OnConnectionClosed;
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += OnReconnected;

        // Register client methods that server can invoke
        _connection.On<JobRequest>(nameof(IAgentClient.ExecuteJobAsync), HandleExecuteJobAsync);
        _connection.On<string>(nameof(IAgentClient.CancelJobAsync), HandleCancelJobAsync);
        _connection.On<IReadOnlyDictionary<string, string>>(
            nameof(IAgentClient.UpdateDesiredStateAsync),
            HandleUpdateDesiredStateAsync);
        _connection.On(nameof(IAgentClient.PingAsync), HandlePingAsync);
        _connection.On<string?>(nameof(IAgentClient.ShutdownAsync), HandleShutdownAsync);
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation(
            "Connecting to OrbitMesh server. AgentId: {AgentId}, Name: {Name}",
            Id, Name);

        await _connection.StartAsync(cancellationToken);

        // Register with the server
        var result = await _connection.InvokeAsync<AgentRegistrationResult>(
            nameof(IServerHub.RegisterAsync),
            _agentInfo,
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to register agent: {result.Error}");
        }

        _heartbeatInterval = result.HeartbeatInterval;

        _logger.LogInformation(
            "Agent registered successfully. AgentId: {AgentId}, HeartbeatInterval: {Interval}s",
            Id, _heartbeatInterval.TotalSeconds);

        // Start heartbeat task
        _ = StartHeartbeatAsync(_heartbeatCts.Token);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disconnecting from OrbitMesh server. AgentId: {AgentId}", Id);

        await _heartbeatCts.CancelAsync();

        try
        {
            await _connection.InvokeAsync(
                nameof(IServerHub.UnregisterAsync),
                Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during unregistration. AgentId: {AgentId}", Id);
        }

        await _connection.StopAsync(cancellationToken);

        _shutdownTcs.TrySetResult();
    }

    /// <inheritdoc />
    public Task WaitForShutdownAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.CanBeCanceled
            ? Task.WhenAny(_shutdownTcs.Task, Task.Delay(Timeout.Infinite, cancellationToken))
            : _shutdownTcs.Task;
    }

    private async Task StartHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(_heartbeatInterval, cancellationToken);

                if (IsConnected)
                {
                    await _connection.InvokeAsync(
                        nameof(IServerHub.HeartbeatAsync),
                        Id,
                        cancellationToken);

                    _logger.LogDebug("Heartbeat sent. AgentId: {AgentId}", Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed. AgentId: {AgentId}", Id);
            }
        }
    }

    private async Task HandleExecuteJobAsync(JobRequest request)
    {
        _logger.LogInformation(
            "Executing job. JobId: {JobId}, Command: {Command}",
            request.Id, request.Command);

        var startTime = DateTimeOffset.UtcNow;
        JobResult result;

        // Register job for cancellation tracking
        var cancellationToken = _cancellationManager.RegisterJob(request.Id);

        try
        {
            // Acknowledge job receipt
            await _connection.InvokeAsync(
                nameof(IServerHub.AcknowledgeJobAsync),
                request.Id,
                Id);

            var context = CommandContext.FromRequest(request, Id, cancellationToken);
            var handler = _handlerRegistry.GetHandler(request.Command);

            if (handler is null)
            {
                result = JobResult.Failure(
                    request.Id,
                    Id,
                    $"No handler registered for command: {request.Command}",
                    "HANDLER_NOT_FOUND");
            }
            else
            {
                var data = await handler.HandleAsync(context, cancellationToken);
                result = JobResult.Success(request.Id, Id, data);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job cancelled. JobId: {JobId}", request.Id);
            result = JobResult.Failure(request.Id, Id, "Job was cancelled", "JOB_CANCELLED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job execution failed. JobId: {JobId}", request.Id);
            result = JobResult.Failure(request.Id, Id, ex.Message, "EXECUTION_FAILED");
        }
        finally
        {
            // Remove job from cancellation tracking
            _cancellationManager.CompleteJob(request.Id);
        }

        await _connection.InvokeAsync(nameof(IServerHub.ReportResultAsync), result);
    }

    private Task HandleCancelJobAsync(string jobId)
    {
        _logger.LogInformation("Job cancellation requested. JobId: {JobId}", jobId);

        var cancelled = _cancellationManager.CancelJob(jobId);

        if (cancelled)
        {
            _logger.LogInformation("Job cancellation signal sent. JobId: {JobId}", jobId);
        }
        else
        {
            _logger.LogWarning(
                "Job not found for cancellation (may have already completed). JobId: {JobId}",
                jobId);
        }

        return Task.CompletedTask;
    }

    private Task HandleUpdateDesiredStateAsync(IReadOnlyDictionary<string, string> desiredState)
    {
        _logger.LogDebug("Desired state update received. Keys: {Keys}", string.Join(", ", desiredState.Keys));
        // State updates will be implemented with state synchronization
        return Task.CompletedTask;
    }

    private Task HandlePingAsync()
    {
        _logger.LogDebug("Ping received from server. AgentId: {AgentId}", Id);
        return Task.CompletedTask;
    }

    private async Task HandleShutdownAsync(string? reason)
    {
        _logger.LogInformation("Shutdown requested by server. Reason: {Reason}", reason ?? "None");
        await DisconnectAsync();
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "Connection closed unexpectedly. AgentId: {AgentId}", Id);
        }
        else
        {
            _logger.LogInformation("Connection closed. AgentId: {AgentId}", Id);
        }

        _shutdownTcs.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(
            exception,
            "Connection lost, attempting to reconnect. AgentId: {AgentId}",
            Id);
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation(
            "Reconnected to server. AgentId: {AgentId}, ConnectionId: {ConnectionId}",
            Id, connectionId);

        // Re-register after reconnection
        try
        {
            var result = await _connection.InvokeAsync<AgentRegistrationResult>(
                nameof(IServerHub.RegisterAsync),
                _agentInfo);

            if (!result.Success)
            {
                _logger.LogError("Re-registration failed: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-register after reconnection");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancel all running jobs
        _cancellationManager.CancelAllJobs();
        _cancellationManager.Dispose();

        await _heartbeatCts.CancelAsync();
        _heartbeatCts.Dispose();

        await _connection.DisposeAsync();

        _shutdownTcs.TrySetResult();
    }
}
