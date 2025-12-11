using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class IdempotencyServiceTests
{
    #region Interface Tests

    [Fact]
    public void IIdempotencyService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IIdempotencyService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void InMemoryIdempotencyService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(InMemoryIdempotencyService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IIdempotencyService));
    }

    #endregion

    #region TryAcquireLock Tests

    [Fact]
    public async Task TryAcquireLockAsync_Returns_True_For_New_Key()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "unique-key-1";

        // Act
        var result = await service.TryAcquireLockAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_Returns_False_For_Existing_Key()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "duplicate-key";

        // Act
        await service.TryAcquireLockAsync(key);
        var result = await service.TryAcquireLockAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLockAsync_Different_Keys_Both_Succeed()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();

        // Act
        var result1 = await service.TryAcquireLockAsync("key-1");
        var result2 = await service.TryAcquireLockAsync("key-2");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    #endregion

    #region SetResult and GetResult Tests

    [Fact]
    public async Task SetResultAsync_And_GetResultAsync_Returns_Stored_Value()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "result-key";
        var expectedResult = "job-123";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, expectedResult);
        var result = await service.GetResultAsync<string>(key);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task GetResultAsync_Returns_Default_For_NonExistent_Key()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();

        // Act
        var result = await service.GetResultAsync<string>("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetResultAsync_Can_Store_Complex_Types()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "complex-key";
        var expected = new IdempotencyResult { JobId = "job-1", Status = "Completed" };

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, expected);
        var result = await service.GetResultAsync<IdempotencyResult>(key);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be("job-1");
        result.Status.Should().Be("Completed");
    }

    #endregion

    #region Release Lock Tests

    [Fact]
    public async Task ReleaseLockAsync_Allows_Key_To_Be_Reacquired()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "release-key";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.ReleaseLockAsync(key);
        var result = await service.TryAcquireLockAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_Clears_Result()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "release-result-key";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, "some-result");
        await service.ReleaseLockAsync(key);
        var result = await service.GetResultAsync<string>(key);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsProcessing Tests

    [Fact]
    public async Task IsProcessingAsync_Returns_True_For_Locked_Key()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "processing-key";

        // Act
        await service.TryAcquireLockAsync(key);
        var result = await service.IsProcessingAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsProcessingAsync_Returns_False_For_Key_With_Result()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "completed-key";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, "result");
        var result = await service.IsProcessingAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsProcessingAsync_Returns_False_For_Unknown_Key()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();

        // Act
        var result = await service.IsProcessingAsync("unknown-key");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public async Task Entries_Expire_After_TTL()
    {
        // Arrange
        var ttl = TimeSpan.FromMilliseconds(50);
        var service = new InMemoryIdempotencyService(ttl);
        var key = "expiring-key";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, "result");
        await Task.Delay(100); // Wait for expiration
        var canAcquire = await service.TryAcquireLockAsync(key);

        // Assert
        canAcquire.Should().BeTrue();
    }

    [Fact]
    public async Task GetResultAsync_Returns_Null_After_Expiration()
    {
        // Arrange
        var ttl = TimeSpan.FromMilliseconds(50);
        var service = new InMemoryIdempotencyService(ttl);
        var key = "expiring-result-key";

        // Act
        await service.TryAcquireLockAsync(key);
        await service.SetResultAsync(key, "result");
        await Task.Delay(100); // Wait for expiration
        var result = await service.GetResultAsync<string>(key);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Concurrent_TryAcquireLock_Only_One_Succeeds()
    {
        // Arrange
        var service = new InMemoryIdempotencyService();
        var key = "concurrent-key";
        var successCount = 0;

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                if (await service.TryAcquireLockAsync(key))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));

        await Task.WhenAll(tasks);

        // Assert
        successCount.Should().Be(1);
    }

    #endregion

    #region Helper Types

    private sealed record IdempotencyResult
    {
        public string? JobId { get; init; }
        public string? Status { get; init; }
    }

    #endregion
}
