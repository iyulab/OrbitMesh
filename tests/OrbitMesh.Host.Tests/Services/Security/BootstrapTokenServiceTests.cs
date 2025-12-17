using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Tests.Services.Security;

public class BootstrapTokenServiceTests
{
    private readonly InMemoryBootstrapTokenService _service;

    public BootstrapTokenServiceTests()
    {
        _service = new InMemoryBootstrapTokenService(
            NullLogger<InMemoryBootstrapTokenService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnValidToken()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest
        {
            Description = "Test token"
        };

        // Act
        var token = await _service.CreateAsync(request);

        // Assert
        token.Should().NotBeNull();
        token.Id.Should().NotBeNullOrEmpty();
        token.Token.Should().NotBeNullOrEmpty();
        token.Description.Should().Be("Test token");
        token.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithOptions_ShouldApplyOptions()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest
        {
            Description = "Auto-approve token",
            ExpirationHours = 2,
            PreApprovedCapabilities = ["cap1", "cap2"],
            AutoApprove = true
        };

        // Act
        var token = await _service.CreateAsync(request);

        // Assert
        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(2), TimeSpan.FromSeconds(5));
        token.PreApprovedCapabilities.Should().BeEquivalentTo(["cap1", "cap2"]);
        token.AutoApprove.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithValidToken_ShouldReturnValidationAndConsume()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest { Description = "Test token" };
        var token = await _service.CreateAsync(request);

        // Act
        var result = await _service.ValidateAndConsumeAsync(token.Token!);

        // Assert
        result.Should().NotBeNull();
        result!.TokenId.Should().Be(token.Id);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid-token-value";

        // Act
        var result = await _service.ValidateAndConsumeAsync(invalidToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_CalledTwice_ShouldReturnNullSecondTime()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest { Description = "Single-use token" };
        var token = await _service.CreateAsync(request);

        // Act
        var firstResult = await _service.ValidateAndConsumeAsync(token.Token!);
        var secondResult = await _service.ValidateAndConsumeAsync(token.Token!);

        // Assert
        firstResult.Should().NotBeNull();
        secondResult.Should().BeNull(); // Already consumed
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithExpiredToken_ShouldReturnNull()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest
        {
            Description = "Short-lived token",
            ExpirationHours = 0 // Immediately expired
        };
        var token = await _service.CreateAsync(request);

        // Act
        var result = await _service.ValidateAndConsumeAsync(token.Token!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_ShouldRemoveToken()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest { Description = "Revocable token" };
        var token = await _service.CreateAsync(request);

        // Act
        var revoked = await _service.RevokeAsync(token.Id);

        // Assert
        revoked.Should().BeTrue();
        var result = await _service.ValidateAndConsumeAsync(token.Token!);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTokensAsync_ShouldReturnOnlyActiveTokens()
    {
        // Arrange
        var request1 = new CreateBootstrapTokenRequest { Description = "Token 1" };
        var request2 = new CreateBootstrapTokenRequest { Description = "Token 2" };

        var token1 = await _service.CreateAsync(request1);
        var token2 = await _service.CreateAsync(request2);

        // Consume token1
        await _service.ValidateAndConsumeAsync(token1.Token!);

        // Act
        var activeTokens = await _service.GetActiveTokensAsync();

        // Assert
        activeTokens.Should().ContainSingle();
        activeTokens[0].Id.Should().Be(token2.Id);
    }

    [Fact]
    public async Task CleanupExpiredAsync_ShouldRemoveExpiredAndConsumedTokens()
    {
        // Arrange
        var request = new CreateBootstrapTokenRequest
        {
            Description = "Token to cleanup",
            ExpirationHours = 0 // Immediately expired
        };
        await _service.CreateAsync(request);

        // Act
        var cleaned = await _service.CleanupExpiredAsync();

        // Assert
        cleaned.Should().BeGreaterThanOrEqualTo(1);
    }
}
