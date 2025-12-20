using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// REST API controller for node enrollment and bootstrap token management.
/// Most endpoints require admin authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AdminAuthorize]
public class EnrollmentController : ControllerBase
{
    private readonly IBootstrapTokenService _bootstrapTokenService;
    private readonly INodeEnrollmentService _enrollmentService;
    private readonly INodeCredentialService _credentialService;
    private readonly ILogger<EnrollmentController> _logger;

    public EnrollmentController(
        IBootstrapTokenService bootstrapTokenService,
        INodeEnrollmentService enrollmentService,
        INodeCredentialService credentialService,
        ILogger<EnrollmentController> logger)
    {
        _bootstrapTokenService = bootstrapTokenService;
        _enrollmentService = enrollmentService;
        _credentialService = credentialService;
        _logger = logger;
    }

    #region Bootstrap Token (Admin)

    /// <summary>
    /// Gets the current bootstrap token configuration.
    /// Token value is never returned - use Regenerate to get a new token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bootstrap token configuration (without token value).</returns>
    [HttpGet("bootstrap-token")]
    [ProducesResponseType(typeof(BootstrapToken), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBootstrapToken(
        CancellationToken cancellationToken = default)
    {
        var token = await _bootstrapTokenService.GetTokenAsync(cancellationToken);
        return Ok(token);
    }

    /// <summary>
    /// Regenerates the bootstrap token.
    /// The old token becomes invalid immediately.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new token (value only shown once).</returns>
    [HttpPost("bootstrap-token/regenerate")]
    [ProducesResponseType(typeof(BootstrapToken), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerateBootstrapToken(
        CancellationToken cancellationToken = default)
    {
        var token = await _bootstrapTokenService.RegenerateAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap token regenerated via API. TokenId: {TokenId}",
            token.Id);

        return Ok(token);
    }

    /// <summary>
    /// Sets whether the bootstrap token is enabled.
    /// </summary>
    /// <param name="request">Enabled state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("bootstrap-token/enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetBootstrapTokenEnabled(
        [FromBody] SetEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        await _bootstrapTokenService.SetEnabledAsync(request.Enabled, cancellationToken);

        _logger.LogInformation(
            "Bootstrap token {Action} via API",
            request.Enabled ? "enabled" : "disabled");

        return NoContent();
    }

    /// <summary>
    /// Sets whether enrollments using the bootstrap token are auto-approved.
    /// </summary>
    /// <param name="request">Auto-approve state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("bootstrap-token/auto-approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetBootstrapTokenAutoApprove(
        [FromBody] SetAutoApproveRequest request,
        CancellationToken cancellationToken = default)
    {
        await _bootstrapTokenService.SetAutoApproveAsync(request.AutoApprove, cancellationToken);

        _logger.LogInformation(
            "Bootstrap token auto-approve set to {AutoApprove} via API",
            request.AutoApprove);

        return NoContent();
    }

    #endregion

    #region Node Enrollment

    /// <summary>
    /// Gets the enrollment status (for node polling).
    /// This endpoint is accessible without authentication for node polling.
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current enrollment status.</returns>
    [HttpGet("status/{enrollmentId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EnrollmentStatusResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollmentStatus(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var status = await _enrollmentService.GetEnrollmentStatusAsync(
            enrollmentId, cancellationToken);

        if (status.Status == EnrollmentStatus.Failed)
        {
            return NotFound(new { Error = "Enrollment not found" });
        }

        return Ok(status);
    }

    /// <summary>
    /// Gets all pending enrollments (admin).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending enrollments.</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingEnrollment>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingEnrollments(
        CancellationToken cancellationToken = default)
    {
        var pending = await _enrollmentService.GetPendingEnrollmentsAsync(cancellationToken);
        return Ok(pending);
    }

    /// <summary>
    /// Gets a specific enrollment by ID (admin).
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enrollment details.</returns>
    [HttpGet("{enrollmentId}")]
    [ProducesResponseType(typeof(PendingEnrollment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollment(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentService.GetEnrollmentAsync(
            enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return NotFound(new { Error = "Enrollment not found" });
        }

        return Ok(enrollment);
    }

    /// <summary>
    /// Approves an enrollment request (admin).
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID to approve.</param>
    /// <param name="options">Approval options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued certificate.</returns>
    [HttpPost("{enrollmentId}/approve")]
    [ProducesResponseType(typeof(NodeCertificate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveEnrollment(
        string enrollmentId,
        [FromBody] ApprovalOptions? options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            options ??= new ApprovalOptions();

            // TODO: Get admin identity from authentication context
            var approvedBy = "admin";

            var certificate = await _enrollmentService.ApproveEnrollmentAsync(
                enrollmentId,
                options,
                approvedBy,
                cancellationToken);

            _logger.LogInformation(
                "Enrollment approved via API. EnrollmentId: {EnrollmentId}, ApprovedBy: {ApprovedBy}",
                enrollmentId,
                approvedBy);

            return Ok(certificate);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Rejects an enrollment request (admin).
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID to reject.</param>
    /// <param name="request">Rejection request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpPost("{enrollmentId}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectEnrollment(
        string enrollmentId,
        [FromBody] RejectEnrollmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Get admin identity from authentication context
            var rejectedBy = "admin";

            await _enrollmentService.RejectEnrollmentAsync(
                enrollmentId,
                request?.Reason,
                rejectedBy,
                request?.BlockNode ?? false,
                cancellationToken);

            _logger.LogInformation(
                "Enrollment rejected via API. EnrollmentId: {EnrollmentId}, RejectedBy: {RejectedBy}, BlockNode: {BlockNode}",
                enrollmentId,
                rejectedBy,
                request?.BlockNode ?? false);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion

    #region Certificates

    /// <summary>
    /// Gets the server's public key information.
    /// This endpoint is accessible without authentication for initial node setup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server key info.</returns>
    [HttpGet("server-key")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ServerKeyInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServerKeyInfo(
        CancellationToken cancellationToken = default)
    {
        var keyInfo = await _credentialService.GetServerKeyInfoAsync(cancellationToken);
        return Ok(keyInfo);
    }

    /// <summary>
    /// Gets a node's certificate (admin).
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Node certificate.</returns>
    [HttpGet("certificates/{nodeId}")]
    [ProducesResponseType(typeof(NodeCertificate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodeCertificate(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var certificate = await _credentialService.GetCertificateAsync(
            nodeId, cancellationToken);

        if (certificate is null)
        {
            return NotFound(new { Error = "Certificate not found" });
        }

        return Ok(certificate);
    }

    /// <summary>
    /// Gets all active certificates (admin).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active certificates.</returns>
    [HttpGet("certificates")]
    [ProducesResponseType(typeof(IReadOnlyList<NodeCertificate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCertificates(
        CancellationToken cancellationToken = default)
    {
        var certificates = await _credentialService.GetActiveCertificatesAsync(cancellationToken);
        return Ok(certificates);
    }

    /// <summary>
    /// Revokes a node's certificate (admin).
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="request">Revocation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpPost("certificates/{nodeId}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeCertificate(
        string nodeId,
        [FromBody] RevokeCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Get admin identity from authentication context
            var revokedBy = "admin";

            await _credentialService.RevokeCertificateAsync(
                nodeId,
                request.Reason,
                revokedBy,
                cancellationToken);

            _logger.LogInformation(
                "Certificate revoked via API. NodeId: {NodeId}, RevokedBy: {RevokedBy}, Reason: {Reason}",
                nodeId,
                revokedBy,
                request.Reason);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the certificate revocation list.
    /// This endpoint is accessible without authentication for certificate validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of revoked certificates.</returns>
    [HttpGet("revocation-list")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<RevokedCertificate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevocationList(
        CancellationToken cancellationToken = default)
    {
        var revoked = await _credentialService.GetRevocationListAsync(cancellationToken);
        return Ok(revoked);
    }

    #endregion
}

/// <summary>
/// Request to reject an enrollment.
/// </summary>
public sealed record RejectEnrollmentRequest
{
    /// <summary>
    /// Rejection reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether to block the node from future enrollments.
    /// </summary>
    public bool BlockNode { get; init; }
}

/// <summary>
/// Request to revoke a certificate.
/// </summary>
public sealed record RevokeCertificateRequest
{
    /// <summary>
    /// Revocation reason (required).
    /// </summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Request to set bootstrap token enabled state.
/// </summary>
public sealed record SetEnabledRequest
{
    /// <summary>
    /// Whether the token is enabled.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// Request to set bootstrap token auto-approve state.
/// </summary>
public sealed record SetAutoApproveRequest
{
    /// <summary>
    /// Whether enrollments are auto-approved.
    /// </summary>
    public bool AutoApprove { get; init; }
}
