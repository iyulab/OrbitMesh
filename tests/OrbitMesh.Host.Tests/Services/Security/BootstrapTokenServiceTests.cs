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
    public async Task GetTokenAsync_ShouldReturnToken()
    {
        // Act
        var token = await _service.GetTokenAsync();

        // Assert
        token.Should().NotBeNull();
        token.Id.Should().NotBeNullOrEmpty();
        token.Token.Should().BeNull(); // Token value not exposed via GetToken
        token.IsEnabled.Should().BeTrue();
        token.AutoApprove.Should().BeTrue();
    }

    [Fact]
    public async Task RegenerateAsync_ShouldReturnNewTokenValue()
    {
        // Act
        var token = await _service.RegenerateAsync();

        // Assert
        token.Should().NotBeNull();
        token.Id.Should().NotBeNullOrEmpty();
        token.Token.Should().NotBeNullOrEmpty(); // Token value only exposed on regenerate
        token.Token.Should().StartWith("orbit_boot_");
        token.LastRegeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegenerateAsync_ShouldInvalidateOldToken()
    {
        // Arrange
        var oldToken = await _service.RegenerateAsync();
        var oldTokenValue = oldToken.Token!;

        // Act
        var newToken = await _service.RegenerateAsync();

        // Assert
        newToken.Token.Should().NotBe(oldTokenValue);

        // Old token should be invalid
        var oldValidation = await _service.ValidateAsync(oldTokenValue);
        oldValidation.Should().BeNull();

        // New token should be valid
        var newValidation = await _service.ValidateAsync(newToken.Token!);
        newValidation.Should().NotBeNull();
    }

    [Fact]
    public async Task SetEnabledAsync_ShouldUpdateEnabledState()
    {
        // Arrange - ensure token exists
        await _service.GetTokenAsync();

        // Act
        await _service.SetEnabledAsync(false);
        var token = await _service.GetTokenAsync();

        // Assert
        token.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetAutoApproveAsync_ShouldUpdateAutoApproveState()
    {
        // Arrange - ensure token exists
        await _service.GetTokenAsync();

        // Act
        await _service.SetAutoApproveAsync(false);
        var token = await _service.GetTokenAsync();

        // Assert
        token.AutoApprove.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithValidToken_ShouldReturnValidation()
    {
        // Arrange
        var token = await _service.RegenerateAsync();

        // Act
        var result = await _service.ValidateAsync(token.Token!);

        // Assert
        result.Should().NotBeNull();
        result!.TokenId.Should().Be(token.Id);
        result.AutoApprove.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid-token-value";

        // Act
        var result = await _service.ValidateAsync(invalidToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenDisabled_ShouldReturnNull()
    {
        // Arrange
        var token = await _service.RegenerateAsync();
        await _service.SetEnabledAsync(false);

        // Act
        var result = await _service.ValidateAsync(token.Token!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnAutoApproveState()
    {
        // Arrange
        var token = await _service.RegenerateAsync();
        await _service.SetAutoApproveAsync(false);

        // Act
        var result = await _service.ValidateAsync(token.Token!);

        // Assert
        result.Should().NotBeNull();
        result!.AutoApprove.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_CanBeCalledMultipleTimes()
    {
        // Arrange - token is reusable, not consumed
        var token = await _service.RegenerateAsync();

        // Act
        var firstResult = await _service.ValidateAsync(token.Token!);
        var secondResult = await _service.ValidateAsync(token.Token!);

        // Assert
        firstResult.Should().NotBeNull();
        secondResult.Should().NotBeNull(); // Token is not consumed, can be reused
    }
}
