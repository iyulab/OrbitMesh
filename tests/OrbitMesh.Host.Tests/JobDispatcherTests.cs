using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Hubs;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class JobDispatcherTests
{
    private readonly Mock<IAgentRegistry> _agentRegistryMock;
    private readonly Mock<IJobManager> _jobManagerMock;
    private readonly Mock<IHubContext<AgentHub, IAgentClient>> _hubContextMock;
    private readonly Mock<ILogger<JobDispatcher>> _loggerMock;

    public JobDispatcherTests()
    {
        _agentRegistryMock = new Mock<IAgentRegistry>();
        _jobManagerMock = new Mock<IJobManager>();
        _hubContextMock = new Mock<IHubContext<AgentHub, IAgentClient>>();
        _loggerMock = new Mock<ILogger<JobDispatcher>>();
    }

    #region Interface and Type Tests

    [Fact]
    public void IJobDispatcher_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IJobDispatcher);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void JobDispatcher_Implements_IJobDispatcher()
    {
        // Arrange & Act
        var dispatcherType = typeof(JobDispatcher);

        // Assert
        dispatcherType.GetInterfaces().Should().Contain(typeof(IJobDispatcher));
    }

    #endregion

    #region Dispatch Tests

    [Fact]
    public async Task DispatchAsync_With_TargetAgentId_Sends_To_Specific_Agent()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var agent = CreateAgent("agent-1", ["process-data"]);
        var job = CreateJob("job-1", "process-data", targetAgentId: "agent-1");

        _agentRegistryMock.Setup(r => r.GetAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var clientMock = new Mock<IAgentClient>();
        SetupHubClient("agent-1", agent.ConnectionId!, clientMock);

        _jobManagerMock.Setup(m => m.AssignAsync(job.Id, "agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AgentId.Should().Be("agent-1");
        clientMock.Verify(c => c.ExecuteJobAsync(job.Request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_With_RequiredCapabilities_Selects_Capable_Agent()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var agent1 = CreateAgent("agent-1", ["process-data"]);
        var agent2 = CreateAgent("agent-2", ["generate-report"]);
        var job = CreateJob("job-1", "process-data", requiredCapabilities: ["process-data"]);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent1]);

        var clientMock = new Mock<IAgentClient>();
        SetupHubClient("agent-1", agent1.ConnectionId!, clientMock);

        _jobManagerMock.Setup(m => m.AssignAsync(job.Id, "agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AgentId.Should().Be("agent-1");
    }

    [Fact]
    public async Task DispatchAsync_Returns_Failure_When_No_Available_Agent()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var job = CreateJob("job-1", "unknown-capability", requiredCapabilities: ["unknown-capability"]);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("unknown-capability", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("No available agent");
    }

    [Fact]
    public async Task DispatchAsync_Returns_Failure_When_Target_Agent_Offline()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var job = CreateJob("job-1", "process-data", targetAgentId: "offline-agent");

        _agentRegistryMock.Setup(r => r.GetAsync("offline-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentInfo?)null);

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("not found");
    }

    #endregion

    #region Queue Tests

    [Fact]
    public async Task EnqueueAsync_Adds_Job_To_Queue()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var request = JobRequest.Create("test-command");
        var job = Job.FromRequest(request);

        _jobManagerMock.Setup(m => m.EnqueueAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        var result = await dispatcher.EnqueueAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(job.Id);
        _jobManagerMock.Verify(m => m.EnqueueAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQueueDepthAsync_Returns_Pending_Job_Count()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var pendingJobs = new List<Job>
        {
            CreateJob("job-1", "cmd"),
            CreateJob("job-2", "cmd"),
            CreateJob("job-3", "cmd")
        };

        _jobManagerMock.Setup(m => m.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingJobs);

        // Act
        var depth = await dispatcher.GetQueueDepthAsync();

        // Assert
        depth.Should().Be(3);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CancelJobAsync_Sends_Cancel_To_Agent_And_Updates_Manager()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var agent = CreateAgent("agent-1", ["process-data"]);
        var job = CreateJob("job-1", "process-data") with
        {
            Status = JobStatus.Running,
            AssignedAgentId = "agent-1"
        };

        _jobManagerMock.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _agentRegistryMock.Setup(r => r.GetAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var clientMock = new Mock<IAgentClient>();
        SetupHubClient("agent-1", agent.ConnectionId!, clientMock);

        _jobManagerMock.Setup(m => m.CancelAsync("job-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await dispatcher.CancelJobAsync("job-1", "User requested");

        // Assert
        result.Should().BeTrue();
        clientMock.Verify(c => c.CancelJobAsync("job-1", It.IsAny<CancellationToken>()), Times.Once);
        _jobManagerMock.Verify(m => m.CancelAsync("job-1", "User requested", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelJobAsync_Returns_False_For_NonExistent_Job()
    {
        // Arrange
        var dispatcher = CreateDispatcher();

        _jobManagerMock.Setup(m => m.GetAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        // Act
        var result = await dispatcher.CancelJobAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatisticsAsync_Returns_Dispatch_Metrics()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var pending = new List<Job> { CreateJob("j1", "cmd"), CreateJob("j2", "cmd") };
        var running = new List<Job> { CreateJob("j3", "cmd") };
        var agents = new List<AgentInfo> { CreateAgent("a1", []), CreateAgent("a2", []) };

        _jobManagerMock.Setup(m => m.GetPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _jobManagerMock.Setup(m => m.GetByStatusAsync(JobStatus.Running, It.IsAny<CancellationToken>())).ReturnsAsync(running);
        _agentRegistryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents);

        // Act
        var stats = await dispatcher.GetStatisticsAsync();

        // Assert
        stats.PendingJobs.Should().Be(2);
        stats.RunningJobs.Should().Be(1);
        stats.ConnectedAgents.Should().Be(2);
    }

    #endregion

    #region Helper Methods

    private JobDispatcher CreateDispatcher()
    {
        return new JobDispatcher(
            _agentRegistryMock.Object,
            _jobManagerMock.Object,
            _hubContextMock.Object,
            _loggerMock.Object);
    }

    private static AgentInfo CreateAgent(string id, IReadOnlyList<string> capabilities)
    {
        return new AgentInfo
        {
            Id = id,
            Name = $"Agent {id}",
            Status = AgentStatus.Ready,
            ConnectionId = $"connection-{id}",
            Capabilities = capabilities.Select(c => new AgentCapability { Name = c }).ToList()
        };
    }

    private static Job CreateJob(
        string id,
        string command,
        string? targetAgentId = null,
        IReadOnlyList<string>? requiredCapabilities = null)
    {
        var request = new JobRequest
        {
            Id = id,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Command = command,
            TargetAgentId = targetAgentId,
            RequiredCapabilities = requiredCapabilities
        };

        return Job.FromRequest(request);
    }

    private void SetupHubClient(string agentId, string connectionId, Mock<IAgentClient> clientMock)
    {
        var clientsMock = new Mock<IHubClients<IAgentClient>>();
        clientsMock.Setup(c => c.Client(connectionId)).Returns(clientMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
    }

    #endregion
}
