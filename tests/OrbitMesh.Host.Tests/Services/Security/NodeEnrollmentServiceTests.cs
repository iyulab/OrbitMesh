using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Tests.Services.Security;

public class NodeEnrollmentServiceTests
{
    private readonly InMemoryNodeEnrollmentService _enrollmentService;
    private readonly InMemoryNodeCredentialService _credentialService;

    public NodeEnrollmentServiceTests()
    {
        _credentialService = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);

        var options = Options.Create(new SecurityOptions());

        _enrollmentService = new InMemoryNodeEnrollmentService(
            _credentialService,
            options,
            NullLogger<InMemoryNodeEnrollmentService>.Instance);
    }

    [Fact]
    public async Task RequestEnrollmentAsync_ShouldCreatePendingEnrollment()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "node-1",
            NodeName = "Test Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32]),
            RequestedCapabilities = ["cap1", "cap2"]
        };

        // Act
        var result = await _enrollmentService.RequestEnrollmentAsync(request, "token-1");

        // Assert
        result.Success.Should().BeTrue();
        result.Status.Should().Be(EnrollmentStatus.Pending);
        result.EnrollmentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestEnrollmentAsync_WithAutoApproval_ShouldImmediatelyApprove()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        // Create service with auto-approval enabled
        var options = Options.Create(new SecurityOptions
        {
            Enrollment = new EnrollmentOptions
            {
                AutoApprove = true,
                AutoApprovePatterns = ["*"]
            }
        });
        var autoApproveService = new InMemoryNodeEnrollmentService(
            _credentialService,
            options,
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        var request = new EnrollmentRequest
        {
            NodeId = "node-auto",
            NodeName = "Auto-approved Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32]),
            RequestedCapabilities = ["cap1"]
        };

        // Act
        var result = await autoApproveService.RequestEnrollmentAsync(request, "token-auto");

        // Assert
        result.Success.Should().BeTrue();
        result.Status.Should().Be(EnrollmentStatus.Approved);
        result.Certificate.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestEnrollmentAsync_WithBlockedNode_ShouldRejectEnrollment()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "blocked-node",
            NodeName = "Blocked Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32])
        };

        // First enrollment - then reject and block
        var firstResult = await _enrollmentService.RequestEnrollmentAsync(request, "token-1");
        await _enrollmentService.RejectEnrollmentAsync(
            firstResult.EnrollmentId!, "Test rejection", "admin", blockNode: true);

        // Act - Second attempt should be blocked
        var result = await _enrollmentService.RequestEnrollmentAsync(request, "token-2");

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(EnrollmentStatus.Blocked);
    }

    [Fact]
    public async Task ApproveEnrollmentAsync_ShouldIssueCertificate()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "node-approve",
            NodeName = "Node to Approve",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32]),
            RequestedCapabilities = ["cap1"]
        };

        var enrollResult = await _enrollmentService.RequestEnrollmentAsync(request, "token-approve");
        var options = new ApprovalOptions
        {
            GrantedCapabilities = ["cap1", "cap2"],
            CertificateValidityDays = 30
        };

        // Act
        var certificate = await _enrollmentService.ApproveEnrollmentAsync(
            enrollResult.EnrollmentId!, options, "admin");

        // Assert
        certificate.Should().NotBeNull();
        certificate.NodeId.Should().Be("node-approve");
        certificate.Capabilities.Should().BeEquivalentTo(["cap1", "cap2"]);
    }

    [Fact]
    public async Task GetEnrollmentStatusAsync_ShouldReturnCurrentStatus()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "node-status",
            NodeName = "Status Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32])
        };

        var enrollResult = await _enrollmentService.RequestEnrollmentAsync(request, "token-status");

        // Act
        var status = await _enrollmentService.GetEnrollmentStatusAsync(enrollResult.EnrollmentId!);

        // Assert
        status.Status.Should().Be(EnrollmentStatus.Pending);
    }

    [Fact]
    public async Task RejectEnrollmentAsync_ShouldUpdateStatus()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "node-reject",
            NodeName = "Rejected Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32])
        };

        var enrollResult = await _enrollmentService.RequestEnrollmentAsync(request, "token-reject");

        // Act
        await _enrollmentService.RejectEnrollmentAsync(
            enrollResult.EnrollmentId!, "Policy violation", "admin");

        // Assert
        var status = await _enrollmentService.GetEnrollmentStatusAsync(enrollResult.EnrollmentId!);
        status.Status.Should().Be(EnrollmentStatus.Rejected);
    }

    [Fact]
    public async Task GetPendingEnrollmentsAsync_ShouldReturnAllPending()
    {
        // Arrange
        await _credentialService.InitializeServerKeysAsync();

        for (int i = 0; i < 3; i++)
        {
            var request = new EnrollmentRequest
            {
                NodeId = $"node-{i}",
                NodeName = $"Node {i}",
                PublicKey = Convert.ToBase64String(new byte[64]),
                Signature = Convert.ToBase64String(new byte[32])
            };
            await _enrollmentService.RequestEnrollmentAsync(request, $"token-{i}");
        }

        // Act
        var pending = await _enrollmentService.GetPendingEnrollmentsAsync();

        // Assert
        pending.Should().HaveCount(3);
    }

    [Fact]
    public async Task CleanupExpiredAsync_ShouldMarkExpiredEnrollments()
    {
        // Arrange - Create service with very short expiration
        var shortOptions = Options.Create(new SecurityOptions
        {
            Enrollment = new EnrollmentOptions
            {
                PendingExpirationDays = 0 // Immediate expiration
            }
        });

        var shortExpirationService = new InMemoryNodeEnrollmentService(
            _credentialService,
            shortOptions,
            NullLogger<InMemoryNodeEnrollmentService>.Instance);

        await _credentialService.InitializeServerKeysAsync();

        var request = new EnrollmentRequest
        {
            NodeId = "node-expire",
            NodeName = "Expiring Node",
            PublicKey = Convert.ToBase64String(new byte[64]),
            Signature = Convert.ToBase64String(new byte[32])
        };

        await shortExpirationService.RequestEnrollmentAsync(request, "token-expire");

        // Act
        var cleanedUp = await shortExpirationService.CleanupExpiredAsync();

        // Assert
        cleanedUp.Should().BeGreaterThanOrEqualTo(1);
    }
}
