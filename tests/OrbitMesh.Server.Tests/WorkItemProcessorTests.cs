using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests;

public class WorkItemProcessorTests
{
    #region Class Tests

    [Fact]
    public void WorkItemProcessor_Exists()
    {
        // Arrange & Act
        var type = typeof(WorkItemProcessor);

        // Assert
        type.Should().NotBeNull();
        type.BaseType!.Name.Should().Be("BackgroundService");
    }

    [Fact]
    public void WorkItemProcessorOptions_Has_Default_Values()
    {
        // Arrange & Act
        var options = new WorkItemProcessorOptions();

        // Assert
        options.MaxConcurrency.Should().Be(10);
        options.PollingInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(5));
        options.MaxDispatchRetries.Should().Be(3);
    }

    [Fact]
    public void WorkItemProcessorOptions_Properties_Are_Settable()
    {
        // Arrange & Act
        var options = new WorkItemProcessorOptions
        {
            MaxConcurrency = 20,
            PollingInterval = TimeSpan.FromSeconds(5),
            RetryDelay = TimeSpan.FromSeconds(10),
            MaxDispatchRetries = 5
        };

        // Assert
        options.MaxConcurrency.Should().Be(20);
        options.PollingInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxDispatchRetries.Should().Be(5);
    }

    #endregion

    #region Dependency Tests

    [Fact]
    public void WorkItemProcessor_Requires_Dependencies()
    {
        // Arrange
        var constructor = typeof(WorkItemProcessor).GetConstructors()[0];
        var parameters = constructor.GetParameters();

        // Assert
        parameters.Should().HaveCount(6);
        parameters.Select(p => p.ParameterType.Name).Should().Contain("IJobManager");
        parameters.Select(p => p.ParameterType.Name).Should().Contain("IJobDispatcher");
        parameters.Select(p => p.ParameterType.Name).Should().Contain("IAgentRegistry");
        parameters.Select(p => p.ParameterType.Name).Should().Contain("IDeadLetterService");
    }

    #endregion
}

public class JobTimeoutMonitorTests
{
    #region Class Tests

    [Fact]
    public void JobTimeoutMonitor_Exists()
    {
        // Arrange & Act
        var type = typeof(JobTimeoutMonitor);

        // Assert
        type.Should().NotBeNull();
        type.BaseType!.Name.Should().Be("BackgroundService");
    }

    [Fact]
    public void JobTimeoutMonitorOptions_Has_Default_Values()
    {
        // Arrange & Act
        var options = new JobTimeoutMonitorOptions();

        // Assert
        options.CheckInterval.Should().Be(TimeSpan.FromSeconds(10));
        options.DefaultJobTimeout.Should().Be(TimeSpan.FromMinutes(5));
        options.AckTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxTimeoutRetries.Should().Be(3);
    }

    [Fact]
    public void JobTimeoutMonitorOptions_Properties_Are_Settable()
    {
        // Arrange & Act
        var options = new JobTimeoutMonitorOptions
        {
            CheckInterval = TimeSpan.FromSeconds(30),
            DefaultJobTimeout = TimeSpan.FromMinutes(10),
            AckTimeout = TimeSpan.FromMinutes(1),
            MaxTimeoutRetries = 5
        };

        // Assert
        options.CheckInterval.Should().Be(TimeSpan.FromSeconds(30));
        options.DefaultJobTimeout.Should().Be(TimeSpan.FromMinutes(10));
        options.AckTimeout.Should().Be(TimeSpan.FromMinutes(1));
        options.MaxTimeoutRetries.Should().Be(5);
    }

    #endregion

    #region Integration Tests with InMemoryJobManager

    [Fact]
    public async Task RequeueForTimeoutAsync_Requeues_Assigned_Job()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);
        await jobManager.AssignAsync(job.Id, "agent-1");

        // Act
        var result = await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);

        // Assert
        result.Should().BeTrue();
        var updatedJob = await jobManager.GetAsync(job.Id);
        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(JobStatus.Pending);
        updatedJob.TimeoutCount.Should().Be(1);
        updatedJob.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Requeues_Running_Job()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);
        await jobManager.AssignAsync(job.Id, "agent-1");
        await jobManager.AcknowledgeAsync(job.Id, "agent-1");

        // Act
        var result = await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);

        // Assert
        result.Should().BeTrue();
        var updatedJob = await jobManager.GetAsync(job.Id);
        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(JobStatus.Pending);
        updatedJob.TimeoutCount.Should().Be(1);
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Increments_TimeoutCount()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);

        // Simulate multiple timeout cycles
        for (int i = 0; i < 3; i++)
        {
            await jobManager.AssignAsync(job.Id, $"agent-{i}");
            await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 5);
        }

        // Assert
        var updatedJob = await jobManager.GetAsync(job.Id);
        updatedJob.Should().NotBeNull();
        updatedJob!.TimeoutCount.Should().Be(3);
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Fails_When_MaxRetries_Exceeded()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);

        // Simulate timeout cycles up to max
        for (int i = 0; i < 3; i++)
        {
            await jobManager.AssignAsync(job.Id, $"agent-{i}");
            await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);
        }

        await jobManager.AssignAsync(job.Id, "agent-final");

        // Act
        var result = await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Returns_False_For_Unknown_Job()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();

        // Act
        var result = await jobManager.RequeueForTimeoutAsync("unknown-job", maxTimeoutRetries: 3);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Returns_False_For_Pending_Job()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);

        // Act (try to requeue a pending job)
        var result = await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequeueForTimeoutAsync_Returns_False_For_Completed_Job()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);
        await jobManager.AssignAsync(job.Id, "agent-1");
        await jobManager.AcknowledgeAsync(job.Id, "agent-1");
        await jobManager.CompleteAsync(job.Id, JobResult.Success(job.Id, "agent-1"));

        // Act
        var result = await jobManager.RequeueForTimeoutAsync(job.Id, maxTimeoutRetries: 3);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Timeout Detection Tests

    [Fact]
    public async Task GetByStatusAsync_Returns_Assigned_Jobs()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request1 = JobRequest.Create("cmd-1");
        var request2 = JobRequest.Create("cmd-2");

        var job1 = await jobManager.EnqueueAsync(request1);
        var job2 = await jobManager.EnqueueAsync(request2);

        await jobManager.AssignAsync(job1.Id, "agent-1");
        // job2 remains pending

        // Act
        var assignedJobs = await jobManager.GetByStatusAsync(JobStatus.Assigned);

        // Assert
        assignedJobs.Should().HaveCount(1);
        assignedJobs[0].Id.Should().Be(job1.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_Returns_Running_Jobs()
    {
        // Arrange
        var jobManager = new InMemoryJobManager();
        var request = JobRequest.Create("test-command");
        var job = await jobManager.EnqueueAsync(request);
        await jobManager.AssignAsync(job.Id, "agent-1");
        await jobManager.AcknowledgeAsync(job.Id, "agent-1");

        // Act
        var runningJobs = await jobManager.GetByStatusAsync(JobStatus.Running);

        // Assert
        runningJobs.Should().HaveCount(1);
        runningJobs[0].Status.Should().Be(JobStatus.Running);
        runningJobs[0].StartedAt.Should().NotBeNull();
    }

    #endregion
}
