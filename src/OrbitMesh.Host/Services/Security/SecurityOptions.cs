using System.Diagnostics.CodeAnalysis;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Configuration options for OrbitMesh security.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "OrbitMesh:Security";

    /// <summary>
    /// Bootstrap token options.
    /// </summary>
    public BootstrapTokenOptions BootstrapToken { get; set; } = new();

    /// <summary>
    /// Enrollment options.
    /// </summary>
    public EnrollmentOptions Enrollment { get; set; } = new();

    /// <summary>
    /// Certificate options.
    /// </summary>
    public CertificateOptions Certificate { get; set; } = new();

    /// <summary>
    /// Whether to require certificate-based authentication.
    /// If false, legacy token authentication is also allowed.
    /// </summary>
    public bool RequireCertificateAuth { get; set; }

    /// <summary>
    /// Whether to allow anonymous connections (no authentication).
    /// Should be false in production.
    /// </summary>
    public bool AllowAnonymous { get; set; }
}

/// <summary>
/// Options for bootstrap tokens.
/// </summary>
public sealed class BootstrapTokenOptions
{
    /// <summary>
    /// Default expiration time in hours for new tokens.
    /// </summary>
    public int ExpirationHours { get; set; } = 24;

    /// <summary>
    /// Maximum number of active (unused) tokens allowed.
    /// </summary>
    public int MaxActiveTokens { get; set; } = 100;

    /// <summary>
    /// Interval in minutes for cleanup of expired tokens.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Options for node enrollment.
/// </summary>
#pragma warning disable CA1002, CA2227 // Configuration classes need mutable collection properties
public sealed class EnrollmentOptions
{
    /// <summary>
    /// Whether to automatically approve enrollments matching certain patterns.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// Patterns for auto-approval (supports wildcards: * for any, prefix*, *suffix).
    /// </summary>
    public List<string> AutoApprovePatterns { get; set; } = [];

    /// <summary>
    /// Days until pending enrollments expire.
    /// </summary>
    public int PendingExpirationDays { get; set; } = 7;

    /// <summary>
    /// Whether to notify admins of new enrollment requests.
    /// </summary>
    public bool NotifyOnNewEnrollment { get; set; } = true;

    /// <summary>
    /// Webhook URL for enrollment notifications.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration property for URL binding")]
    public string? NotificationWebhookUrl { get; set; }
}
#pragma warning restore CA1002, CA2227

/// <summary>
/// Options for certificates.
/// </summary>
public sealed class CertificateOptions
{
    /// <summary>
    /// Default validity period in days for new certificates.
    /// </summary>
    public int ValidityDays { get; set; } = 90;

    /// <summary>
    /// Days before expiry to trigger renewal.
    /// </summary>
    public int RenewalThresholdDays { get; set; } = 30;

    /// <summary>
    /// Signing algorithm (currently only Ed25519 simulated via ECDSA).
    /// </summary>
    public string Algorithm { get; set; } = "Ed25519";

    /// <summary>
    /// Whether to automatically renew certificates before expiry.
    /// </summary>
    public bool AutoRenewal { get; set; } = true;
}
