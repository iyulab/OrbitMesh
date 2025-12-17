namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Service for managing node enrollment workflow.
/// Handles the process from enrollment request to admin approval.
/// </summary>
public interface INodeEnrollmentService
{
    /// <summary>
    /// Submits a new enrollment request from a node.
    /// </summary>
    /// <param name="request">Enrollment request with node details and public key.</param>
    /// <param name="bootstrapTokenId">The bootstrap token used for this enrollment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enrollment result with status and ID.</returns>
    Task<EnrollmentResult> RequestEnrollmentAsync(
        EnrollmentRequest request,
        string bootstrapTokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending enrollments awaiting approval.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending enrollments.</returns>
    Task<IReadOnlyList<PendingEnrollment>> GetPendingEnrollmentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific enrollment by ID.
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enrollment if found, null otherwise.</returns>
    Task<PendingEnrollment?> GetEnrollmentAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the status of an enrollment (for node polling).
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current enrollment status.</returns>
    Task<EnrollmentStatusResult> GetEnrollmentStatusAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an enrollment and triggers certificate issuance.
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID to approve.</param>
    /// <param name="options">Approval options (capabilities, restrictions).</param>
    /// <param name="approvedBy">Admin who approved the enrollment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued node certificate.</returns>
    Task<NodeCertificate> ApproveEnrollmentAsync(
        string enrollmentId,
        ApprovalOptions options,
        string approvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an enrollment request.
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID to reject.</param>
    /// <param name="reason">Optional rejection reason.</param>
    /// <param name="rejectedBy">Admin who rejected the enrollment.</param>
    /// <param name="blockNode">If true, block future enrollments from this node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RejectEnrollmentAsync(
        string enrollmentId,
        string? reason,
        string rejectedBy,
        bool blockNode = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a node ID is blocked from enrollment.
    /// </summary>
    /// <param name="nodeId">Node ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if blocked.</returns>
    Task<bool> IsNodeBlockedAsync(
        string nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired pending enrollments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of enrollments cleaned up.</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Node enrollment request submitted during initial connection.
/// </summary>
public sealed record EnrollmentRequest
{
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Display name for the node.
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// Base64-encoded Ed25519 public key.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// Capabilities the node is requesting.
    /// </summary>
    public IReadOnlyList<string> RequestedCapabilities { get; init; } = [];

    /// <summary>
    /// Node metadata (hostname, platform, version, etc.).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Request timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Signature of the request (signed with node's private key).
    /// </summary>
    public required string Signature { get; init; }
}

/// <summary>
/// Result of enrollment request submission.
/// </summary>
public sealed record EnrollmentResult
{
    /// <summary>
    /// Whether the request was accepted for processing.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Enrollment ID for status polling.
    /// </summary>
    public string? EnrollmentId { get; init; }

    /// <summary>
    /// Current enrollment status.
    /// </summary>
    public EnrollmentStatus Status { get; init; }

    /// <summary>
    /// Error message if request failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Certificate if auto-approved.
    /// </summary>
    public NodeCertificate? Certificate { get; init; }

    /// <summary>
    /// Approved capabilities (if auto-approved).
    /// </summary>
    public IReadOnlyList<string>? ApprovedCapabilities { get; init; }

    public static EnrollmentResult Pending(string enrollmentId) => new()
    {
        Success = true,
        EnrollmentId = enrollmentId,
        Status = EnrollmentStatus.Pending
    };

    public static EnrollmentResult AutoApproved(string enrollmentId, NodeCertificate certificate, IReadOnlyList<string>? capabilities = null) => new()
    {
        Success = true,
        EnrollmentId = enrollmentId,
        Status = EnrollmentStatus.Approved,
        Certificate = certificate,
        ApprovedCapabilities = capabilities ?? certificate.Capabilities
    };

    public static EnrollmentResult Failed(string error) => new()
    {
        Success = false,
        Status = EnrollmentStatus.Failed,
        Error = error
    };

    public static EnrollmentResult Blocked() => new()
    {
        Success = false,
        Status = EnrollmentStatus.Blocked,
        Error = "Node is blocked from enrollment"
    };
}

/// <summary>
/// Enrollment status.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>
    /// Enrollment is pending admin approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Enrollment has been approved.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Enrollment has been rejected.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Enrollment expired before approval.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// Enrollment failed (validation error).
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Node is blocked from enrollment.
    /// </summary>
    Blocked = 5
}

/// <summary>
/// Pending enrollment record.
/// </summary>
public sealed record PendingEnrollment
{
    /// <summary>
    /// Unique enrollment ID.
    /// </summary>
    public required string EnrollmentId { get; init; }

    /// <summary>
    /// Node ID.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Node display name.
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// Node's public key (Base64).
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// Requested capabilities.
    /// </summary>
    public IReadOnlyList<string> RequestedCapabilities { get; init; } = [];

    /// <summary>
    /// Node metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// When enrollment was requested.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// When enrollment expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Current status.
    /// </summary>
    public EnrollmentStatus Status { get; init; }

    /// <summary>
    /// Bootstrap token ID used for enrollment.
    /// </summary>
    public required string BootstrapTokenId { get; init; }

    /// <summary>
    /// IP address of enrolling node.
    /// </summary>
    public string? RemoteIpAddress { get; init; }
}

/// <summary>
/// Result of enrollment status check.
/// </summary>
public sealed record EnrollmentStatusResult
{
    /// <summary>
    /// Current status.
    /// </summary>
    public EnrollmentStatus Status { get; init; }

    /// <summary>
    /// Certificate if approved.
    /// </summary>
    public NodeCertificate? Certificate { get; init; }

    /// <summary>
    /// Rejection reason if rejected.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Estimated wait time for pending enrollments.
    /// </summary>
    public TimeSpan? EstimatedWaitTime { get; init; }

    /// <summary>
    /// Approved capabilities (if approved).
    /// </summary>
    public IReadOnlyList<string>? ApprovedCapabilities { get; init; }
}

/// <summary>
/// Options for enrollment approval.
/// </summary>
public sealed record ApprovalOptions
{
    /// <summary>
    /// Capabilities to grant (if different from requested).
    /// If null, grants all requested capabilities.
    /// </summary>
    public IReadOnlyList<string>? GrantedCapabilities { get; init; }

    /// <summary>
    /// Certificate validity in days. Default is 90.
    /// </summary>
    public int CertificateValidityDays { get; init; } = 90;

    /// <summary>
    /// Optional note about the approval.
    /// </summary>
    public string? Note { get; init; }
}
