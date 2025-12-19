using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite-backed implementation of node enrollment service.
/// Uses IDbContextFactory for proper scoping with SignalR hubs.
/// </summary>
public sealed class SqliteNodeEnrollmentStore : INodeEnrollmentService
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private readonly INodeCredentialService _credentialService;
    private readonly SecurityOptions _options;
    private readonly ILogger<SqliteNodeEnrollmentStore> _logger;

    public SqliteNodeEnrollmentStore(
        IDbContextFactory<OrbitMeshDbContext> contextFactory,
        INodeCredentialService credentialService,
        IOptions<SecurityOptions> options,
        ILogger<SqliteNodeEnrollmentStore> logger)
    {
        _contextFactory = contextFactory;
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
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Check if node is blocked
        if (await dbContext.BlockedNodes.AnyAsync(b => b.NodeId == request.NodeId, cancellationToken))
        {
            _logger.LogWarning(
                "Enrollment rejected: Node is blocked. NodeId: {NodeId}",
                request.NodeId);
            return EnrollmentResult.Blocked();
        }

        // Check for existing pending enrollment
        var existingEnrollment = await dbContext.Enrollments
            .Where(e => e.NodeId == request.NodeId && e.Status == (int)EnrollmentStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEnrollment is not null)
        {
            _logger.LogInformation(
                "Existing pending enrollment found for node. NodeId: {NodeId}, EnrollmentId: {EnrollmentId}",
                request.NodeId,
                existingEnrollment.EnrollmentId);

            return EnrollmentResult.Pending(existingEnrollment.EnrollmentId);
        }

        var enrollmentId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var entity = new EnrollmentEntity
        {
            EnrollmentId = enrollmentId,
            NodeId = request.NodeId,
            NodeName = request.NodeName,
            PublicKey = request.PublicKey,
            RequestedCapabilitiesJson = request.RequestedCapabilities.Count > 0
                ? JsonSerializer.Serialize(request.RequestedCapabilities)
                : null,
            MetadataJson = request.Metadata.Count > 0
                ? JsonSerializer.Serialize(request.Metadata)
                : null,
            RequestedAt = now,
            ExpiresAt = now.AddDays(_options.Enrollment.PendingExpirationDays),
            Status = (int)EnrollmentStatus.Pending,
            BootstrapTokenId = bootstrapTokenId
        };

        dbContext.Enrollments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

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
    public async Task<IReadOnlyList<PendingEnrollment>> GetPendingEnrollmentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Enrollments
            .Where(e => e.Status == (int)EnrollmentStatus.Pending)
            .OrderBy(e => e.RequestedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToPendingEnrollment).ToList();
    }

    /// <inheritdoc />
    public async Task<PendingEnrollment?> GetEnrollmentAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken);

        return entity is null ? null : MapToPendingEnrollment(entity);
    }

    /// <inheritdoc />
    public async Task<EnrollmentStatusResult> GetEnrollmentStatusAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken);

        if (entity is null)
        {
            return new EnrollmentStatusResult { Status = EnrollmentStatus.Failed };
        }

        // Check if expired
        if (entity.Status == (int)EnrollmentStatus.Pending &&
            entity.ExpiresAt < DateTimeOffset.UtcNow)
        {
            entity.Status = (int)EnrollmentStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new EnrollmentStatusResult { Status = EnrollmentStatus.Expired };
        }

        var status = (EnrollmentStatus)entity.Status;
        var result = new EnrollmentStatusResult { Status = status };

        if (status == EnrollmentStatus.Approved)
        {
            var certificate = await _credentialService.GetCertificateAsync(
                entity.NodeId, cancellationToken);
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
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment not found: {enrollmentId}");

        if (entity.Status != (int)EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Enrollment is not pending: {(EnrollmentStatus)entity.Status}");
        }

        // Determine capabilities to grant
        var requestedCapabilities = string.IsNullOrEmpty(entity.RequestedCapabilitiesJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(entity.RequestedCapabilitiesJson) ?? [];

        var capabilities = options.GrantedCapabilities ?? requestedCapabilities;

        // Issue certificate
        var certificate = await _credentialService.IssueCertificateAsync(
            entity.NodeId,
            entity.NodeName,
            entity.PublicKey,
            capabilities,
            options.CertificateValidityDays,
            cancellationToken);

        // Update enrollment status
        entity.Status = (int)EnrollmentStatus.Approved;
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enrollment approved. EnrollmentId: {EnrollmentId}, NodeId: {NodeId}, ApprovedBy: {ApprovedBy}",
            enrollmentId,
            entity.NodeId,
            approvedBy);

        return certificate;
    }

    /// <inheritdoc />
    public async Task RejectEnrollmentAsync(
        string enrollmentId,
        string? reason,
        string rejectedBy,
        bool blockNode = false,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment not found: {enrollmentId}");

        if (entity.Status != (int)EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Enrollment is not pending: {(EnrollmentStatus)entity.Status}");
        }

        // Update enrollment status
        entity.Status = (int)EnrollmentStatus.Rejected;

        // Block node if requested
        if (blockNode)
        {
            var blockedNode = new BlockedNodeEntity
            {
                NodeId = entity.NodeId,
                BlockedAt = DateTimeOffset.UtcNow,
                Reason = reason ?? "Enrollment rejected",
                BlockedBy = rejectedBy
            };

            dbContext.BlockedNodes.Add(blockedNode);

            _logger.LogWarning(
                "Node blocked from future enrollments. NodeId: {NodeId}, BlockedBy: {BlockedBy}",
                entity.NodeId,
                rejectedBy);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enrollment rejected. EnrollmentId: {EnrollmentId}, NodeId: {NodeId}, RejectedBy: {RejectedBy}, Reason: {Reason}",
            enrollmentId,
            entity.NodeId,
            rejectedBy,
            reason ?? "No reason provided");
    }

    /// <inheritdoc />
    public async Task<bool> IsNodeBlockedAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.BlockedNodes.AnyAsync(b => b.NodeId == nodeId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite DateTimeOffset comparison requires client-side evaluation
        var pendingEnrollments = await dbContext.Enrollments
            .Where(e => e.Status == (int)EnrollmentStatus.Pending)
            .ToListAsync(cancellationToken);

        var expiredEnrollments = pendingEnrollments
            .Where(e => e.ExpiresAt < now)
            .ToList();

        if (expiredEnrollments.Count == 0)
        {
            return 0;
        }

        foreach (var enrollment in expiredEnrollments)
        {
            enrollment.Status = (int)EnrollmentStatus.Expired;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Marked {Count} enrollments as expired", expiredEnrollments.Count);
        return expiredEnrollments.Count;
    }

    private static PendingEnrollment MapToPendingEnrollment(EnrollmentEntity entity)
    {
        return new PendingEnrollment
        {
            EnrollmentId = entity.EnrollmentId,
            NodeId = entity.NodeId,
            NodeName = entity.NodeName,
            PublicKey = entity.PublicKey,
            RequestedCapabilities = string.IsNullOrEmpty(entity.RequestedCapabilitiesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.RequestedCapabilitiesJson) ?? [],
            Metadata = string.IsNullOrEmpty(entity.MetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson)
                    ?? new Dictionary<string, string>(),
            RequestedAt = entity.RequestedAt,
            ExpiresAt = entity.ExpiresAt,
            Status = (EnrollmentStatus)entity.Status,
            BootstrapTokenId = entity.BootstrapTokenId ?? string.Empty
        };
    }

    private bool ShouldAutoApprove(EnrollmentRequest request)
    {
        if (!_options.Enrollment.AutoApprove)
        {
            return false;
        }

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
