using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests;

public class DeadLetterQueueTests
{
    #region Interface Tests

    [Fact]
    public void IDeadLetterService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IDeadLetterService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void InMemoryDeadLetterService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(InMemoryDeadLetterService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IDeadLetterService));
    }

    [Fact]
    public void DeadLetterEntry_Record_Exists()
    {
        // Arrange & Act
        var entryType = typeof(DeadLetterEntry);

        // Assert
        entryType.Should().NotBeNull();
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public async Task EnqueueAsync_Adds_Failed_Job_To_DLQ()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Test failure");

        // Act
        var entry = await dlq.EnqueueAsync(job, "Max retries exceeded");

        // Assert
        entry.Should().NotBeNull();
        entry.Job.Id.Should().Be("job-1");
        entry.Reason.Should().Be("Max retries exceeded");
    }

    [Fact]
    public async Task EnqueueAsync_Sets_EntryId_And_Timestamp()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Test failure");

        // Act
        var entry = await dlq.EnqueueAsync(job, "Unrecoverable error");

        // Assert
        entry.Id.Should().NotBeNullOrEmpty();
        entry.EnqueuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EnqueueAsync_Preserves_Original_Error()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Original error message", "ERR001");

        // Act
        var entry = await dlq.EnqueueAsync(job, "DLQ reason");

        // Assert
        entry.Job.Error.Should().Be("Original error message");
        entry.Job.ErrorCode.Should().Be("ERR001");
    }

    #endregion

    #region Get and Browse Tests

    [Fact]
    public async Task GetAsync_Returns_Entry_By_Id()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Test failure");
        var entry = await dlq.EnqueueAsync(job, "Test reason");

        // Act
        var result = await dlq.GetAsync(entry.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task GetAsync_Returns_Null_For_NonExistent_Entry()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();

        // Act
        var result = await dlq.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Returns_All_Entries()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error 1"), "Reason 1");
        await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error 2"), "Reason 2");
        await dlq.EnqueueAsync(CreateFailedJob("job-3", "Error 3"), "Reason 3");

        // Act
        var entries = await dlq.GetAllAsync();

        // Assert
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Entries_In_FIFO_Order()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error"), "First");
        await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error"), "Second");
        await dlq.EnqueueAsync(CreateFailedJob("job-3", "Error"), "Third");

        // Act
        var entries = await dlq.GetAllAsync();

        // Assert
        entries[0].Job.Id.Should().Be("job-1");
        entries[1].Job.Id.Should().Be("job-2");
        entries[2].Job.Id.Should().Be("job-3");
    }

    [Fact]
    public async Task GetByJobIdAsync_Returns_Entry_By_JobId()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error 1"), "Reason 1");
        await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error 2"), "Reason 2");

        // Act
        var entry = await dlq.GetByJobIdAsync("job-2");

        // Assert
        entry.Should().NotBeNull();
        entry!.Job.Id.Should().Be("job-2");
    }

    #endregion

    #region Retry Tests

    [Fact]
    public async Task MarkForRetryAsync_Updates_RetryRequested_Flag()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Error");
        var entry = await dlq.EnqueueAsync(job, "Reason");

        // Act
        var result = await dlq.MarkForRetryAsync(entry.Id);

        // Assert
        result.Should().BeTrue();
        var updated = await dlq.GetAsync(entry.Id);
        updated!.RetryRequested.Should().BeTrue();
        updated.RetryRequestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkForRetryAsync_Returns_False_For_NonExistent_Entry()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();

        // Act
        var result = await dlq.MarkForRetryAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingRetryAsync_Returns_Entries_Marked_For_Retry()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var entry1 = await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error"), "Reason");
        var entry2 = await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error"), "Reason");
        await dlq.EnqueueAsync(CreateFailedJob("job-3", "Error"), "Reason");

        await dlq.MarkForRetryAsync(entry1.Id);
        await dlq.MarkForRetryAsync(entry2.Id);

        // Act
        var pending = await dlq.GetPendingRetryAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.Select(e => e.Job.Id).Should().Contain(["job-1", "job-2"]);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public async Task RemoveAsync_Removes_Entry_From_DLQ()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        var job = CreateFailedJob("job-1", "Error");
        var entry = await dlq.EnqueueAsync(job, "Reason");

        // Act
        var result = await dlq.RemoveAsync(entry.Id);

        // Assert
        result.Should().BeTrue();
        var remaining = await dlq.GetAllAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_Returns_False_For_NonExistent_Entry()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();

        // Act
        var result = await dlq.RemoveAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeAsync_Removes_All_Entries()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error"), "Reason");
        await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error"), "Reason");
        await dlq.EnqueueAsync(CreateFailedJob("job-3", "Error"), "Reason");

        // Act
        var count = await dlq.PurgeAsync();

        // Assert
        count.Should().Be(3);
        var remaining = await dlq.GetAllAsync();
        remaining.Should().BeEmpty();
    }

    #endregion

    #region Count and Statistics Tests

    [Fact]
    public async Task GetCountAsync_Returns_Entry_Count()
    {
        // Arrange
        var dlq = new InMemoryDeadLetterService();
        await dlq.EnqueueAsync(CreateFailedJob("job-1", "Error"), "Reason");
        await dlq.EnqueueAsync(CreateFailedJob("job-2", "Error"), "Reason");

        // Act
        var count = await dlq.GetCountAsync();

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region Helper Methods

    private static Job CreateFailedJob(string id, string error, string? errorCode = null)
    {
        return Job.FromRequest(JobRequest.Create("test-command") with { IdempotencyKey = id }) with
        {
            Id = id,
            Status = JobStatus.Failed,
            Error = error,
            ErrorCode = errorCode,
            RetryCount = 3
        };
    }

    #endregion
}
