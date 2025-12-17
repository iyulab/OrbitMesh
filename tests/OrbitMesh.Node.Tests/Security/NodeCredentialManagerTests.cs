using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Node.Security;

namespace OrbitMesh.Node.Tests.Security;

public sealed class NodeCredentialManagerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly NodeCredentialManager _manager;

    public NodeCredentialManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"orbitmesh-test-{Guid.NewGuid():N}.json");
        _manager = new NodeCredentialManager(_tempPath, NullLogger<NodeCredentialManager>.Instance);
    }

    public void Dispose()
    {
        _manager.Dispose();
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InitializeAsync_ShouldGenerateKeyPair()
    {
        // Act
        await _manager.InitializeAsync("test-node");

        // Assert
        _manager.PublicKey.Should().NotBeNullOrEmpty();
        _manager.Credentials.Should().NotBeNull();
        _manager.Credentials!.NodeId.Should().Be("test-node");
        _manager.Credentials.PrivateKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPersistCredentials()
    {
        // Arrange
        await _manager.InitializeAsync("persist-node");
        var publicKey = _manager.PublicKey;

        // Act - Create new manager with same path
        using var newManager = new NodeCredentialManager(_tempPath, NullLogger<NodeCredentialManager>.Instance);
        await newManager.InitializeAsync("persist-node");

        // Assert
        newManager.PublicKey.Should().Be(publicKey);
    }

    [Fact]
    public async Task InitializeAsync_WithDifferentNodeId_ShouldGenerateNewKeyPair()
    {
        // Arrange
        await _manager.InitializeAsync("node-1");
        var firstPublicKey = _manager.PublicKey;

        // Act - Initialize with different node ID
        using var newManager = new NodeCredentialManager(_tempPath, NullLogger<NodeCredentialManager>.Instance);
        await newManager.InitializeAsync("node-2");

        // Assert
        newManager.PublicKey.Should().NotBe(firstPublicKey);
    }

    [Fact]
    public async Task Sign_ShouldProduceValidSignature()
    {
        // Arrange
        await _manager.InitializeAsync("sign-node");

        // Act
        var signature = _manager.Sign("test-data");

        // Assert
        signature.Should().NotBeNullOrEmpty();
        // Verify it's valid Base64
        var bytes = Convert.FromBase64String(signature);
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEnrollmentSignature_ShouldSignNodeId()
    {
        // Arrange
        await _manager.InitializeAsync("enroll-node");

        // Act
        var signature = _manager.CreateEnrollmentSignature("enroll-node");

        // Assert
        signature.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StoreCertificateAsync_ShouldSetIsEnrolled()
    {
        // Arrange
        await _manager.InitializeAsync("cert-node");
        _manager.IsEnrolled.Should().BeFalse();

        // Act
        await _manager.StoreCertificateAsync(
            Convert.ToBase64String(new byte[100]),
            Convert.ToBase64String(new byte[64]));

        // Assert
        _manager.IsEnrolled.Should().BeTrue();
        _manager.Credentials!.Certificate.Should().NotBeNullOrEmpty();
        _manager.Credentials.ServerPublicKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StoreEnrollmentIdAsync_ShouldStorePendingId()
    {
        // Arrange
        await _manager.InitializeAsync("pending-node");

        // Act
        await _manager.StoreEnrollmentIdAsync("enrollment-123");

        // Assert
        _manager.Credentials!.PendingEnrollmentId.Should().Be("enrollment-123");
    }

    [Fact]
    public async Task ClearCredentialsAsync_ShouldResetState()
    {
        // Arrange
        await _manager.InitializeAsync("clear-node");
        await _manager.StoreCertificateAsync(
            Convert.ToBase64String(new byte[100]),
            Convert.ToBase64String(new byte[64]));

        // Act
        await _manager.ClearCredentialsAsync();

        // Assert
        _manager.Credentials.Should().BeNull();
        _manager.PublicKey.Should().BeNull();
        _manager.IsEnrolled.Should().BeFalse();
        File.Exists(_tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task Sign_BeforeInitialize_ShouldThrow()
    {
        // Act & Assert
        var act = () => _manager.Sign("test");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task StoreCertificateAsync_BeforeInitialize_ShouldThrow()
    {
        // Act & Assert
        var act = async () => await _manager.StoreCertificateAsync("cert", "key");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }
}
