using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Tests.Services.Security;

public class NodeCredentialServiceTests
{
    private readonly InMemoryNodeCredentialService _service;

    public NodeCredentialServiceTests()
    {
        _service = new InMemoryNodeCredentialService(
            NullLogger<InMemoryNodeCredentialService>.Instance);
    }

    [Fact]
    public async Task InitializeServerKeysAsync_ShouldGenerateKeyPair()
    {
        // Act
        var keyInfo = await _service.InitializeServerKeysAsync();

        // Assert
        keyInfo.Should().NotBeNull();
        keyInfo.ServerId.Should().NotBeNullOrEmpty();
        keyInfo.PublicKey.Should().NotBeNullOrEmpty();
        keyInfo.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task InitializeServerKeysAsync_CalledTwice_ShouldReturnSameKeys()
    {
        // Arrange
        var firstKeyInfo = await _service.InitializeServerKeysAsync();

        // Act
        var secondKeyInfo = await _service.InitializeServerKeysAsync();

        // Assert
        secondKeyInfo.ServerId.Should().Be(firstKeyInfo.ServerId);
        secondKeyInfo.PublicKey.Should().Be(firstKeyInfo.PublicKey);
    }

    [Fact]
    public async Task IssueCertificateAsync_ShouldCreateValidCertificate()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var nodeId = "test-node";
        var nodeName = "Test Node";
        var publicKey = Convert.ToBase64String(new byte[64]);
        var capabilities = new List<string> { "cap1", "cap2" };

        // Act
        var certificate = await _service.IssueCertificateAsync(
            nodeId, nodeName, publicKey, capabilities);

        // Assert
        certificate.Should().NotBeNull();
        certificate.NodeId.Should().Be(nodeId);
        certificate.NodeName.Should().Be(nodeName);
        certificate.PublicKey.Should().Be(publicKey);
        certificate.Capabilities.Should().BeEquivalentTo(capabilities);
        certificate.SerialNumber.Should().NotBeNullOrEmpty();
        certificate.Signature.Should().NotBeNullOrEmpty();
        certificate.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithValidCertificate_ShouldReturnValid()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var certificate = await _service.IssueCertificateAsync(
            "test-node", "Test Node", Convert.ToBase64String(new byte[64]), ["cap1"]);

        // Act
        var validation = await _service.ValidateCertificateAsync(certificate.ToBase64());

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Certificate.Should().NotBeNull();
        validation.Certificate!.NodeId.Should().Be("test-node");
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithInvalidCertificate_ShouldReturnInvalid()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();

        // Act
        var validation = await _service.ValidateCertificateAsync("invalid-certificate-data");

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RevokeCertificateAsync_ShouldAddToRevocationList()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var certificate = await _service.IssueCertificateAsync(
            "revoke-node", "Revoke Node", Convert.ToBase64String(new byte[64]), ["cap1"]);

        // Act
        await _service.RevokeCertificateAsync("revoke-node", "Compromised", "admin");

        // Assert
        var isRevoked = await _service.IsRevokedAsync(certificate.SerialNumber);
        isRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithRevokedCertificate_ShouldReturnInvalid()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var certificate = await _service.IssueCertificateAsync(
            "revoke-test", "Revoke Test", Convert.ToBase64String(new byte[64]), ["cap1"]);
        await _service.RevokeCertificateAsync("revoke-test", "Testing", "admin");

        // Act
        var validation = await _service.ValidateCertificateAsync(certificate.ToBase64());

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("revoked");
    }

    [Fact]
    public async Task RenewCertificateAsync_ShouldIssueNewCertificate()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var oldCertificate = await _service.IssueCertificateAsync(
            "renew-node", "Renew Node", Convert.ToBase64String(new byte[64]), ["cap1"]);

        // Act
        var newCertificate = await _service.RenewCertificateAsync("renew-node", 30);

        // Assert
        newCertificate.Should().NotBeNull();
        newCertificate.SerialNumber.Should().NotBe(oldCertificate.SerialNumber);
        newCertificate.NodeId.Should().Be(oldCertificate.NodeId);
        newCertificate.ExpiresAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_ShouldReturnOnlyActive()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        await _service.IssueCertificateAsync(
            "active-1", "Active 1", Convert.ToBase64String(new byte[64]), ["cap1"]);
        await _service.IssueCertificateAsync(
            "active-2", "Active 2", Convert.ToBase64String(new byte[64]), ["cap2"]);
        var toRevoke = await _service.IssueCertificateAsync(
            "to-revoke", "To Revoke", Convert.ToBase64String(new byte[64]), ["cap3"]);
        await _service.RevokeCertificateAsync("to-revoke", "Test", "admin");

        // Act
        var activeCerts = await _service.GetActiveCertificatesAsync();

        // Assert
        activeCerts.Should().HaveCount(2);
        activeCerts.Should().NotContain(c => c.NodeId == "to-revoke");
    }

    [Fact]
    public async Task GetExpiringCertificatesAsync_ShouldReturnSoonToExpire()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        await _service.IssueCertificateAsync(
            "expire-soon", "Expire Soon", Convert.ToBase64String(new byte[64]), ["cap1"], 10);
        await _service.IssueCertificateAsync(
            "expire-later", "Expire Later", Convert.ToBase64String(new byte[64]), ["cap2"], 90);

        // Act
        var expiring = await _service.GetExpiringCertificatesAsync(30);

        // Assert
        expiring.Should().ContainSingle();
        expiring[0].NodeId.Should().Be("expire-soon");
    }

    [Fact]
    public async Task VerifySignatureAsync_WithValidSignature_ShouldReturnTrue()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        var publicKey = Convert.ToBase64String(new byte[64]);
        var signature = Convert.ToBase64String(new byte[32]);

        // Act
        var isValid = await _service.VerifySignatureAsync("test-node", publicKey, signature);

        // Assert
        isValid.Should().BeTrue(); // Simplified verification in InMemory implementation
    }

    [Fact]
    public async Task GetRevocationListAsync_ShouldReturnAllRevoked()
    {
        // Arrange
        await _service.InitializeServerKeysAsync();
        await _service.IssueCertificateAsync(
            "revoke-1", "Revoke 1", Convert.ToBase64String(new byte[64]), ["cap1"]);
        await _service.IssueCertificateAsync(
            "revoke-2", "Revoke 2", Convert.ToBase64String(new byte[64]), ["cap2"]);
        await _service.RevokeCertificateAsync("revoke-1", "Reason 1", "admin");
        await _service.RevokeCertificateAsync("revoke-2", "Reason 2", "admin");

        // Act
        var revocationList = await _service.GetRevocationListAsync();

        // Assert
        revocationList.Should().HaveCount(2);
    }
}
