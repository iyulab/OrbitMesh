using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Node.Security;

/// <summary>
/// Manages node credentials including key pairs and certificates.
/// Handles the enrollment workflow and certificate storage.
/// </summary>
public sealed class NodeCredentialManager : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _credentialsPath;
    private readonly ILogger<NodeCredentialManager> _logger;
    private ECDsa? _keyPair;
    private NodeCredentials? _credentials;
    private bool _disposed;

    /// <summary>
    /// Gets the current node credentials if available.
    /// </summary>
    public NodeCredentials? Credentials => _credentials;

    /// <summary>
    /// Gets whether the node is enrolled (has a valid certificate).
    /// </summary>
    public bool IsEnrolled => _credentials?.Certificate is not null;

    /// <summary>
    /// Gets the node's public key in Base64 format.
    /// </summary>
    public string? PublicKey { get; private set; }

    /// <summary>
    /// Creates a new NodeCredentialManager.
    /// </summary>
    /// <param name="credentialsPath">Path to store credentials file.</param>
    /// <param name="logger">Logger instance.</param>
    public NodeCredentialManager(string? credentialsPath, ILogger<NodeCredentialManager> logger)
    {
        _credentialsPath = credentialsPath ?? GetDefaultCredentialsPath();
        _logger = logger;
    }

    /// <summary>
    /// Initializes credentials - loads existing or generates new key pair.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try to load existing credentials
        if (File.Exists(_credentialsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_credentialsPath, cancellationToken);
                _credentials = JsonSerializer.Deserialize<NodeCredentials>(json);

                if (_credentials?.NodeId == nodeId && _credentials.PrivateKey is not null)
                {
                    // Restore key pair from stored private key
                    _keyPair = ECDsa.Create();
                    var privateKeyBytes = Convert.FromBase64String(_credentials.PrivateKey);
                    _keyPair.ImportECPrivateKey(privateKeyBytes, out _);
                    PublicKey = _credentials.PublicKey;

                    _logger.LogInformation(
                        "Loaded existing credentials. NodeId: {NodeId}, HasCertificate: {HasCert}",
                        nodeId,
                        _credentials.Certificate is not null);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing credentials, generating new ones");
            }
        }

        // Generate new key pair
        _keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = _keyPair.ExportParameters(true);

        // Store public key
        var publicKeyBytes = new byte[parameters.Q.X!.Length + parameters.Q.Y!.Length];
        parameters.Q.X.CopyTo(publicKeyBytes, 0);
        parameters.Q.Y.CopyTo(publicKeyBytes, parameters.Q.X.Length);
        PublicKey = Convert.ToBase64String(publicKeyBytes);

        // Store private key
        var exportedPrivateKey = _keyPair.ExportECPrivateKey();
        var privateKeyBase64 = Convert.ToBase64String(exportedPrivateKey);

        _credentials = new NodeCredentials
        {
            NodeId = nodeId,
            PublicKey = PublicKey,
            PrivateKey = privateKeyBase64,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        await SaveCredentialsAsync(cancellationToken);

        _logger.LogInformation("Generated new key pair for node. NodeId: {NodeId}", nodeId);
    }

    /// <summary>
    /// Signs data with the node's private key.
    /// </summary>
    /// <param name="data">Data to sign.</param>
    /// <returns>Base64-encoded signature.</returns>
    public string Sign(string data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_keyPair is null)
        {
            throw new InvalidOperationException("Credentials not initialized. Call InitializeAsync first.");
        }

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signature = _keyPair.SignData(dataBytes, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Creates a signature for the enrollment request.
    /// </summary>
    /// <param name="nodeId">Node identifier.</param>
    /// <returns>Base64-encoded signature.</returns>
    public string CreateEnrollmentSignature(string nodeId)
    {
        // Sign the nodeId as proof of key ownership
        return Sign(nodeId);
    }

    /// <summary>
    /// Stores the certificate received after enrollment approval.
    /// </summary>
    /// <param name="certificate">Base64-encoded certificate.</param>
    /// <param name="serverPublicKey">Server's public key for verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StoreCertificateAsync(
        string certificate,
        string serverPublicKey,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_credentials is null)
        {
            throw new InvalidOperationException("Credentials not initialized. Call InitializeAsync first.");
        }

        _credentials = _credentials with
        {
            Certificate = certificate,
            ServerPublicKey = serverPublicKey,
            EnrolledAt = DateTimeOffset.UtcNow
        };

        await SaveCredentialsAsync(cancellationToken);

        _logger.LogInformation("Certificate stored successfully. NodeId: {NodeId}", _credentials.NodeId);
    }

    /// <summary>
    /// Stores the enrollment ID for pending enrollments.
    /// </summary>
    /// <param name="enrollmentId">Enrollment ID for status polling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StoreEnrollmentIdAsync(
        string enrollmentId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_credentials is null)
        {
            throw new InvalidOperationException("Credentials not initialized. Call InitializeAsync first.");
        }

        _credentials = _credentials with
        {
            PendingEnrollmentId = enrollmentId
        };

        await SaveCredentialsAsync(cancellationToken);

        _logger.LogDebug("Enrollment ID stored. EnrollmentId: {EnrollmentId}", enrollmentId);
    }

    /// <summary>
    /// Clears credentials (for re-enrollment or testing).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearCredentialsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (File.Exists(_credentialsPath))
        {
            await Task.Run(() => File.Delete(_credentialsPath), cancellationToken);
        }

        _credentials = null;
        _keyPair?.Dispose();
        _keyPair = null;
        PublicKey = null;

        _logger.LogInformation("Credentials cleared");
    }

    private async Task SaveCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_credentials is null) return;

        var directory = Path.GetDirectoryName(_credentialsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_credentials, s_jsonOptions);

        await File.WriteAllTextAsync(_credentialsPath, json, cancellationToken);
    }

    private static string GetDefaultCredentialsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "OrbitMesh", "node-credentials.json");
    }

    /// <summary>
    /// Loads the stored node ID from credentials file if it exists.
    /// Returns null if no credentials exist.
    /// </summary>
    /// <param name="credentialsPath">Optional custom credentials path.</param>
    /// <returns>The stored node ID or null.</returns>
    public static string? LoadStoredNodeId(string? credentialsPath = null)
    {
        var path = credentialsPath ?? GetDefaultCredentialsPath();

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var credentials = JsonSerializer.Deserialize<NodeCredentials>(json);
            return credentials?.NodeId;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _keyPair?.Dispose();
    }
}

/// <summary>
/// Node credentials stored on disk.
/// </summary>
public sealed record NodeCredentials
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Base64-encoded public key.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// Base64-encoded private key (stored securely).
    /// </summary>
    public required string PrivateKey { get; init; }

    /// <summary>
    /// Base64-encoded certificate (after enrollment).
    /// </summary>
    public string? Certificate { get; init; }

    /// <summary>
    /// Server's public key for verification.
    /// </summary>
    public string? ServerPublicKey { get; init; }

    /// <summary>
    /// Pending enrollment ID (if enrollment not yet approved).
    /// </summary>
    public string? PendingEnrollmentId { get; init; }

    /// <summary>
    /// When credentials were generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// When certificate was received.
    /// </summary>
    public DateTimeOffset? EnrolledAt { get; init; }
}
