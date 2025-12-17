using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrbitMesh.Host.Services.Security;

/// <summary>
/// Background service that initializes security components on startup
/// and performs periodic cleanup tasks.
/// </summary>
public sealed class SecurityInitializationService : BackgroundService
{
    private readonly INodeCredentialService _credentialService;
    private readonly IBootstrapTokenService _bootstrapTokenService;
    private readonly INodeEnrollmentService _enrollmentService;
    private readonly SecurityOptions _options;
    private readonly ILogger<SecurityInitializationService> _logger;

    public SecurityInitializationService(
        INodeCredentialService credentialService,
        IBootstrapTokenService bootstrapTokenService,
        INodeEnrollmentService enrollmentService,
        IOptions<SecurityOptions> options,
        ILogger<SecurityInitializationService> logger)
    {
        _credentialService = credentialService;
        _bootstrapTokenService = bootstrapTokenService;
        _enrollmentService = enrollmentService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize server keys
        await InitializeServerKeysAsync(stoppingToken);

        // Start cleanup loop
        await RunCleanupLoopAsync(stoppingToken);
    }

    private async Task InitializeServerKeysAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing server security keys...");

            var serverKeyInfo = await _credentialService.InitializeServerKeysAsync(cancellationToken);

            _logger.LogInformation(
                "Server security initialized. ServerId: {ServerId}, Algorithm: {Algorithm}",
                serverKeyInfo.ServerId,
                serverKeyInfo.Algorithm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize server security keys");
            throw; // Critical failure - cannot proceed without keys
        }
    }

    private async Task RunCleanupLoopAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = TimeSpan.FromMinutes(_options.BootstrapToken.CleanupIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, stoppingToken);

                // Cleanup expired bootstrap tokens
                var tokensCleaned = await _bootstrapTokenService.CleanupExpiredAsync(stoppingToken);
                if (tokensCleaned > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired bootstrap tokens", tokensCleaned);
                }

                // Cleanup expired enrollments
                var enrollmentsCleaned = await _enrollmentService.CleanupExpiredAsync(stoppingToken);
                if (enrollmentsCleaned > 0)
                {
                    _logger.LogDebug("Marked {Count} enrollments as expired", enrollmentsCleaned);
                }

                // Check for expiring certificates and log warnings
                await CheckExpiringCertificatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security cleanup cycle");
            }
        }
    }

    private async Task CheckExpiringCertificatesAsync(CancellationToken cancellationToken)
    {
        var expiringCerts = await _credentialService.GetExpiringCertificatesAsync(
            _options.Certificate.RenewalThresholdDays,
            cancellationToken);

        foreach (var cert in expiringCerts)
        {
            var daysUntilExpiry = (cert.ExpiresAt - DateTimeOffset.UtcNow).TotalDays;

            if (daysUntilExpiry <= 7)
            {
                _logger.LogWarning(
                    "Certificate expiring soon! NodeId: {NodeId}, ExpiresAt: {ExpiresAt}, DaysRemaining: {Days}",
                    cert.NodeId,
                    cert.ExpiresAt,
                    Math.Round(daysUntilExpiry, 1));
            }
            else
            {
                _logger.LogInformation(
                    "Certificate approaching expiry. NodeId: {NodeId}, ExpiresAt: {ExpiresAt}, DaysRemaining: {Days}",
                    cert.NodeId,
                    cert.ExpiresAt,
                    Math.Round(daysUntilExpiry, 1));
            }

            // Auto-renew if enabled
            if (_options.Certificate.AutoRenewal && daysUntilExpiry <= _options.Certificate.RenewalThresholdDays)
            {
                try
                {
                    var renewed = await _credentialService.RenewCertificateAsync(
                        cert.NodeId,
                        _options.Certificate.ValidityDays,
                        cancellationToken);

                    _logger.LogInformation(
                        "Certificate auto-renewed. NodeId: {NodeId}, NewExpiresAt: {ExpiresAt}",
                        cert.NodeId,
                        renewed.ExpiresAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-renew certificate for node: {NodeId}", cert.NodeId);
                }
            }
        }
    }
}
