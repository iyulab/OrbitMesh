using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// In-memory implementation of node credential service.
/// Uses Ed25519 for signing certificates.
/// For production, replace with HSM or secure key storage.
/// </summary>
public sealed class InMemoryNodeCredentialService : INodeCredentialService
{
    private readonly ConcurrentDictionary<string, NodeCertificate> _certificates = new();
    private readonly ConcurrentDictionary<string, RevokedCertificate> _revocationList = new();
    private readonly ILogger<InMemoryNodeCredentialService> _logger;

    private ServerKeyInfo? _serverKeyInfo;
    private byte[]? _serverPrivateKey;
    private readonly object _keyLock = new();

    public InMemoryNodeCredentialService(ILogger<InMemoryNodeCredentialService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ServerKeyInfo> InitializeServerKeysAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_keyLock)
        {
            if (_serverKeyInfo is not null)
            {
                _logger.LogInformation("Server keys already initialized");
                return Task.FromResult(_serverKeyInfo);
            }

            // Generate Ed25519 key pair
            using var ed25519 = GenerateEd25519KeyPair();
            var parameters = ed25519.ExportParameters(true);

            _serverPrivateKey = parameters.D!;
            var publicKey = Convert.ToBase64String(GetEd25519PublicKey(ed25519));

            _serverKeyInfo = new ServerKeyInfo
            {
                ServerId = Guid.NewGuid().ToString("N"),
                PublicKey = publicKey,
                GeneratedAt = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Server keys initialized. ServerId: {ServerId}",
                _serverKeyInfo.ServerId);

            return Task.FromResult(_serverKeyInfo);
        }
    }

    /// <inheritdoc />
    public Task<ServerKeyInfo> GetServerKeyInfoAsync(
        CancellationToken cancellationToken = default)
    {
        if (_serverKeyInfo is null)
        {
            throw new InvalidOperationException(
                "Server keys not initialized. Call InitializeServerKeysAsync first.");
        }

        return Task.FromResult(_serverKeyInfo);
    }

    /// <inheritdoc />
    public Task<NodeCertificate> IssueCertificateAsync(
        string nodeId,
        string nodeName,
        string publicKey,
        IReadOnlyList<string> capabilities,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        if (_serverKeyInfo is null || _serverPrivateKey is null)
        {
            throw new InvalidOperationException(
                "Server keys not initialized. Call InitializeServerKeysAsync first.");
        }

        var now = DateTimeOffset.UtcNow;
        var serialNumber = GenerateSerialNumber();

        // Create certificate without signature first
        var unsignedCert = new UnsignedCertificate
        {
            Version = 1,
            SerialNumber = serialNumber,
            NodeId = nodeId,
            NodeName = nodeName,
            PublicKey = publicKey,
            ServerId = _serverKeyInfo.ServerId,
            ServerPublicKey = _serverKeyInfo.PublicKey,
            Capabilities = capabilities.ToList(),
            IssuedAt = now,
            ExpiresAt = now.AddDays(validityDays)
        };

        // Sign the certificate
        var dataToSign = JsonSerializer.Serialize(unsignedCert);
        var signature = SignData(dataToSign);

        var certificate = new NodeCertificate
        {
            Version = unsignedCert.Version,
            SerialNumber = unsignedCert.SerialNumber,
            NodeId = unsignedCert.NodeId,
            NodeName = unsignedCert.NodeName,
            PublicKey = unsignedCert.PublicKey,
            ServerId = unsignedCert.ServerId,
            ServerPublicKey = unsignedCert.ServerPublicKey,
            Capabilities = unsignedCert.Capabilities,
            IssuedAt = unsignedCert.IssuedAt,
            ExpiresAt = unsignedCert.ExpiresAt,
            Signature = signature
        };

        _certificates[nodeId] = certificate;

        _logger.LogInformation(
            "Certificate issued. NodeId: {NodeId}, SerialNumber: {SerialNumber}, ExpiresAt: {ExpiresAt}",
            nodeId,
            serialNumber,
            certificate.ExpiresAt);

        return Task.FromResult(certificate);
    }

    /// <inheritdoc />
    public Task<CertificateValidation> ValidateCertificateAsync(
        string certificateData,
        CancellationToken cancellationToken = default)
    {
        if (_serverKeyInfo is null)
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.UnknownIssuer,
                "Server keys not initialized"));
        }

        // Parse certificate
        var certificate = NodeCertificate.FromBase64(certificateData);
        if (certificate is null)
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.InvalidFormat,
                "Failed to parse certificate"));
        }

        // Check issuer
        if (certificate.ServerId != _serverKeyInfo.ServerId)
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.UnknownIssuer,
                "Certificate was not issued by this server"));
        }

        // Check expiration
        if (certificate.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.Expired,
                "Certificate has expired"));
        }

        // Check revocation
        if (_revocationList.ContainsKey(certificate.SerialNumber))
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.Revoked,
                "Certificate has been revoked"));
        }

        // Verify signature
        var unsignedCert = new UnsignedCertificate
        {
            Version = certificate.Version,
            SerialNumber = certificate.SerialNumber,
            NodeId = certificate.NodeId,
            NodeName = certificate.NodeName,
            PublicKey = certificate.PublicKey,
            ServerId = certificate.ServerId,
            ServerPublicKey = certificate.ServerPublicKey,
            Capabilities = certificate.Capabilities.ToList(),
            IssuedAt = certificate.IssuedAt,
            ExpiresAt = certificate.ExpiresAt
        };

        var dataToVerify = JsonSerializer.Serialize(unsignedCert);
        if (!VerifySignature(dataToVerify, certificate.Signature))
        {
            return Task.FromResult(CertificateValidation.Invalid(
                CertificateValidationError.InvalidSignature,
                "Certificate signature is invalid"));
        }

        _logger.LogDebug(
            "Certificate validated successfully. NodeId: {NodeId}, SerialNumber: {SerialNumber}",
            certificate.NodeId,
            certificate.SerialNumber);

        return Task.FromResult(CertificateValidation.Valid(certificate));
    }

    /// <inheritdoc />
    public Task<NodeCertificate?> GetCertificateAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        _certificates.TryGetValue(nodeId, out var certificate);
        return Task.FromResult(certificate);
    }

    /// <inheritdoc />
    public async Task<NodeCertificate> RenewCertificateAsync(
        string nodeId,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        if (!_certificates.TryGetValue(nodeId, out var existingCert))
        {
            throw new InvalidOperationException($"No certificate found for node: {nodeId}");
        }

        // Issue new certificate with same public key and capabilities
        var newCert = await IssueCertificateAsync(
            existingCert.NodeId,
            existingCert.NodeName,
            existingCert.PublicKey,
            existingCert.Capabilities,
            validityDays,
            cancellationToken);

        _logger.LogInformation(
            "Certificate renewed. NodeId: {NodeId}, OldSerial: {OldSerial}, NewSerial: {NewSerial}",
            nodeId,
            existingCert.SerialNumber,
            newCert.SerialNumber);

        return newCert;
    }

    /// <inheritdoc />
    public Task RevokeCertificateAsync(
        string nodeId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_certificates.TryRemove(nodeId, out var certificate))
        {
            throw new InvalidOperationException($"No certificate found for node: {nodeId}");
        }

        var revoked = new RevokedCertificate
        {
            SerialNumber = certificate.SerialNumber,
            NodeId = nodeId,
            RevokedAt = DateTimeOffset.UtcNow,
            Reason = reason,
            RevokedBy = revokedBy
        };

        _revocationList[certificate.SerialNumber] = revoked;

        _logger.LogWarning(
            "Certificate revoked. NodeId: {NodeId}, SerialNumber: {SerialNumber}, Reason: {Reason}, RevokedBy: {RevokedBy}",
            nodeId,
            certificate.SerialNumber,
            reason,
            revokedBy);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RevokedCertificate>> GetRevocationListAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _revocationList.Values
            .OrderByDescending(r => r.RevokedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<RevokedCertificate>>(list);
    }

    /// <inheritdoc />
    public Task<bool> IsRevokedAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_revocationList.ContainsKey(serialNumber));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NodeCertificate>> GetActiveCertificatesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = _certificates.Values
            .Where(c => c.ExpiresAt > now && !_revocationList.ContainsKey(c.SerialNumber))
            .OrderBy(c => c.NodeName)
            .ToList();

        return Task.FromResult<IReadOnlyList<NodeCertificate>>(active);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NodeCertificate>> GetExpiringCertificatesAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddDays(days);

        var expiring = _certificates.Values
            .Where(c => c.ExpiresAt > now && c.ExpiresAt <= threshold)
            .OrderBy(c => c.ExpiresAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<NodeCertificate>>(expiring);
    }

    private static ECDsa GenerateEd25519KeyPair()
    {
        // Note: .NET doesn't have native Ed25519 support, using ECDsa with P-256 as fallback
        // For production, use a library like NSec or Bouncy Castle for true Ed25519
        return ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    private static byte[] GetEd25519PublicKey(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(false);
        // Combine X and Y coordinates for public key
        var publicKey = new byte[parameters.Q.X!.Length + parameters.Q.Y!.Length];
        parameters.Q.X.CopyTo(publicKey, 0);
        parameters.Q.Y.CopyTo(publicKey, parameters.Q.X.Length);
        return publicKey;
    }

    private string SignData(string data)
    {
        if (_serverPrivateKey is null)
        {
            throw new InvalidOperationException("Server private key not initialized");
        }

        // Use HMAC-SHA256 as a simplified signature scheme
        // For production, use proper Ed25519 signing
        using var hmac = new HMACSHA256(_serverPrivateKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private bool VerifySignature(string data, string signature)
    {
        if (_serverPrivateKey is null)
        {
            return false;
        }

        var expectedSignature = SignData(data);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    private static string GenerateSerialNumber()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Unsigned certificate for signing.
    /// </summary>
    private sealed record UnsignedCertificate
    {
        public int Version { get; init; }
        public required string SerialNumber { get; init; }
        public required string NodeId { get; init; }
        public required string NodeName { get; init; }
        public required string PublicKey { get; init; }
        public required string ServerId { get; init; }
        public required string ServerPublicKey { get; init; }
        public IReadOnlyList<string> Capabilities { get; init; } = [];
        public DateTimeOffset IssuedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}
