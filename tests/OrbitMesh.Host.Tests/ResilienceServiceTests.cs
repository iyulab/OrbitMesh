using OrbitMesh.Host.Services;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrbitMesh.Host.Tests;

public class ResilienceServiceTests
{
    #region Interface Tests

    [Fact]
    public void IResilienceService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IResilienceService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ResilienceService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(ResilienceService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IResilienceService));
    }

    [Fact]
    public void ResilienceOptions_Record_Exists()
    {
        // Arrange & Act
        var optionsType = typeof(ResilienceOptions);

        // Assert
        optionsType.Should().NotBeNull();
    }

    #endregion

    #region Retry Policy Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_Succeeds_On_First_Attempt()
    {
        // Arrange
        var service = CreateService();
        var attempts = 0;

        // Act
        var result = await service.ExecuteWithRetryAsync(
            "test-operation",
            async ct =>
            {
                attempts++;
                await Task.CompletedTask;
                return "success";
            });

        // Assert
        result.Should().Be("success");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_Retries_On_Transient_Failure()
    {
        // Arrange
        var options = new ResilienceOptions { MaxRetryAttempts = 3, InitialRetryDelayMs = 10 };
        var service = CreateService(options);
        var attempts = 0;

        // Act
        var result = await service.ExecuteWithRetryAsync(
            "retry-operation",
            async ct =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("Transient failure");
                }
                await Task.CompletedTask;
                return "success after retries";
            });

        // Assert
        result.Should().Be("success after retries");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_Throws_After_MaxRetries()
    {
        // Arrange
        var options = new ResilienceOptions { MaxRetryAttempts = 2, InitialRetryDelayMs = 10 };
        var service = CreateService(options);
        var attempts = 0;

        // Act
        var act = async () => await service.ExecuteWithRetryAsync<string>(
            "failing-operation",
            async ct =>
            {
                attempts++;
                await Task.CompletedTask;
                throw new InvalidOperationException("Persistent failure");
            });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Persistent failure");
        attempts.Should().Be(3); // Initial + 2 retries
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task CircuitBreaker_Opens_After_Consecutive_Failures()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            CircuitBreakerFailureThreshold = 0.5, // 50% failure ratio (must be 0-1)
            CircuitBreakerSamplingDurationMs = 5000,
            CircuitBreakerMinimumThroughput = 2, // Minimum required by Polly
            CircuitBreakerBreakDurationMs = 1000,
            MaxRetryAttempts = 0 // Disable retries for this test
        };
        var service = CreateService(options);
        var attempts = 0;

        // Act - Generate failures to open circuit (need at least MinimumThroughput calls)
        for (int i = 0; i < 4; i++)
        {
            try
            {
                await service.ExecuteWithCircuitBreakerAsync<string>(
                    "circuit-test",
                    async ct =>
                    {
                        attempts++;
                        await Task.CompletedTask;
                        throw new InvalidOperationException("Failure");
                    });
            }
            catch (Exception)
            {
                // Expected
            }
        }

        // Assert - Circuit should be open
        var isOpen = service.IsCircuitOpen("circuit-test");
        isOpen.Should().BeTrue();
    }

    [Fact]
    public async Task CircuitBreaker_RejectsRequests_When_Open()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            CircuitBreakerFailureThreshold = 0.5, // 50% failure ratio
            CircuitBreakerSamplingDurationMs = 5000,
            CircuitBreakerMinimumThroughput = 2, // Minimum required by Polly
            CircuitBreakerBreakDurationMs = 30000,
            MaxRetryAttempts = 0
        };
        var service = CreateService(options);

        // Open the circuit (need at least MinimumThroughput failures)
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await service.ExecuteWithCircuitBreakerAsync<string>(
                    "reject-test",
                    async ct =>
                    {
                        await Task.CompletedTask;
                        throw new InvalidOperationException("Open circuit");
                    });
            }
            catch { }
        }

        // Act - Try to execute when circuit is open
        var act = async () => await service.ExecuteWithCircuitBreakerAsync(
            "reject-test",
            async ct =>
            {
                await Task.CompletedTask;
                return "should not reach";
            });

        // Assert
        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task ExecuteWithTimeoutAsync_Completes_Within_Timeout()
    {
        // Arrange
        var options = new ResilienceOptions { DefaultTimeoutMs = 5000 };
        var service = CreateService(options);

        // Act
        var result = await service.ExecuteWithTimeoutAsync(
            "quick-operation",
            async ct =>
            {
                await Task.Delay(10, ct);
                return "completed";
            });

        // Assert
        result.Should().Be("completed");
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_Throws_On_Timeout()
    {
        // Arrange
        var options = new ResilienceOptions { DefaultTimeoutMs = 50 };
        var service = CreateService(options);

        // Act
        var act = async () => await service.ExecuteWithTimeoutAsync(
            "slow-operation",
            async ct =>
            {
                await Task.Delay(5000, ct);
                return "should not reach";
            });

        // Assert
        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    #endregion

    #region Combined Policy Tests

    [Fact]
    public async Task ExecuteWithResilienceAsync_Combines_All_Policies()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 2,
            InitialRetryDelayMs = 10,
            DefaultTimeoutMs = 5000,
            CircuitBreakerFailureThreshold = 0.5 // Must be between 0 and 1
        };
        var service = CreateService(options);
        var attempts = 0;

        // Act
        var result = await service.ExecuteWithResilienceAsync(
            "combined-operation",
            async ct =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new InvalidOperationException("Retry me");
                }
                await Task.CompletedTask;
                return "resilient success";
            });

        // Assert
        result.Should().Be("resilient success");
        attempts.Should().Be(2);
    }

    #endregion

    #region OnRetry Callback Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_Invokes_OnRetry_Callback()
    {
        // Arrange
        var options = new ResilienceOptions { MaxRetryAttempts = 2, InitialRetryDelayMs = 10 };
        var service = CreateService(options);
        var retryCallbackInvoked = false;
        var attempts = 0;

        // Act
        try
        {
            await service.ExecuteWithRetryAsync<string>(
                "callback-operation",
                async ct =>
                {
                    attempts++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Retry");
                },
                onRetry: (ex, retryCount, _) =>
                {
                    retryCallbackInvoked = true;
                });
        }
        catch { }

        // Assert
        retryCallbackInvoked.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static ResilienceService CreateService(ResilienceOptions? options = null)
    {
        return new ResilienceService(options ?? new ResilienceOptions());
    }

    #endregion
}
