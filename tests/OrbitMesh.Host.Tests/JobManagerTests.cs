using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class JobManagerTests
{
    #region Interface and Type Tests

    [Fact]
    public void IJobManager_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IJobManager);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Job_Model_Exists()
    {
        // Arrange & Act
        var modelType = typeof(Job);

        // Assert
        modelType.Should().NotBeNull();
        modelType.IsClass.Should().BeTrue();
    }

    #endregion

    #region Job Lifecycle Tests

    [Fact]
    public async Task EnqueueAsync_Creates_Pending_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");

        // Act
        var job = await manager.EnqueueAsync(request);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().Be(request.Id);
        job.Status.Should().Be(JobStatus.Pending);
        job.Request.Should().Be(request);
    }

    [Fact]
    public async Task GetAsync_Returns_Existing_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var created = await manager.EnqueueAsync(request);

        // Act
        var retrieved = await manager.GetAsync(created.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetAsync_Returns_Null_For_NonExistent_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();

        // Act
        var result = await manager.GetAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignAsync_Updates_Job_Status_To_Assigned()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);

        // Act
        var assigned = await manager.AssignAsync(job.Id, "agent-1");

        // Assert
        assigned.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Assigned);
        updated.AssignedAgentId.Should().Be("agent-1");
    }

    [Fact]
    public async Task AcknowledgeAsync_Updates_Job_Status_To_Running()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");

        // Act
        var acknowledged = await manager.AcknowledgeAsync(job.Id, "agent-1");

        // Assert
        acknowledged.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Running);
        updated.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteAsync_Updates_Job_Status_To_Completed()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");

        var result = JobResult.Success(job.Id, "agent-1");

        // Act
        var completed = await manager.CompleteAsync(job.Id, result);

        // Assert
        completed.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
        updated.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task FailAsync_Updates_Job_Status_To_Failed()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");

        // Act
        var failed = await manager.FailAsync(job.Id, "Test error", "TEST_ERROR");

        // Assert
        failed.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Failed);
        updated.Error.Should().Be("Test error");
        updated.ErrorCode.Should().Be("TEST_ERROR");
    }

    [Fact]
    public async Task CancelAsync_Updates_Job_Status_To_Cancelled()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);

        // Act
        var cancelled = await manager.CancelAsync(job.Id, "User requested");

        // Assert
        cancelled.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Cancelled);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetPendingAsync_Returns_Only_Pending_Jobs()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request1 = JobRequest.Create("command-1");
        var request2 = JobRequest.Create("command-2");
        var request3 = JobRequest.Create("command-3");

        var job1 = await manager.EnqueueAsync(request1);
        await manager.EnqueueAsync(request2);
        await manager.EnqueueAsync(request3);
        await manager.AssignAsync(job1.Id, "agent-1"); // No longer pending

        // Act
        var pending = await manager.GetPendingAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().AllSatisfy(j => j.Status.Should().Be(JobStatus.Pending));
    }

    [Fact]
    public async Task GetByAgentAsync_Returns_Jobs_For_Specific_Agent()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request1 = JobRequest.Create("command-1");
        var request2 = JobRequest.Create("command-2");

        var job1 = await manager.EnqueueAsync(request1);
        var job2 = await manager.EnqueueAsync(request2);

        await manager.AssignAsync(job1.Id, "agent-1");
        await manager.AssignAsync(job2.Id, "agent-2");

        // Act
        var agent1Jobs = await manager.GetByAgentAsync("agent-1");

        // Assert
        agent1Jobs.Should().HaveCount(1);
        agent1Jobs[0].Id.Should().Be(job1.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_Returns_Jobs_With_Specific_Status()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request1 = JobRequest.Create("command-1");
        var request2 = JobRequest.Create("command-2");

        var job1 = await manager.EnqueueAsync(request1);
        await manager.EnqueueAsync(request2);
        await manager.AssignAsync(job1.Id, "agent-1");
        await manager.AcknowledgeAsync(job1.Id, "agent-1");

        // Act
        var runningJobs = await manager.GetByStatusAsync(JobStatus.Running);

        // Assert
        runningJobs.Should().HaveCount(1);
        runningJobs[0].Status.Should().Be(JobStatus.Running);
    }

    #endregion

    #region Priority Queue Tests

    [Fact]
    public async Task DequeueNextAsync_Returns_Highest_Priority_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();

        var lowPriority = JobRequest.Create("low") with { Priority = 1 };
        var highPriority = JobRequest.Create("high") with { Priority = 10 };
        var mediumPriority = JobRequest.Create("medium") with { Priority = 5 };

        await manager.EnqueueAsync(lowPriority);
        await manager.EnqueueAsync(highPriority);
        await manager.EnqueueAsync(mediumPriority);

        // Act
        var first = await manager.DequeueNextAsync();
        var second = await manager.DequeueNextAsync();
        var third = await manager.DequeueNextAsync();

        // Assert
        first!.Request.Priority.Should().Be(10);
        second!.Request.Priority.Should().Be(5);
        third!.Request.Priority.Should().Be(1);
    }

    [Fact]
    public async Task DequeueNextAsync_Returns_Null_When_Queue_Empty()
    {
        // Arrange
        var manager = new InMemoryJobManager();

        // Act
        var result = await manager.DequeueNextAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DequeueNextAsync_With_Capability_Returns_Matching_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();

        var gpuJob = JobRequest.Create("gpu-task") with
        {
            RequiredCapabilities = ["gpu"]
        };
        var cpuJob = JobRequest.Create("cpu-task") with
        {
            RequiredCapabilities = ["cpu"]
        };

        await manager.EnqueueAsync(gpuJob);
        await manager.EnqueueAsync(cpuJob);

        // Act
        var result = await manager.DequeueNextAsync(["cpu"]);

        // Assert
        result.Should().NotBeNull();
        result!.Request.Command.Should().Be("cpu-task");
    }

    [Fact]
    public async Task DequeueNextAsync_Same_Priority_Uses_FIFO()
    {
        // Arrange
        var manager = new InMemoryJobManager();

        var first = JobRequest.Create("first") with { Priority = 5 };
        var second = JobRequest.Create("second") with { Priority = 5 };
        var third = JobRequest.Create("third") with { Priority = 5 };

        await manager.EnqueueAsync(first);
        await Task.Delay(10); // Ensure distinct timestamps
        await manager.EnqueueAsync(second);
        await Task.Delay(10);
        await manager.EnqueueAsync(third);

        // Act
        var dequeued1 = await manager.DequeueNextAsync();
        var dequeued2 = await manager.DequeueNextAsync();
        var dequeued3 = await manager.DequeueNextAsync();

        // Assert
        dequeued1!.Request.Command.Should().Be("first");
        dequeued2!.Request.Command.Should().Be("second");
        dequeued3!.Request.Command.Should().Be("third");
    }

    #endregion

    #region Timeout and Retry Tests

    [Fact]
    public async Task GetTimedOutJobsAsync_Returns_Jobs_Past_Timeout()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command") with
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");

        await Task.Delay(100); // Wait past timeout

        // Act
        var timedOut = await manager.GetTimedOutJobsAsync();

        // Assert
        timedOut.Should().HaveCount(1);
        timedOut[0].Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task RequeueAsync_Resets_Job_For_Retry()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command") with { MaxRetries = 3 };
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");
        await manager.FailAsync(job.Id, "First failure", "ERROR");

        // Act
        var requeued = await manager.RequeueAsync(job.Id);

        // Assert
        requeued.Should().BeTrue();
        var updated = await manager.GetAsync(job.Id);
        updated!.Status.Should().Be(JobStatus.Pending);
        updated.RetryCount.Should().Be(1);
        updated.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public async Task RequeueAsync_Fails_When_Max_Retries_Exceeded()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command") with { MaxRetries = 0 };
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");
        await manager.FailAsync(job.Id, "Failure", "ERROR");

        // Act
        var requeued = await manager.RequeueAsync(job.Id);

        // Assert
        requeued.Should().BeFalse();
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task EnqueueAsync_With_Same_IdempotencyKey_Returns_Existing_Job()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var idempotencyKey = Guid.NewGuid().ToString();
        var request1 = new JobRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Command = "command-1"
        };
        var request2 = new JobRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Command = "command-2"
        };

        // Act
        var job1 = await manager.EnqueueAsync(request1);
        var job2 = await manager.EnqueueAsync(request2);

        // Assert
        job1.Id.Should().Be(job2.Id);
        job2.Request.Command.Should().Be("command-1"); // Original command preserved
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public async Task UpdateProgressAsync_Updates_Job_Progress()
    {
        // Arrange
        var manager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await manager.EnqueueAsync(request);
        await manager.AssignAsync(job.Id, "agent-1");
        await manager.AcknowledgeAsync(job.Id, "agent-1");

        var progress = new JobProgress
        {
            JobId = job.Id,
            Percentage = 50,
            Message = "Processing..."
        };

        // Act
        await manager.UpdateProgressAsync(progress);

        // Assert
        var updated = await manager.GetAsync(job.Id);
        updated!.LastProgress.Should().NotBeNull();
        updated.LastProgress!.Percentage.Should().Be(50);
    }

    #endregion
}
