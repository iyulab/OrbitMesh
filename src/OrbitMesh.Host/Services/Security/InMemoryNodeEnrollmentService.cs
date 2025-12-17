using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// In-memory implementation of node enrollment service.
/// For production, replace with a persistent store.
/// </summary>
public sealed class InMemoryNodeEnrollmentService : INodeEnrollmentService
{
    private readonly ConcurrentDictionary<string, PendingEnrollment> _enrollments = new();
    private readonly ConcurrentDictionary<string, BlockedNode> _blockedNodes = new();
    private readonly INodeCredentialService _credentialService;
    private readonly ILogger<InMemoryNodeEnrollmentService> _logger;
    private readonly SecurityOptions _options;

    public InMemoryNodeEnrollmentService(
        INodeCredentialService credentialService,
        IOptions<SecurityOptions> options,
        ILogger<InMemoryNodeEnrollmentService> logger)
    {
        _credentialService = credentialService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> RequestEnrollmentAsync(
        EnrollmentRequest request,
        string bootstrapTokenId,
        CancellationToken cancellationToken = default)
    {
        // Check if node is blocked
        if (_blockedNodes.ContainsKey(request.NodeId))
        {
            _logger.LogWarning(
                "Enrollment rejected: Node is blocked. NodeId: {NodeId}",
                request.NodeId);
            return EnrollmentResult.Blocked();
        }

        // Check for existing pending enrollment
        var existingEnrollment = _enrollments.Values
            .FirstOrDefault(e => e.NodeId == request.NodeId && e.Status == EnrollmentStatus.Pending);

        if (existingEnrollment is not null)
        {
            _logger.LogInformation(
                "Existing pending enrollment found for node. NodeId: {NodeId}, EnrollmentId: {EnrollmentId}",
                request.NodeId,
                existingEnrollment.EnrollmentId);

            return EnrollmentResult.Pending(existingEnrollment.EnrollmentId);
        }

        // TODO: Validate signature using node's public key
        // For now, we trust the signature is valid

        var enrollmentId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var enrollment = new PendingEnrollment
        {
            EnrollmentId = enrollmentId,
            NodeId = request.NodeId,
            NodeName = request.NodeName,
            PublicKey = request.PublicKey,
            RequestedCapabilities = request.RequestedCapabilities,
            Metadata = request.Metadata,
            RequestedAt = now,
            ExpiresAt = now.AddDays(_options.Enrollment.PendingExpirationDays),
            Status = EnrollmentStatus.Pending,
            BootstrapTokenId = bootstrapTokenId
        };

        _enrollments[enrollmentId] = enrollment;

        _logger.LogInformation(
            "Node enrollment requested. NodeId: {NodeId}, NodeName: {NodeName}, EnrollmentId: {EnrollmentId}",
            request.NodeId,
            request.NodeName,
            enrollmentId);

        // Check for auto-approve patterns
        if (ShouldAutoApprove(request))
        {
            _logger.LogInformation(
                "Auto-approving enrollment based on pattern match. EnrollmentId: {EnrollmentId}",
                enrollmentId);

            var certificate = await ApproveEnrollmentAsync(
                enrollmentId,
                new ApprovalOptions { GrantedCapabilities = request.RequestedCapabilities.ToList() },
                "system:auto-approve",
                cancellationToken);

            return EnrollmentResult.AutoApproved(enrollmentId, certificate);
        }

        return EnrollmentResult.Pending(enrollmentId);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingEnrollment>> GetPendingEnrollmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = _enrollments.Values
            .Where(e => e.Status == EnrollmentStatus.Pending)
            .OrderBy(e => e.RequestedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PendingEnrollment>>(pending);
    }

    /// <inheritdoc />
    public Task<PendingEnrollment?> GetEnrollmentAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        _enrollments.TryGetValue(enrollmentId, out var enrollment);
        return Task.FromResult(enrollment);
    }

    /// <inheritdoc />
    public async Task<EnrollmentStatusResult> GetEnrollmentStatusAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        if (!_enrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            return new EnrollmentStatusResult { Status = EnrollmentStatus.Failed };
        }

        // Check if expired
        if (enrollment.Status == EnrollmentStatus.Pending &&
            enrollment.ExpiresAt < DateTimeOffset.UtcNow)
        {
            var expired = enrollment with { Status = EnrollmentStatus.Expired };
            _enrollments.TryUpdate(enrollmentId, expired, enrollment);
            return new EnrollmentStatusResult { Status = EnrollmentStatus.Expired };
        }

        var result = new EnrollmentStatusResult
        {
            Status = enrollment.Status
        };

        if (enrollment.Status == EnrollmentStatus.Approved)
        {
            var certificate = await _credentialService.GetCertificateAsync(
                enrollment.NodeId, cancellationToken);
            result = result with { Certificate = certificate };
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<NodeCertificate> ApproveEnrollmentAsync(
        string enrollmentId,
        ApprovalOptions options,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_enrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            throw new InvalidOperationException($"Enrollment not found: {enrollmentId}");
        }

        if (enrollment.Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Enrollment is not pending: {enrollment.Status}");
        }

        // Determine capabilities to grant
        var capabilities = options.GrantedCapabilities
            ?? enrollment.RequestedCapabilities.ToList();

        // Issue certificate
        var certificate = await _credentialService.IssueCertificateAsync(
            enrollment.NodeId,
            enrollment.NodeName,
            enrollment.PublicKey,
            capabilities,
            options.CertificateValidityDays,
            cancellationToken);

        // Update enrollment status
        var approved = enrollment with { Status = EnrollmentStatus.Approved };
        _enrollments.TryUpdate(enrollmentId, approved, enrollment);

        _logger.LogInformation(
            "Enrollment approved. EnrollmentId: {EnrollmentId}, NodeId: {NodeId}, ApprovedBy: {ApprovedBy}",
            enrollmentId,
            enrollment.NodeId,
            approvedBy);

        return certificate;
    }

    /// <inheritdoc />
    public Task RejectEnrollmentAsync(
        string enrollmentId,
        string? reason,
        string rejectedBy,
        bool blockNode = false,
        CancellationToken cancellationToken = default)
    {
        if (!_enrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            throw new InvalidOperationException($"Enrollment not found: {enrollmentId}");
        }

        if (enrollment.Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Enrollment is not pending: {enrollment.Status}");
        }

        // Update enrollment status
        var rejected = enrollment with { Status = EnrollmentStatus.Rejected };
        _enrollments.TryUpdate(enrollmentId, rejected, enrollment);

        // Block node if requested
        if (blockNode)
        {
            _blockedNodes[enrollment.NodeId] = new BlockedNode
            {
                NodeId = enrollment.NodeId,
                BlockedAt = DateTimeOffset.UtcNow,
                Reason = reason ?? "Enrollment rejected",
                BlockedBy = rejectedBy
            };

            _logger.LogWarning(
                "Node blocked from future enrollments. NodeId: {NodeId}, BlockedBy: {BlockedBy}",
                enrollment.NodeId,
                rejectedBy);
        }

        _logger.LogInformation(
            "Enrollment rejected. EnrollmentId: {EnrollmentId}, NodeId: {NodeId}, RejectedBy: {RejectedBy}, Reason: {Reason}",
            enrollmentId,
            enrollment.NodeId,
            rejectedBy,
            reason ?? "No reason provided");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsNodeBlockedAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_blockedNodes.ContainsKey(nodeId));
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIds = _enrollments
            .Where(kvp => kvp.Value.ExpiresAt < now && kvp.Value.Status == EnrollmentStatus.Pending)
            .Select(kvp => kvp.Key)
            .ToList();

        var cleaned = 0;
        foreach (var id in expiredIds)
        {
            if (_enrollments.TryGetValue(id, out var enrollment))
            {
                var expired = enrollment with { Status = EnrollmentStatus.Expired };
                if (_enrollments.TryUpdate(id, expired, enrollment))
                {
                    cleaned++;
                }
            }
        }

        if (cleaned > 0)
        {
            _logger.LogInformation("Marked {Count} enrollments as expired", cleaned);
        }

        return Task.FromResult(cleaned);
    }

    private bool ShouldAutoApprove(EnrollmentRequest request)
    {
        if (!_options.Enrollment.AutoApprove)
        {
            return false;
        }

        // Check against auto-approve patterns
        foreach (var pattern in _options.Enrollment.AutoApprovePatterns)
        {
            if (MatchesPattern(request.NodeName, pattern) ||
                MatchesPattern(request.NodeId, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (pattern.StartsWith('*') && pattern.EndsWith('*'))
        {
            return value.Contains(pattern[1..^1], StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.StartsWith('*'))
        {
            return value.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith('*'))
        {
            return value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        }

        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Information about a blocked node.
/// </summary>
public sealed record BlockedNode
{
    public required string NodeId { get; init; }
    public DateTimeOffset BlockedAt { get; init; }
    public required string Reason { get; init; }
    public required string BlockedBy { get; init; }
}
