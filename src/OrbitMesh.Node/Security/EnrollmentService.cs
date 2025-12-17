using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Node.Security;

/// <summary>
/// Handles the node enrollment workflow with the server.
/// </summary>
public sealed class EnrollmentService
{
    private readonly HubConnection _connection;
    private readonly NodeCredentialManager _credentialManager;
    private readonly ILogger<EnrollmentService> _logger;

    /// <summary>
    /// Enrollment poll interval for pending enrollments.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time to wait for enrollment approval.
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Creates a new EnrollmentService.
    /// </summary>
    public EnrollmentService(
        HubConnection connection,
        NodeCredentialManager credentialManager,
        ILogger<EnrollmentService> logger)
    {
        _connection = connection;
        _credentialManager = credentialManager;
        _logger = logger;
    }

    /// <summary>
    /// Performs enrollment or reconnects with existing certificate.
    /// Returns true if successfully enrolled/authenticated.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="nodeName">Node display name.</param>
    /// <param name="metadata">Node metadata.</param>
    /// <param name="requestedCapabilities">Capabilities to request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enrollment is complete, false if pending approval.</returns>
    public async Task<EnrollmentOutcome> EnrollAsync(
        string nodeId,
        string nodeName,
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyList<string>? requestedCapabilities,
        CancellationToken cancellationToken = default)
    {
        // Check if already enrolled
        if (_credentialManager.IsEnrolled)
        {
            _logger.LogInformation("Node already enrolled, using existing certificate");
            return EnrollmentOutcome.AlreadyEnrolled;
        }

        // Check if there's a pending enrollment
        if (_credentialManager.Credentials?.PendingEnrollmentId is not null)
        {
            return await CheckPendingEnrollmentAsync(
                _credentialManager.Credentials.PendingEnrollmentId,
                cancellationToken);
        }

        // Start new enrollment
        return await StartEnrollmentAsync(
            nodeId,
            nodeName,
            metadata ?? new Dictionary<string, string>(),
            requestedCapabilities ?? [],
            cancellationToken);
    }

    /// <summary>
    /// Waits for a pending enrollment to be approved.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enrollment outcome.</returns>
    public async Task<EnrollmentOutcome> WaitForApprovalAsync(CancellationToken cancellationToken = default)
    {
        var enrollmentId = _credentialManager.Credentials?.PendingEnrollmentId;
        if (enrollmentId is null)
        {
            return EnrollmentOutcome.Failed("No pending enrollment");
        }

        using var timeoutCts = new CancellationTokenSource(MaxWaitTime);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            var outcome = await CheckPendingEnrollmentAsync(enrollmentId, linkedCts.Token);

            if (outcome.Status != EnrollmentStatus.Pending)
            {
                return outcome;
            }

            _logger.LogDebug("Enrollment still pending. Waiting {Interval}s before next check",
                PollInterval.TotalSeconds);

            await Task.Delay(PollInterval, linkedCts.Token);
        }

        return EnrollmentOutcome.Failed("Enrollment approval timeout");
    }

    private async Task<EnrollmentOutcome> StartEnrollmentAsync(
        string nodeId,
        string nodeName,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<string> requestedCapabilities,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting enrollment request. NodeId: {NodeId}, NodeName: {NodeName}",
            nodeId, nodeName);

        var publicKey = _credentialManager.PublicKey
            ?? throw new InvalidOperationException("Credentials not initialized");

        var signature = _credentialManager.CreateEnrollmentSignature(nodeId);

        var request = new NodeEnrollmentRequest
        {
            NodeId = nodeId,
            NodeName = nodeName,
            PublicKey = publicKey,
            RequestedCapabilities = requestedCapabilities,
            Metadata = metadata,
            Signature = signature
        };

        try
        {
            var result = await _connection.InvokeCoreAsync<NodeEnrollmentResult>(
                nameof(IServerHub.RequestEnrollmentAsync),
                [request],
                cancellationToken);

            return await ProcessEnrollmentResultAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrollment request failed");
            return EnrollmentOutcome.Failed($"Enrollment request failed: {ex.Message}");
        }
    }

    private async Task<EnrollmentOutcome> CheckPendingEnrollmentAsync(
        string enrollmentId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking enrollment status. EnrollmentId: {EnrollmentId}", enrollmentId);

        try
        {
            var result = await _connection.InvokeCoreAsync<NodeEnrollmentResult>(
                nameof(IServerHub.CheckEnrollmentStatusAsync),
                [enrollmentId],
                cancellationToken);

            return await ProcessEnrollmentResultAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check enrollment status");
            return EnrollmentOutcome.Failed($"Failed to check enrollment status: {ex.Message}");
        }
    }

    private async Task<EnrollmentOutcome> ProcessEnrollmentResultAsync(
        NodeEnrollmentResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case NodeEnrollmentStatus.Approved:
                if (result.Certificate is null || result.ServerPublicKey is null)
                {
                    return EnrollmentOutcome.Failed("Approved but no certificate received");
                }

                await _credentialManager.StoreCertificateAsync(
                    result.Certificate,
                    result.ServerPublicKey,
                    cancellationToken);

                _logger.LogInformation("Enrollment approved! Certificate received.");
                return EnrollmentOutcome.Approved();

            case NodeEnrollmentStatus.Pending:
                if (result.EnrollmentId is not null)
                {
                    await _credentialManager.StoreEnrollmentIdAsync(
                        result.EnrollmentId,
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Enrollment pending approval. EnrollmentId: {EnrollmentId}",
                    result.EnrollmentId);

                return EnrollmentOutcome.Pending(
                    result.EnrollmentId ?? string.Empty,
                    result.PollInterval);

            case NodeEnrollmentStatus.Rejected:
                _logger.LogWarning("Enrollment rejected. Reason: {Reason}", result.Error);
                return EnrollmentOutcome.Rejected(result.Error);

            case NodeEnrollmentStatus.Blocked:
                _logger.LogError("Node is blocked from enrollment");
                return EnrollmentOutcome.Blocked();

            case NodeEnrollmentStatus.Expired:
                _logger.LogWarning("Enrollment expired");
                return EnrollmentOutcome.Expired();

            default:
                return EnrollmentOutcome.Failed(result.Error ?? "Unknown enrollment status");
        }
    }
}

/// <summary>
/// Enrollment status.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>
    /// Already enrolled with valid certificate.
    /// </summary>
    AlreadyEnrolled,

    /// <summary>
    /// Enrollment approved, certificate received.
    /// </summary>
    Approved,

    /// <summary>
    /// Enrollment pending admin approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Enrollment was rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Node is blocked from enrollment.
    /// </summary>
    Blocked,

    /// <summary>
    /// Enrollment expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Enrollment failed (error).
    /// </summary>
    Failed
}

/// <summary>
/// Result of an enrollment operation.
/// </summary>
public sealed record EnrollmentOutcome
{
    /// <summary>
    /// Enrollment status.
    /// </summary>
    public required EnrollmentStatus Status { get; init; }

    /// <summary>
    /// Enrollment ID (if pending).
    /// </summary>
    public string? EnrollmentId { get; init; }

    /// <summary>
    /// Suggested poll interval (if pending).
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Error message (if failed/rejected).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether enrollment is complete and node can operate normally.
    /// </summary>
    public bool IsComplete => Status is EnrollmentStatus.AlreadyEnrolled or EnrollmentStatus.Approved;

    /// <summary>
    /// Creates an already enrolled outcome.
    /// </summary>
    public static EnrollmentOutcome AlreadyEnrolled => new() { Status = EnrollmentStatus.AlreadyEnrolled };

    /// <summary>
    /// Creates an approved outcome.
    /// </summary>
    public static EnrollmentOutcome Approved() => new() { Status = EnrollmentStatus.Approved };

    /// <summary>
    /// Creates a pending outcome.
    /// </summary>
    public static EnrollmentOutcome Pending(string enrollmentId, TimeSpan pollInterval) => new()
    {
        Status = EnrollmentStatus.Pending,
        EnrollmentId = enrollmentId,
        PollInterval = pollInterval
    };

    /// <summary>
    /// Creates a rejected outcome.
    /// </summary>
    public static EnrollmentOutcome Rejected(string? reason) => new()
    {
        Status = EnrollmentStatus.Rejected,
        Error = reason ?? "Enrollment rejected"
    };

    /// <summary>
    /// Creates a blocked outcome.
    /// </summary>
    public static EnrollmentOutcome Blocked() => new()
    {
        Status = EnrollmentStatus.Blocked,
        Error = "Node is blocked from enrollment"
    };

    /// <summary>
    /// Creates an expired outcome.
    /// </summary>
    public static EnrollmentOutcome Expired() => new()
    {
        Status = EnrollmentStatus.Expired,
        Error = "Enrollment expired"
    };

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    public static EnrollmentOutcome Failed(string error) => new()
    {
        Status = EnrollmentStatus.Failed,
        Error = error
    };
}
