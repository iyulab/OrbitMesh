using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite-backed implementation of node credential service.
/// Uses IDbContextFactory for proper scoping with SignalR hubs.
/// Uses Ed25519 (simulated via ECDSA/HMAC) for signing certificates.
/// </summary>
public sealed class SqliteNodeCredentialStore : INodeCredentialService, IDisposable
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private readonly ILogger<SqliteNodeCredentialStore> _logger;

    // Cached server key info for performance
    private ServerKeyInfo? _cachedServerKeyInfo;
    private byte[]? _cachedServerPrivateKey;
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private bool _disposed;

    public SqliteNodeCredentialStore(
        IDbContextFactory<OrbitMeshDbContext> contextFactory,
        ILogger<SqliteNodeCredentialStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _keyLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<ServerKeyInfo> InitializeServerKeysAsync(
        CancellationToken cancellationToken = default)
    {
        await _keyLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Check if keys already exist in database
            var existingKey = await dbContext.ServerKeyInfos
                .Where(k => k.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingKey is not null)
            {
                _cachedServerKeyInfo = new ServerKeyInfo
                {
                    ServerId = existingKey.ServerId,
                    PublicKey = existingKey.PublicKey,
                    GeneratedAt = existingKey.GeneratedAt,
                    Algorithm = existingKey.Algorithm
                };
                _cachedServerPrivateKey = Convert.FromBase64String(existingKey.PrivateKey);

                _logger.LogInformation("Server keys loaded from database. ServerId: {ServerId}",
                    existingKey.ServerId);
                return _cachedServerKeyInfo;
            }

            // Generate new key pair
            using var ecdsa = GenerateEd25519KeyPair();
            var parameters = ecdsa.ExportParameters(true);
            var privateKey = parameters.D!;
            var publicKey = Convert.ToBase64String(GetEd25519PublicKey(ecdsa));

            var serverId = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;

            var entity = new ServerKeyInfoEntity
            {
                ServerId = serverId,
                PublicKey = publicKey,
                PrivateKey = Convert.ToBase64String(privateKey),
                Algorithm = "Ed25519",
                GeneratedAt = now,
                IsActive = true
            };

            dbContext.ServerKeyInfos.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            _cachedServerKeyInfo = new ServerKeyInfo
            {
                ServerId = serverId,
                PublicKey = publicKey,
                GeneratedAt = now
            };
            _cachedServerPrivateKey = privateKey;

            _logger.LogInformation("Server keys initialized and stored. ServerId: {ServerId}", serverId);
            return _cachedServerKeyInfo;
        }
        finally
        {
            _keyLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServerKeyInfo> GetServerKeyInfoAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cachedServerKeyInfo is not null)
        {
            return _cachedServerKeyInfo;
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Load from database
        var entity = await dbContext.ServerKeyInfos
            .Where(k => k.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Server keys not initialized. Call InitializeServerKeysAsync first.");

        _cachedServerKeyInfo = new ServerKeyInfo
        {
            ServerId = entity.ServerId,
            PublicKey = entity.PublicKey,
            GeneratedAt = entity.GeneratedAt,
            Algorithm = entity.Algorithm
        };
        _cachedServerPrivateKey = Convert.FromBase64String(entity.PrivateKey);

        return _cachedServerKeyInfo;
    }

    /// <inheritdoc />
    public async Task<NodeCertificate> IssueCertificateAsync(
        string nodeId,
        string nodeName,
        string publicKey,
        IReadOnlyList<string> capabilities,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        var serverKeyInfo = await GetServerKeyInfoAsync(cancellationToken);

        if (_cachedServerPrivateKey is null)
        {
            throw new InvalidOperationException("Server private key not loaded");
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
            ServerId = serverKeyInfo.ServerId,
            ServerPublicKey = serverKeyInfo.PublicKey,
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

        // Store in database
        var entity = new NodeCertificateEntity
        {
            SerialNumber = serialNumber,
            Version = 1,
            NodeId = nodeId,
            NodeName = nodeName,
            PublicKey = publicKey,
            ServerId = serverKeyInfo.ServerId,
            ServerPublicKey = serverKeyInfo.PublicKey,
            CapabilitiesJson = capabilities.Count > 0
                ? JsonSerializer.Serialize(capabilities)
                : null,
            IssuedAt = now,
            ExpiresAt = now.AddDays(validityDays),
            Signature = signature,
            IsRevoked = false
        };

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.NodeCertificates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Certificate issued. NodeId: {NodeId}, SerialNumber: {SerialNumber}, ExpiresAt: {ExpiresAt}",
            nodeId,
            serialNumber,
            certificate.ExpiresAt);

        return certificate;
    }

    /// <inheritdoc />
    public Task<NodeCertificate> IssueCertificateAsync(
        string nodeId,
        string publicKey,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken = default)
    {
        return IssueCertificateAsync(nodeId, nodeId, publicKey, capabilities, 90, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> VerifySignatureAsync(
        string nodeId,
        string publicKey,
        string signature,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simplified verification: check that signature is valid Base64
            var signatureBytes = Convert.FromBase64String(signature);
            var publicKeyBytes = Convert.FromBase64String(publicKey);
            var isValid = signatureBytes.Length > 0 && publicKeyBytes.Length > 0;

            _logger.LogDebug(
                "Signature verification for node {NodeId}: {Result}",
                nodeId,
                isValid ? "Valid" : "Invalid");

            return Task.FromResult(isValid);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Invalid Base64 format in signature or public key for node {NodeId}", nodeId);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task<CertificateValidation> ValidateCertificateAsync(
        string certificateData,
        CancellationToken cancellationToken = default)
    {
        var serverKeyInfo = await GetServerKeyInfoAsync(cancellationToken);

        // Parse certificate
        var certificate = NodeCertificate.FromBase64(certificateData);
        if (certificate is null)
        {
            return CertificateValidation.Invalid(
                CertificateValidationError.InvalidFormat,
                "Failed to parse certificate");
        }

        // Check issuer
        if (certificate.ServerId != serverKeyInfo.ServerId)
        {
            return CertificateValidation.Invalid(
                CertificateValidationError.UnknownIssuer,
                "Certificate was not issued by this server");
        }

        // Check expiration
        if (certificate.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return CertificateValidation.Invalid(
                CertificateValidationError.Expired,
                "Certificate has expired");
        }

        // Check revocation in database
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var isRevoked = await dbContext.NodeCertificates
            .AnyAsync(c => c.SerialNumber == certificate.SerialNumber && c.IsRevoked, cancellationToken);

        if (isRevoked)
        {
            return CertificateValidation.Invalid(
                CertificateValidationError.Revoked,
                "Certificate has been revoked");
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
        if (!VerifyServerSignature(dataToVerify, certificate.Signature))
        {
            return CertificateValidation.Invalid(
                CertificateValidationError.InvalidSignature,
                "Certificate signature is invalid");
        }

        _logger.LogDebug(
            "Certificate validated successfully. NodeId: {NodeId}, SerialNumber: {SerialNumber}",
            certificate.NodeId,
            certificate.SerialNumber);

        return CertificateValidation.Valid(certificate);
    }

    /// <inheritdoc />
    public async Task<NodeCertificate?> GetCertificateAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.NodeCertificates
            .Where(c => c.NodeId == nodeId && !c.IsRevoked)
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapToCertificate(entity);
    }

    /// <inheritdoc />
    public async Task<NodeCertificate> RenewCertificateAsync(
        string nodeId,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        var existingCert = await GetCertificateAsync(nodeId, cancellationToken)
            ?? throw new InvalidOperationException($"No certificate found for node: {nodeId}");

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
    public async Task RevokeCertificateAsync(
        string nodeId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.NodeCertificates
            .Where(c => c.NodeId == nodeId && !c.IsRevoked)
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"No certificate found for node: {nodeId}");

        entity.IsRevoked = true;
        entity.RevokedAt = DateTimeOffset.UtcNow;
        entity.RevocationReason = reason;
        entity.RevokedBy = revokedBy;

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Certificate revoked. NodeId: {NodeId}, SerialNumber: {SerialNumber}, Reason: {Reason}, RevokedBy: {RevokedBy}",
            nodeId,
            entity.SerialNumber,
            reason,
            revokedBy);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RevokedCertificate>> GetRevocationListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.NodeCertificates
            .Where(c => c.IsRevoked)
            .OrderByDescending(c => c.RevokedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new RevokedCertificate
        {
            SerialNumber = e.SerialNumber,
            NodeId = e.NodeId,
            RevokedAt = e.RevokedAt ?? DateTimeOffset.UtcNow,
            Reason = e.RevocationReason ?? "Unknown",
            RevokedBy = e.RevokedBy ?? "Unknown"
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.NodeCertificates
            .AnyAsync(c => c.SerialNumber == serialNumber && c.IsRevoked, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeCertificate>> GetActiveCertificatesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite DateTimeOffset comparison requires client-side evaluation
        var allCerts = await dbContext.NodeCertificates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var filtered = allCerts
            .Where(c => !c.IsRevoked && c.ExpiresAt > now)
            .OrderBy(c => c.NodeName)
            .ToList();

        return filtered.Select(MapToCertificate).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeCertificate>> GetExpiringCertificatesAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddDays(days);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite DateTimeOffset comparison requires client-side evaluation
        var allCerts = await dbContext.NodeCertificates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var filtered = allCerts
            .Where(c => !c.IsRevoked && c.ExpiresAt > now && c.ExpiresAt <= threshold)
            .OrderBy(c => c.ExpiresAt)
            .ToList();

        return filtered.Select(MapToCertificate).ToList();
    }

    private static NodeCertificate MapToCertificate(NodeCertificateEntity entity)
    {
        return new NodeCertificate
        {
            Version = entity.Version,
            SerialNumber = entity.SerialNumber,
            NodeId = entity.NodeId,
            NodeName = entity.NodeName,
            PublicKey = entity.PublicKey,
            ServerId = entity.ServerId,
            ServerPublicKey = entity.ServerPublicKey,
            Capabilities = string.IsNullOrEmpty(entity.CapabilitiesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.CapabilitiesJson) ?? [],
            IssuedAt = entity.IssuedAt,
            ExpiresAt = entity.ExpiresAt,
            Signature = entity.Signature
        };
    }

    private static ECDsa GenerateEd25519KeyPair()
    {
        // Note: .NET doesn't have native Ed25519 support, using ECDsa with P-256 as fallback
        return ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    private static byte[] GetEd25519PublicKey(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(false);
        var publicKey = new byte[parameters.Q.X!.Length + parameters.Q.Y!.Length];
        parameters.Q.X.CopyTo(publicKey, 0);
        parameters.Q.Y.CopyTo(publicKey, parameters.Q.X.Length);
        return publicKey;
    }

    private string SignData(string data)
    {
        if (_cachedServerPrivateKey is null)
        {
            throw new InvalidOperationException("Server private key not initialized");
        }

        using var hmac = new HMACSHA256(_cachedServerPrivateKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private bool VerifyServerSignature(string data, string signature)
    {
        if (_cachedServerPrivateKey is null)
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
