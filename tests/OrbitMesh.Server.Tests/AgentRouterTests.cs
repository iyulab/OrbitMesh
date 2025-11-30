using Moq;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests;

public class AgentRouterTests
{
    private readonly Mock<IAgentRegistry> _agentRegistryMock;
    private readonly Mock<IJobManager> _jobManagerMock;

    public AgentRouterTests()
    {
        _agentRegistryMock = new Mock<IAgentRegistry>();
        _jobManagerMock = new Mock<IJobManager>();
    }

    #region Interface and Type Tests

    [Fact]
    public void IAgentRouter_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IAgentRouter);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void LoadBalancingStrategy_Enum_Exists()
    {
        // Arrange & Act
        var enumType = typeof(LoadBalancingStrategy);

        // Assert
        enumType.Should().NotBeNull();
        enumType.IsEnum.Should().BeTrue();
    }

    [Fact]
    public void AgentRouter_Implements_IAgentRouter()
    {
        // Arrange & Act
        var routerType = typeof(AgentRouter);

        // Assert
        routerType.GetInterfaces().Should().Contain(typeof(IAgentRouter));
    }

    #endregion

    #region Capability Matching Tests

    [Fact]
    public async Task SelectAgentAsync_Returns_Agent_With_All_Required_Capabilities()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var agent1 = CreateAgent("agent-1", ["gpu", "cuda"]);
        var agent2 = CreateAgent("agent-2", ["gpu"]);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("gpu", It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent1, agent2]);

        var request = CreateRoutingRequest(requiredCapabilities: ["gpu", "cuda"]);

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("agent-1"); // Only agent-1 has both capabilities
    }

    [Fact]
    public async Task SelectAgentAsync_Returns_Null_When_No_Agent_Has_All_Capabilities()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var agent1 = CreateAgent("agent-1", ["gpu"]);
        var agent2 = CreateAgent("agent-2", ["cpu"]);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("gpu", It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent1]);

        var request = CreateRoutingRequest(requiredCapabilities: ["gpu", "cuda"]);

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectAgentAsync_Filters_Out_Non_Ready_Agents()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var readyAgent = CreateAgent("agent-1", ["process"], AgentStatus.Ready);
        var pausedAgent = CreateAgent("agent-2", ["process"], AgentStatus.Paused);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync([readyAgent, pausedAgent]);

        var request = CreateRoutingRequest(requiredCapabilities: ["process"]);

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("agent-1");
    }

    #endregion

    #region Round Robin Tests

    [Fact]
    public async Task RoundRobin_Distributes_Evenly_Across_Agents()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var agents = new[]
        {
            CreateAgent("agent-1", ["process"]),
            CreateAgent("agent-2", ["process"]),
            CreateAgent("agent-3", ["process"])
        };

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents.ToList());

        var request = CreateRoutingRequest(requiredCapabilities: ["process"]);

        // Act
        var selections = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var result = await router.SelectAgentAsync(request);
            selections.Add(result!.Id);
        }

        // Assert
        selections.Count(s => s == "agent-1").Should().Be(2);
        selections.Count(s => s == "agent-2").Should().Be(2);
        selections.Count(s => s == "agent-3").Should().Be(2);
    }

    #endregion

    #region Least Connections Tests

    [Fact]
    public async Task LeastConnections_Selects_Agent_With_Fewest_Running_Jobs()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.LeastConnections);
        var agent1 = CreateAgent("agent-1", ["process"]);
        var agent2 = CreateAgent("agent-2", ["process"]);
        var agent3 = CreateAgent("agent-3", ["process"]);

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent1, agent2, agent3]);

        // Agent 1 has 3 running jobs, Agent 2 has 1, Agent 3 has 2
        _jobManagerMock.Setup(m => m.GetByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRunningJobs(3));
        _jobManagerMock.Setup(m => m.GetByAgentAsync("agent-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRunningJobs(1));
        _jobManagerMock.Setup(m => m.GetByAgentAsync("agent-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRunningJobs(2));

        var request = CreateRoutingRequest(requiredCapabilities: ["process"]);

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("agent-2"); // Least connections
    }

    #endregion

    #region Random Tests

    [Fact]
    public async Task Random_Selects_From_Available_Agents()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.Random);
        var agents = new[]
        {
            CreateAgent("agent-1", ["process"]),
            CreateAgent("agent-2", ["process"]),
            CreateAgent("agent-3", ["process"])
        };

        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents.ToList());

        var request = CreateRoutingRequest(requiredCapabilities: ["process"]);

        // Act - Run multiple times to verify randomness
        var selections = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var result = await router.SelectAgentAsync(request);
            selections.Add(result!.Id);
        }

        // Assert - Should have selected all agents at least once
        selections.Should().HaveCountGreaterThanOrEqualTo(2); // Statistically should hit at least 2
    }

    #endregion

    #region Preferred Agent Tests

    [Fact]
    public async Task SelectAgentAsync_Prefers_Specified_Agent_When_Available()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var preferredAgent = CreateAgent("preferred", ["process"]);
        var otherAgent = CreateAgent("other", ["process"]);

        _agentRegistryMock.Setup(r => r.GetAsync("preferred", It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferredAgent);
        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync([preferredAgent, otherAgent]);

        var request = CreateRoutingRequest(
            requiredCapabilities: ["process"],
            preferredAgentId: "preferred");

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("preferred");
    }

    [Fact]
    public async Task SelectAgentAsync_Falls_Back_When_Preferred_Agent_Unavailable()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var unavailableAgent = CreateAgent("preferred", ["process"], AgentStatus.Paused);
        var availableAgent = CreateAgent("other", ["process"]);

        _agentRegistryMock.Setup(r => r.GetAsync("preferred", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unavailableAgent);
        _agentRegistryMock.Setup(r => r.GetByCapabilityAsync("process", It.IsAny<CancellationToken>()))
            .ReturnsAsync([unavailableAgent, availableAgent]);

        var request = CreateRoutingRequest(
            requiredCapabilities: ["process"],
            preferredAgentId: "preferred");

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("other");
    }

    #endregion

    #region Group Filtering Tests

    [Fact]
    public async Task SelectAgentAsync_Filters_By_Agent_Group()
    {
        // Arrange
        var router = CreateRouter(LoadBalancingStrategy.RoundRobin);
        var groupAAgent = CreateAgent("agent-a", ["process"]) with { Group = "production" };
        var groupBAgent = CreateAgent("agent-b", ["process"]) with { Group = "staging" };

        _agentRegistryMock.Setup(r => r.GetByGroupAsync("production", It.IsAny<CancellationToken>()))
            .ReturnsAsync([groupAAgent]);

        var request = CreateRoutingRequest(
            requiredCapabilities: ["process"],
            targetGroup: "production");

        // Act
        var result = await router.SelectAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("agent-a");
    }

    #endregion

    #region Helper Methods

    private AgentRouter CreateRouter(LoadBalancingStrategy strategy)
    {
        return new AgentRouter(_agentRegistryMock.Object, _jobManagerMock.Object, strategy);
    }

    private static AgentInfo CreateAgent(
        string id,
        IReadOnlyList<string> capabilities,
        AgentStatus status = AgentStatus.Ready)
    {
        return new AgentInfo
        {
            Id = id,
            Name = $"Agent {id}",
            Status = status,
            ConnectionId = $"connection-{id}",
            Capabilities = capabilities.Select(c => new AgentCapability { Name = c }).ToList()
        };
    }

    private static RoutingRequest CreateRoutingRequest(
        IReadOnlyList<string>? requiredCapabilities = null,
        string? preferredAgentId = null,
        string? targetGroup = null)
    {
        return new RoutingRequest
        {
            RequiredCapabilities = requiredCapabilities ?? [],
            PreferredAgentId = preferredAgentId,
            TargetGroup = targetGroup
        };
    }

    private static List<Job> CreateRunningJobs(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => Job.FromRequest(JobRequest.Create($"command-{i}")) with
            {
                Status = JobStatus.Running
            })
            .ToList();
    }

    #endregion
}
