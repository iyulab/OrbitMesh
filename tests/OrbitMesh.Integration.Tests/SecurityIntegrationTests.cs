using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Node.Security;
using HostEnrollmentStatus = OrbitMesh.Host.Services.Security.EnrollmentStatus;

namespace OrbitMesh.Integration.Tests;

/// <summary>
/// Integration tests for the complete security enrollment workflow.
/// </summary>
public class SecurityIntegrationTests
{
    [Fact]
    public void Security_Types_Are_Available()
    {
        // Server-side types
        typeof(IBootstrapTokenService).Should().NotBeNull();
        typeof(INodeCredentialService).Should().NotBeNull();
        typeof(INodeEnrollmentService).Should().NotBeNull();
        typeof(InMemoryBootstrapTokenService).Should().NotBeNull();
        typeof(InMemoryNodeCredentialService).Should().NotBeNull();
        typeof(InMemoryNodeEnrollmentService).Should().NotBeNull();

        // Node-side types
        typeof(NodeCredentialManager).Should().NotBeNull();
        typeof(EnrollmentService).Should().NotBeNull();
        typeof(EnrollmentOutcome).Should().NotBeNull();
    }

    [Fact]
    public async Task FullEnrollmentWorkflow_ManualApproval_ShouldSucceed()
    {
        // Arrange - Server side setup
        var credentialService = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);
        var enrollmentService = new InMemoryNodeEnrollmentService(
            credentialService,
            Options.Create(new SecurityOptions()),
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        await credentialService.InitializeServerKeysAsync();

        // Step 1: Admin regenerates bootstrap token to get the value
        var token = await bootstrapService.RegenerateAsync();
        token.Token.Should().NotBeNullOrEmpty();

        // Step 2: Node generates key pair and requests enrollment
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.json");
        try
        {
            using var nodeCredManager = new NodeCredentialManager(tempPath,
                NullLogger<NodeCredentialManager>.Instance);
            await nodeCredManager.InitializeAsync("test-node");

            var signature = nodeCredManager.CreateEnrollmentSignature("test-node");

            var enrollRequest = new EnrollmentRequest
            {
                NodeId = "test-node",
                NodeName = "Test Node",
                PublicKey = nodeCredManager.PublicKey!,
                Signature = signature,
                RequestedCapabilities = ["execute-jobs"]
            };

            // Step 3: Server receives enrollment request
            var enrollResult = await enrollmentService.RequestEnrollmentAsync(enrollRequest, token.Id);
            enrollResult.Success.Should().BeTrue();
            enrollResult.Status.Should().Be(HostEnrollmentStatus.Pending);

            // Step 4: Admin reviews and approves
            var pendingEnrollments = await enrollmentService.GetPendingEnrollmentsAsync();
            pendingEnrollments.Should().ContainSingle();

            var certificate = await enrollmentService.ApproveEnrollmentAsync(
                enrollResult.EnrollmentId!,
                new ApprovalOptions { GrantedCapabilities = ["execute-jobs"] },
                "admin");

            certificate.Should().NotBeNull();
            certificate.NodeId.Should().Be("test-node");

            // Step 5: Node stores certificate
            var serverKeyInfo = await credentialService.GetServerKeyInfoAsync();
            await nodeCredManager.StoreCertificateAsync(
                certificate.ToBase64(),
                serverKeyInfo.PublicKey);

            nodeCredManager.IsEnrolled.Should().BeTrue();

            // Step 6: Verify certificate is valid
            var validation = await credentialService.ValidateCertificateAsync(
                nodeCredManager.Credentials!.Certificate!);
            validation.IsValid.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FullEnrollmentWorkflow_AutoApproval_ShouldSucceed()
    {
        // Arrange - Server side setup with auto-approval enabled
        var credentialService = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);

        // Enable auto-approval in security options
        var securityOptions = new SecurityOptions
        {
            Enrollment = new EnrollmentOptions
            {
                AutoApprove = true,
                AutoApprovePatterns = ["*"]
            }
        };

        var enrollmentService = new InMemoryNodeEnrollmentService(
            credentialService,
            Options.Create(securityOptions),
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        await credentialService.InitializeServerKeysAsync();

        // Step 1: Admin regenerates bootstrap token with auto-approval enabled
        var token = await bootstrapService.RegenerateAsync();
        // Auto-approve is enabled by default

        // Step 2: Node generates key pair and requests enrollment
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-auto-{Guid.NewGuid():N}.json");
        try
        {
            using var nodeCredManager = new NodeCredentialManager(tempPath,
                NullLogger<NodeCredentialManager>.Instance);
            await nodeCredManager.InitializeAsync("auto-node");

            var signature = nodeCredManager.CreateEnrollmentSignature("auto-node");

            var enrollRequest = new EnrollmentRequest
            {
                NodeId = "auto-node",
                NodeName = "Auto-Approved Node",
                PublicKey = nodeCredManager.PublicKey!,
                Signature = signature,
                RequestedCapabilities = ["execute-jobs"]
            };

            // Step 3: Server receives and auto-approves enrollment
            var enrollResult = await enrollmentService.RequestEnrollmentAsync(enrollRequest, token.Id);

            // Should be immediately approved due to auto-approval pattern "*"
            enrollResult.Success.Should().BeTrue();
            enrollResult.Status.Should().Be(HostEnrollmentStatus.Approved);
            enrollResult.Certificate.Should().NotBeNull();

            // Step 4: Node stores certificate
            var serverKeyInfo = await credentialService.GetServerKeyInfoAsync();
            await nodeCredManager.StoreCertificateAsync(
                enrollResult.Certificate!.ToBase64(),
                serverKeyInfo.PublicKey);

            nodeCredManager.IsEnrolled.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CertificateRevocation_ShouldInvalidateAccess()
    {
        // Arrange
        var credentialService = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);

        var securityOptions = new SecurityOptions
        {
            Enrollment = new EnrollmentOptions
            {
                AutoApprove = true,
                AutoApprovePatterns = ["*"]
            }
        };

        var enrollmentService = new InMemoryNodeEnrollmentService(
            credentialService,
            Options.Create(securityOptions),
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        await credentialService.InitializeServerKeysAsync();

        // Create auto-approval token
        var token = await bootstrapService.RegenerateAsync();

        var tempPath = Path.Combine(Path.GetTempPath(), $"test-revoke-{Guid.NewGuid():N}.json");
        try
        {
            using var nodeCredManager = new NodeCredentialManager(tempPath,
                NullLogger<NodeCredentialManager>.Instance);
            await nodeCredManager.InitializeAsync("revoke-test-node");

            var enrollResult = await enrollmentService.RequestEnrollmentAsync(
                new EnrollmentRequest
                {
                    NodeId = "revoke-test-node",
                    NodeName = "Revoke Test",
                    PublicKey = nodeCredManager.PublicKey!,
                    Signature = nodeCredManager.CreateEnrollmentSignature("revoke-test-node"),
                    RequestedCapabilities = ["test"]
                },
                token.Id);

            // Verify certificate is valid
            var validation1 = await credentialService.ValidateCertificateAsync(
                enrollResult.Certificate!.ToBase64());
            validation1.IsValid.Should().BeTrue();

            // Revoke certificate
            await credentialService.RevokeCertificateAsync(
                "revoke-test-node", "Compromised", "security-admin");

            // Verify certificate is now invalid
            var validation2 = await credentialService.ValidateCertificateAsync(
                enrollResult.Certificate!.ToBase64());
            validation2.IsValid.Should().BeFalse();
            validation2.Error.Should().Contain("revoked");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task BootstrapToken_Reusable_ShouldNotBeConsumed()
    {
        // Arrange
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);

        var token = await bootstrapService.RegenerateAsync();

        // Act - Validate the token multiple times (token is reusable, not consumed)
        var validation1 = await bootstrapService.ValidateAsync(token.Token!);
        validation1.Should().NotBeNull();

        var validation2 = await bootstrapService.ValidateAsync(token.Token!);
        validation2.Should().NotBeNull();

        // Assert - Both validations should succeed (token is reusable)
        validation1!.TokenId.Should().Be(token.Id);
        validation2!.TokenId.Should().Be(token.Id);
    }

    [Fact]
    public async Task BootstrapToken_WhenDisabled_ShouldFailValidation()
    {
        // Arrange
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);

        var token = await bootstrapService.RegenerateAsync();

        // Act - Disable the token
        await bootstrapService.SetEnabledAsync(false);

        // Assert - Validation should fail when disabled
        var validation = await bootstrapService.ValidateAsync(token.Token!);
        validation.Should().BeNull();
    }

    [Fact]
    public async Task EnrollmentRejection_ShouldBlockFutureAttempts()
    {
        // Arrange
        var credentialService = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);
        var bootstrapService = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);
        var enrollmentService = new InMemoryNodeEnrollmentService(
            credentialService,
            Options.Create(new SecurityOptions()),
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        await credentialService.InitializeServerKeysAsync();

        // Get bootstrap token
        var token = await bootstrapService.RegenerateAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "block-test-node",
            NodeName = "Block Test",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32]),
            RequestedCapabilities = ["suspicious-cap"]
        };

        var result1 = await enrollmentService.RequestEnrollmentAsync(request, token.Id);

        // Admin rejects and blocks the node
        await enrollmentService.RejectEnrollmentAsync(
            result1.EnrollmentId!,
            "Suspicious activity",
            "admin",
            blockNode: true);

        // Second enrollment attempt with same token (token is reusable)
        var result2 = await enrollmentService.RequestEnrollmentAsync(request, token.Id);

        // Assert - Should be blocked
        result2.Success.Should().BeFalse();
        result2.Status.Should().Be(HostEnrollmentStatus.Blocked);
    }
}
