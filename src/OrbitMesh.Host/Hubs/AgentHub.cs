using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Hubs;

/// <summary>
/// SignalR hub for agent communication.
/// Implements the server-side of the OrbitMesh protocol.
/// </summary>
public class AgentHub : Hub<IAgentClient>, IServerHub
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IJobManager _jobManager;
    private readonly IProgressService _progressService;
    private readonly IStreamingService _streamingService;
    private readonly IApiTokenService _tokenService;
    private readonly IBootstrapTokenService _bootstrapTokenService;
    private readonly INodeEnrollmentService _enrollmentService;
    private readonly INodeCredentialService _credentialService;
    private readonly SecurityOptions _securityOptions;
    private readonly ILogger<AgentHub> _logger;

    /// <summary>
    /// SignalR group for all connected agents.
    /// </summary>
    public const string AllAgentsGroup = "all-agents";

    /// <summary>
    /// Required scope for agent connections.
    /// </summary>
    public const string AgentScope = "agent";

    /// <summary>
    /// SignalR group for nodes pending enrollment.
    /// </summary>
    public const string PendingEnrollmentGroup = "pending-enrollment";

    public AgentHub(
        IAgentRegistry agentRegistry,
        IJobManager jobManager,
        IProgressService progressService,
        IStreamingService streamingService,
        IApiTokenService tokenService,
        IBootstrapTokenService bootstrapTokenService,
        INodeEnrollmentService enrollmentService,
        INodeCredentialService credentialService,
        IOptions<SecurityOptions> securityOptions,
        ILogger<AgentHub> logger)
    {
        _agentRegistry = agentRegistry;
        _jobManager = jobManager;
        _progressService = progressService;
        _streamingService = streamingService;
        _tokenService = tokenService;
        _bootstrapTokenService = bootstrapTokenService;
        _enrollmentService = enrollmentService;
        _credentialService = credentialService;
        _securityOptions = securityOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Agent connection initiated. ConnectionId: {ConnectionId}",
            Context.ConnectionId);

        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            _logger.LogWarning("No HTTP context available. ConnectionId: {ConnectionId}", Context.ConnectionId);
            if (!_securityOptions.AllowAnonymous)
            {
                Context.Abort();
                return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup);
            await base.OnConnectedAsync();
            return;
        }

        // Extract authentication tokens from query string or header
        var accessToken = httpContext.Request.Query["access_token"].FirstOrDefault()
            ?? httpContext.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        var bootstrapToken = httpContext.Request.Query["bootstrap_token"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Bootstrap-Token"].FirstOrDefault();
        var nodeId = httpContext.Request.Query["node_id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Node-Id"].FirstOrDefault();
        var certificate = httpContext.Request.Query["certificate"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Node-Certificate"].FirstOrDefault();

        // Priority 1: Certificate-based authentication (for enrolled nodes)
        if (!string.IsNullOrEmpty(certificate) && !string.IsNullOrEmpty(nodeId))
        {
            var validation = await _credentialService.ValidateCertificateAsync(certificate);
            if (validation.IsValid && validation.NodeId == nodeId)
            {
                Context.Items["NodeId"] = nodeId;
                Context.Items["AuthType"] = "Certificate";
                Context.Items["IsEnrolled"] = true;

                _logger.LogInformation(
                    "Node authenticated with certificate. NodeId: {NodeId}, ConnectionId: {ConnectionId}",
                    nodeId,
                    Context.ConnectionId);

                await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup);
                await base.OnConnectedAsync();
                return;
            }

            _logger.LogWarning(
                "Invalid certificate provided. NodeId: {NodeId}, ConnectionId: {ConnectionId}",
                nodeId,
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Priority 2: Bootstrap token authentication (for enrollment)
        if (!string.IsNullOrEmpty(bootstrapToken))
        {
            var validation = await _bootstrapTokenService.ValidateAndConsumeAsync(bootstrapToken);
            if (validation is not null)
            {
                Context.Items["BootstrapTokenId"] = validation.TokenId;
                Context.Items["AuthType"] = "Bootstrap";
                Context.Items["IsEnrolled"] = false;
                Context.Items["AllowedCapabilities"] = validation.AllowedCapabilities;

                _logger.LogInformation(
                    "Node connected with bootstrap token for enrollment. TokenId: {TokenId}, ConnectionId: {ConnectionId}",
                    validation.TokenId,
                    Context.ConnectionId);

                // Add to pending enrollment group (limited access)
                await Groups.AddToGroupAsync(Context.ConnectionId, PendingEnrollmentGroup);
                await base.OnConnectedAsync();
                return;
            }

            _logger.LogWarning(
                "Invalid or consumed bootstrap token provided. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Priority 3: Legacy API token authentication
        if (!string.IsNullOrEmpty(accessToken))
        {
            var validToken = await _tokenService.ValidateTokenAsync(accessToken, AgentScope);
            if (validToken is not null)
            {
                Context.Items["TokenId"] = validToken.Id;
                Context.Items["TokenName"] = validToken.Name;
                Context.Items["AuthType"] = "ApiToken";
                await _tokenService.UpdateLastUsedAsync(validToken.Id);

                _logger.LogInformation(
                    "Agent authenticated with API token. TokenName: {TokenName}, ConnectionId: {ConnectionId}",
                    validToken.Name,
                    Context.ConnectionId);

                await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup);
                await base.OnConnectedAsync();
                return;
            }

            _logger.LogWarning(
                "Invalid or expired API token provided. ConnectionId: {ConnectionId}",
                Context.ConnectionId);

            // If certificate auth is required, reject
            if (_securityOptions.RequireCertificateAuth)
            {
                Context.Abort();
                return;
            }
        }

        // No valid authentication provided
        if (_securityOptions.AllowAnonymous)
        {
            Context.Items["AuthType"] = "Anonymous";
            _logger.LogWarning(
                "Agent connected without authentication. ConnectionId: {ConnectionId}. Anonymous access is enabled.",
                Context.ConnectionId);

            await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup);
            await base.OnConnectedAsync();
            return;
        }

        _logger.LogWarning(
            "Agent connection rejected: No valid authentication provided. ConnectionId: {ConnectionId}",
            Context.ConnectionId);
        Context.Abort();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = Context.Items["AgentId"] as string;

        if (agentId is not null)
        {
            await _agentRegistry.UpdateStatusAsync(agentId, AgentStatus.Disconnected);

            _logger.LogInformation(
                "Agent disconnected. AgentId: {AgentId}, ConnectionId: {ConnectionId}, Reason: {Reason}",
                agentId,
                Context.ConnectionId,
                exception?.Message ?? "Normal disconnect");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllAgentsGroup);
        await base.OnDisconnectedAsync(exception);
    }

    /// <inheritdoc />
    public async Task<AgentRegistrationResult> RegisterAsync(AgentInfo agentInfo)
    {
        try
        {
            // Store agent ID in connection context
            Context.Items["AgentId"] = agentInfo.Id;

            // Update agent with connection ID
            var registeredAgent = agentInfo with
            {
                ConnectionId = Context.ConnectionId,
                Status = AgentStatus.Ready,
                LastHeartbeat = DateTimeOffset.UtcNow
            };

            await _agentRegistry.RegisterAsync(registeredAgent);

            // Add to capability-based groups
            foreach (var capability in agentInfo.Capabilities)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"capability:{capability.Name}");
            }

            // Add to agent group if specified
            if (agentInfo.Group is not null)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"group:{agentInfo.Group}");
            }

            _logger.LogInformation(
                "Agent registered. AgentId: {AgentId}, Name: {Name}, Capabilities: {Capabilities}",
                agentInfo.Id,
                agentInfo.Name,
                string.Join(", ", agentInfo.Capabilities.Select(c => c.Name)));

            return AgentRegistrationResult.Succeeded(TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent. AgentId: {AgentId}", agentInfo.Id);
            return AgentRegistrationResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await _agentRegistry.UnregisterAsync(agentId, cancellationToken);

        _logger.LogInformation(
            "Agent unregistered. AgentId: {AgentId}, ConnectionId: {ConnectionId}",
            agentId,
            Context.ConnectionId);
    }

    /// <inheritdoc />
    public async Task HeartbeatAsync(string agentId)
    {
        await _agentRegistry.UpdateHeartbeatAsync(agentId, DateTimeOffset.UtcNow);

        _logger.LogDebug("Heartbeat received. AgentId: {AgentId}", agentId);
    }

    /// <inheritdoc />
    public async Task AcknowledgeJobAsync(
        string jobId,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var acknowledged = await _jobManager.AcknowledgeAsync(jobId, agentId, cancellationToken);

        if (acknowledged)
        {
            _logger.LogInformation(
                "Job acknowledged. JobId: {JobId}, AgentId: {AgentId}",
                jobId,
                agentId);
        }
        else
        {
            _logger.LogWarning(
                "Job acknowledgment failed. JobId: {JobId}, AgentId: {AgentId}",
                jobId,
                agentId);
        }
    }

    /// <inheritdoc />
    public async Task ReportResultAsync(JobResult result, CancellationToken cancellationToken = default)
    {
        bool updated;

        if (result.IsSuccess)
        {
            updated = await _jobManager.CompleteAsync(result.JobId, result, cancellationToken);
        }
        else
        {
            updated = await _jobManager.FailAsync(
                result.JobId,
                result.Error ?? "Unknown error",
                result.ErrorCode,
                cancellationToken);
        }

        if (updated)
        {
            _logger.LogInformation(
                "Job result processed. JobId: {JobId}, Status: {Status}, Duration: {Duration}ms",
                result.JobId,
                result.Status,
                result.Duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Job result could not be processed (job may already be completed). JobId: {JobId}",
                result.JobId);
        }

        // Clear progress tracking for completed job
        await _progressService.ClearProgressAsync(result.JobId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        // Update progress in the service (notifies subscribers)
        await _progressService.ReportProgressAsync(progress, cancellationToken);

        // Also update the job manager for persistence
        await _jobManager.UpdateProgressAsync(progress, cancellationToken);

        _logger.LogDebug(
            "Job progress received. JobId: {JobId}, Progress: {Progress}%, Message: {Message}",
            progress.JobId,
            progress.Percentage,
            progress.Message ?? "N/A");
    }

    /// <inheritdoc />
    public async Task ReportStateAsync(
        string agentId,
        IReadOnlyDictionary<string, string> reportedState,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Agent state reported. AgentId: {AgentId}, Properties: {Count}",
            agentId,
            reportedState.Count);

        // State reporting will be handled by the state manager
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReportStreamItemAsync(StreamItem item, CancellationToken cancellationToken = default)
    {
        // Publish to streaming service (notifies all subscribers)
        await _streamingService.PublishAsync(item, cancellationToken);

        _logger.LogDebug(
            "Stream item received. JobId: {JobId}, Sequence: {Sequence}, Size: {Size} bytes, IsEnd: {IsEnd}",
            item.JobId,
            item.SequenceNumber,
            item.Data.Length,
            item.IsEndOfStream);

        // If end of stream, mark stream as complete
        if (item.IsEndOfStream)
        {
            await _streamingService.CompleteStreamAsync(item.JobId, cancellationToken);
        }
    }

    /// <summary>
    /// SignalR streaming endpoint for clients to subscribe to job streams.
    /// Clients can call this method to receive streaming data as an IAsyncEnumerable.
    /// </summary>
    /// <param name="jobId">The job ID to subscribe to.</param>
    /// <param name="fromSequence">Optional sequence number to start from (for replay).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of stream items.</returns>
    public IAsyncEnumerable<StreamItem> SubscribeToStream(
        string jobId,
        long fromSequence = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Client subscribing to stream. JobId: {JobId}, FromSequence: {FromSequence}, ConnectionId: {ConnectionId}",
            jobId,
            fromSequence,
            Context.ConnectionId);

        return _streamingService.SubscribeAsync(jobId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NodeEnrollmentResult> RequestEnrollmentAsync(
        NodeEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify the connection is authenticated with a bootstrap token
        var authType = Context.Items["AuthType"] as string;
        if (authType != "Bootstrap")
        {
            _logger.LogWarning(
                "Enrollment request rejected: Not authenticated with bootstrap token. ConnectionId: {ConnectionId}, AuthType: {AuthType}",
                Context.ConnectionId,
                authType ?? "None");

            return NodeEnrollmentResult.Failed("Enrollment requires a valid bootstrap token");
        }

        try
        {
            _logger.LogInformation(
                "Processing enrollment request. NodeId: {NodeId}, NodeName: {NodeName}, ConnectionId: {ConnectionId}",
                request.NodeId,
                request.NodeName,
                Context.ConnectionId);

            // Validate signature
            var isSignatureValid = await _credentialService.VerifySignatureAsync(
                request.NodeId,
                request.PublicKey,
                request.Signature,
                cancellationToken);

            if (!isSignatureValid)
            {
                _logger.LogWarning(
                    "Enrollment request rejected: Invalid signature. NodeId: {NodeId}",
                    request.NodeId);
                return NodeEnrollmentResult.Failed("Invalid signature");
            }

            // Check if node is blocked
            var isBlocked = await _enrollmentService.IsNodeBlockedAsync(request.NodeId, cancellationToken);
            if (isBlocked)
            {
                _logger.LogWarning(
                    "Enrollment request rejected: Node is blocked. NodeId: {NodeId}",
                    request.NodeId);
                return NodeEnrollmentResult.Blocked();
            }

            // Get bootstrap token ID from context
            var bootstrapTokenId = Context.Items["BootstrapTokenId"] as string ?? string.Empty;

            // Create enrollment request
            var enrollment = new EnrollmentRequest
            {
                NodeId = request.NodeId,
                NodeName = request.NodeName,
                PublicKey = request.PublicKey,
                RequestedCapabilities = request.RequestedCapabilities,
                Metadata = request.Metadata,
                Signature = request.Signature
            };

            var enrollmentResult = await _enrollmentService.RequestEnrollmentAsync(
                enrollment, bootstrapTokenId, cancellationToken);

            // Store enrollment ID in connection context
            Context.Items["EnrollmentId"] = enrollmentResult.EnrollmentId;

            if (enrollmentResult.Status == EnrollmentStatus.Approved)
            {
                // Auto-approved - issue certificate immediately
                var certificate = await _credentialService.IssueCertificateAsync(
                    request.NodeId,
                    request.PublicKey,
                    enrollmentResult.ApprovedCapabilities ?? [],
                    cancellationToken);

                var serverKeyInfo = await _credentialService.GetServerKeyInfoAsync(cancellationToken);

                _logger.LogInformation(
                    "Enrollment auto-approved and certificate issued. NodeId: {NodeId}, EnrollmentId: {EnrollmentId}",
                    request.NodeId,
                    enrollmentResult.EnrollmentId);

                // Move from pending enrollment group to all agents group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, PendingEnrollmentGroup, cancellationToken);
                await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup, cancellationToken);

                Context.Items["IsEnrolled"] = true;
                Context.Items["NodeId"] = request.NodeId;

                return NodeEnrollmentResult.Approved(certificate.ToBase64(), serverKeyInfo.PublicKey);
            }

            _logger.LogInformation(
                "Enrollment request pending approval. NodeId: {NodeId}, EnrollmentId: {EnrollmentId}",
                request.NodeId,
                enrollmentResult.EnrollmentId);

            return NodeEnrollmentResult.Pending(enrollmentResult.EnrollmentId ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing enrollment request. NodeId: {NodeId}", request.NodeId);
            return NodeEnrollmentResult.Failed($"Enrollment failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<NodeEnrollmentResult> CheckEnrollmentStatusAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Checking enrollment status. EnrollmentId: {EnrollmentId}, ConnectionId: {ConnectionId}",
                enrollmentId,
                Context.ConnectionId);

            var status = await _enrollmentService.GetEnrollmentStatusAsync(enrollmentId, cancellationToken);

            switch (status.Status)
            {
                case EnrollmentStatus.Approved:
                    // Enrollment approved - issue certificate
                    var enrollment = await _enrollmentService.GetEnrollmentAsync(enrollmentId, cancellationToken);
                    if (enrollment is null)
                    {
                        return NodeEnrollmentResult.Failed("Enrollment not found");
                    }

                    var certificate = await _credentialService.IssueCertificateAsync(
                        enrollment.NodeId,
                        enrollment.PublicKey,
                        status.ApprovedCapabilities ?? [],
                        cancellationToken);

                    var serverKeyInfo = await _credentialService.GetServerKeyInfoAsync(cancellationToken);

                    _logger.LogInformation(
                        "Enrollment approved, certificate issued. NodeId: {NodeId}, EnrollmentId: {EnrollmentId}",
                        enrollment.NodeId,
                        enrollmentId);

                    // Move from pending enrollment group to all agents group
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, PendingEnrollmentGroup, cancellationToken);
                    await Groups.AddToGroupAsync(Context.ConnectionId, AllAgentsGroup, cancellationToken);

                    Context.Items["IsEnrolled"] = true;
                    Context.Items["NodeId"] = enrollment.NodeId;

                    return NodeEnrollmentResult.Approved(certificate.ToBase64(), serverKeyInfo.PublicKey);

                case EnrollmentStatus.Pending:
                    return NodeEnrollmentResult.Pending(enrollmentId);

                case EnrollmentStatus.Rejected:
                    _logger.LogInformation(
                        "Enrollment was rejected. EnrollmentId: {EnrollmentId}, Reason: {Reason}",
                        enrollmentId,
                        status.RejectionReason);
                    return NodeEnrollmentResult.Rejected(status.RejectionReason);

                case EnrollmentStatus.Expired:
                    return NodeEnrollmentResult.Failed("Enrollment expired");

                default:
                    return NodeEnrollmentResult.Failed($"Unknown enrollment status: {status.Status}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking enrollment status. EnrollmentId: {EnrollmentId}", enrollmentId);
            return NodeEnrollmentResult.Failed($"Failed to check enrollment status: {ex.Message}");
        }
    }
}
