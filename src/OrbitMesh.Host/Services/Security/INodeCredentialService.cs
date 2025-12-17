namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Service for managing node credentials and certificates.
/// Handles certificate issuance, validation, and revocation.
/// </summary>
public interface INodeCredentialService
{
    /// <summary>
    /// Initializes server key pair on first run.
    /// If keys already exist, returns existing public key info.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server key information.</returns>
    Task<ServerKeyInfo> InitializeServerKeysAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server's public key information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server public key info.</returns>
    Task<ServerKeyInfo> GetServerKeyInfoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a certificate for an approved node.
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="nodeName">Node display name.</param>
    /// <param name="publicKey">Node's public key (Base64).</param>
    /// <param name="capabilities">Granted capabilities.</param>
    /// <param name="validityDays">Certificate validity in days.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued certificate.</returns>
    Task<NodeCertificate> IssueCertificateAsync(
        string nodeId,
        string nodeName,
        string publicKey,
        IReadOnlyList<string> capabilities,
        int validityDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a certificate for an approved node (simplified version).
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="publicKey">Node's public key (Base64).</param>
    /// <param name="capabilities">Granted capabilities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued certificate.</returns>
    Task<NodeCertificate> IssueCertificateAsync(
        string nodeId,
        string publicKey,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a signature made by a node's public key.
    /// Used for validating enrollment request signatures.
    /// </summary>
    /// <param name="nodeId">Node ID (used as signing context).</param>
    /// <param name="publicKey">Base64-encoded public key.</param>
    /// <param name="signature">Base64-encoded signature to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if signature is valid.</returns>
    Task<bool> VerifySignatureAsync(
        string nodeId,
        string publicKey,
        string signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a node certificate.
    /// </summary>
    /// <param name="certificateData">Base64-encoded certificate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    Task<CertificateValidation> ValidateCertificateAsync(
        string certificateData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a node's current certificate.
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Certificate if found, null otherwise.</returns>
    Task<NodeCertificate?> GetCertificateAsync(
        string nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a node's certificate before expiration.
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="validityDays">New certificate validity in days.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Renewed certificate.</returns>
    Task<NodeCertificate> RenewCertificateAsync(
        string nodeId,
        int validityDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a node's certificate.
    /// </summary>
    /// <param name="nodeId">Node ID.</param>
    /// <param name="reason">Revocation reason.</param>
    /// <param name="revokedBy">Admin who revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeCertificateAsync(
        string nodeId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the certificate revocation list.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of revoked certificates.</returns>
    Task<IReadOnlyList<RevokedCertificate>> GetRevocationListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a certificate serial number is revoked.
    /// </summary>
    /// <param name="serialNumber">Certificate serial number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if revoked.</returns>
    Task<bool> IsRevokedAsync(
        string serialNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (non-revoked, non-expired) certificates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active certificates.</returns>
    Task<IReadOnlyList<NodeCertificate>> GetActiveCertificatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets certificates expiring within the specified days.
    /// </summary>
    /// <param name="days">Days until expiration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of expiring certificates.</returns>
    Task<IReadOnlyList<NodeCertificate>> GetExpiringCertificatesAsync(
        int days = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Server key information.
/// </summary>
public sealed record ServerKeyInfo
{
    /// <summary>
    /// Server unique identifier.
    /// </summary>
    public required string ServerId { get; init; }

    /// <summary>
    /// Server's public key (Base64-encoded Ed25519).
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// When the key pair was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Key algorithm (always "Ed25519").
    /// </summary>
    public string Algorithm { get; init; } = "Ed25519";
}

/// <summary>
/// Node certificate issued after enrollment approval.
/// </summary>
public sealed record NodeCertificate
{
    /// <summary>
    /// Certificate format version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Unique certificate serial number.
    /// </summary>
    public required string SerialNumber { get; init; }

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
    /// Server ID that issued this certificate.
    /// </summary>
    public required string ServerId { get; init; }

    /// <summary>
    /// Server's public key (Base64).
    /// </summary>
    public required string ServerPublicKey { get; init; }

    /// <summary>
    /// Capabilities granted to this node.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// When the certificate was issued.
    /// </summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// When the certificate expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Server's signature of this certificate (Base64).
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Serializes the certificate to Base64 for transmission.
    /// </summary>
    public string ToBase64()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Deserializes a certificate from Base64.
    /// </summary>
    public static NodeCertificate? FromBase64(string base64)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return System.Text.Json.JsonSerializer.Deserialize<NodeCertificate>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of certificate validation.
/// </summary>
public sealed record CertificateValidation
{
    /// <summary>
    /// Whether the certificate is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Node ID from the certificate.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Node name from the certificate.
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// Capabilities granted by the certificate.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Validation error if invalid.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Validation failure reason.
    /// </summary>
    public CertificateValidationError? ErrorCode { get; init; }

    /// <summary>
    /// The validated certificate (if valid).
    /// </summary>
    public NodeCertificate? Certificate { get; init; }

    public static CertificateValidation Valid(NodeCertificate certificate) => new()
    {
        IsValid = true,
        NodeId = certificate.NodeId,
        NodeName = certificate.NodeName,
        Capabilities = certificate.Capabilities,
        Certificate = certificate
    };

    public static CertificateValidation Invalid(CertificateValidationError errorCode, string error) => new()
    {
        IsValid = false,
        Error = error,
        ErrorCode = errorCode
    };
}

/// <summary>
/// Certificate validation error codes.
/// </summary>
public enum CertificateValidationError
{
    /// <summary>
    /// Certificate format is invalid.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// Certificate signature is invalid.
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// Certificate has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Certificate has been revoked.
    /// </summary>
    Revoked,

    /// <summary>
    /// Certificate was not issued by this server.
    /// </summary>
    UnknownIssuer,

    /// <summary>
    /// Node is not registered in the system.
    /// </summary>
    UnknownNode
}

/// <summary>
/// Information about a revoked certificate.
/// </summary>
public sealed record RevokedCertificate
{
    /// <summary>
    /// Certificate serial number.
    /// </summary>
    public required string SerialNumber { get; init; }

    /// <summary>
    /// Node ID.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// When the certificate was revoked.
    /// </summary>
    public DateTimeOffset RevokedAt { get; init; }

    /// <summary>
    /// Revocation reason.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Admin who revoked the certificate.
    /// </summary>
    public required string RevokedBy { get; init; }
}
