using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests.Services;

public class HealthCheckServiceTests
{
    #region AgentHealthCheck Tests

    [Fact]
    public async Task AgentHealthCheck_When_HasReadyAgents_Returns_Healthy()
    {
        // Arrange
        var registryMock = new Mock<IAgentRegistry>();
        registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentInfo>
            {
                CreateAgent("agent-1", AgentStatus.Ready),
                CreateAgent("agent-2", AgentStatus.Ready)
            });

        var healthCheck = new AgentHealthCheck(registryMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("2");
    }

    [Fact]
    public async Task AgentHealthCheck_When_NoAgents_Returns_Degraded()
    {
        // Arrange
        var registryMock = new Mock<IAgentRegistry>();
        registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentInfo>());

        var healthCheck = new AgentHealthCheck(registryMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("No agents");
    }

    [Fact]
    public async Task AgentHealthCheck_When_AllDisconnected_Returns_Degraded()
    {
        // Arrange
        var registryMock = new Mock<IAgentRegistry>();
        registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentInfo>
            {
                CreateAgent("agent-1", AgentStatus.Disconnected),
                CreateAgent("agent-2", AgentStatus.Disconnected)
            });

        var healthCheck = new AgentHealthCheck(registryMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task AgentHealthCheck_Returns_Agent_Count_Data()
    {
        // Arrange
        var registryMock = new Mock<IAgentRegistry>();
        registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentInfo>
            {
                CreateAgent("agent-1", AgentStatus.Ready),
                CreateAgent("agent-2", AgentStatus.Running),
                CreateAgent("agent-3", AgentStatus.Disconnected)
            });

        var healthCheck = new AgentHealthCheck(registryMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("TotalAgents");
        result.Data["TotalAgents"].Should().Be(3);
        result.Data.Should().ContainKey("ReadyAgents");
        result.Data["ReadyAgents"].Should().Be(1);
        result.Data.Should().ContainKey("RunningAgents");
        result.Data["RunningAgents"].Should().Be(1);
    }

    [Fact]
    public async Task AgentHealthCheck_When_Exception_Returns_Unhealthy()
    {
        // Arrange
        var registryMock = new Mock<IAgentRegistry>();
        registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var healthCheck = new AgentHealthCheck(registryMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }

    #endregion

    #region JobQueueHealthCheck Tests

    [Fact]
    public async Task JobQueueHealthCheck_When_NoPendingJobs_Returns_Healthy()
    {
        // Arrange
        var jobManagerMock = new Mock<IJobManager>();
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Pending, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Running, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

        var healthCheck = new JobQueueHealthCheck(jobManagerMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task JobQueueHealthCheck_When_FewPendingJobs_Returns_Healthy()
    {
        // Arrange
        var jobManagerMock = new Mock<IJobManager>();
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Pending, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>
            {
                CreateJob("job-1", JobStatus.Pending),
                CreateJob("job-2", JobStatus.Pending)
            });
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Running, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

        var healthCheck = new JobQueueHealthCheck(jobManagerMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task JobQueueHealthCheck_When_ManyPendingJobs_Returns_Degraded()
    {
        // Arrange
        var jobManagerMock = new Mock<IJobManager>();
        var manyPendingJobs = Enumerable
            .Range(1, 100)
            .Select(i => CreateJob($"job-{i}", JobStatus.Pending))
            .ToList();

        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Pending, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyPendingJobs);
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Running, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

        var healthCheck = new JobQueueHealthCheck(jobManagerMock.Object, pendingThreshold: 50);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("pending");
    }

    [Fact]
    public async Task JobQueueHealthCheck_Returns_Job_Count_Data()
    {
        // Arrange
        var jobManagerMock = new Mock<IJobManager>();
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Pending, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { CreateJob("job-1", JobStatus.Pending) });
        jobManagerMock
            .Setup(m => m.GetJobsAsync(JobStatus.Running, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>
            {
                CreateJob("job-2", JobStatus.Running),
                CreateJob("job-3", JobStatus.Running)
            });

        var healthCheck = new JobQueueHealthCheck(jobManagerMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("PendingJobs");
        result.Data["PendingJobs"].Should().Be(1);
        result.Data.Should().ContainKey("RunningJobs");
        result.Data["RunningJobs"].Should().Be(2);
    }

    [Fact]
    public async Task JobQueueHealthCheck_When_Exception_Returns_Unhealthy()
    {
        // Arrange
        var jobManagerMock = new Mock<IJobManager>();
        jobManagerMock
            .Setup(m => m.GetJobsAsync(It.IsAny<JobStatus?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var healthCheck = new JobQueueHealthCheck(jobManagerMock.Object);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static HealthCheckContext CreateHealthCheckContext()
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", _ => null!, null, null)
        };
    }

    private static AgentInfo CreateAgent(string id, AgentStatus status)
    {
        return new AgentInfo
        {
            Id = id,
            Name = $"Agent-{id}",
            Status = status,
            Capabilities = [new AgentCapability { Name = "test" }],
            LastHeartbeat = DateTimeOffset.UtcNow
        };
    }

    private static Job CreateJob(string id, JobStatus status)
    {
        return new Job
        {
            Id = id,
            Request = JobRequest.Create("test") with { Id = id },
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}
