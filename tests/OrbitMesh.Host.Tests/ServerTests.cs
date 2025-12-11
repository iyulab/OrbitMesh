using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Hubs;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class ServerTests
{
    [Fact]
    public void IAgentRegistry_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IAgentRegistry);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void AgentHub_Inherits_From_SignalR_Hub()
    {
        // Arrange & Act
        var hubType = typeof(AgentHub);

        // Assert
        hubType.Should().NotBeNull();
        hubType.BaseType?.Name.Should().Contain("Hub");
    }

    [Fact]
    public void AgentHub_Implements_IServerHub()
    {
        // Arrange & Act
        var hubType = typeof(AgentHub);

        // Assert
        hubType.GetInterfaces().Should().Contain(typeof(IServerHub));
    }

    [Fact]
    public async Task InMemoryAgentRegistry_Registers_Agent()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        var agent = new AgentInfo
        {
            Id = "test-agent",
            Name = "Test Agent"
        };

        // Act
        await registry.RegisterAsync(agent);
        var retrieved = await registry.GetAsync("test-agent");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test-agent");
        retrieved.Name.Should().Be("Test Agent");
    }

    [Fact]
    public async Task InMemoryAgentRegistry_GetAll_Returns_All_Agents()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(new AgentInfo { Id = "agent-1", Name = "Agent 1" });
        await registry.RegisterAsync(new AgentInfo { Id = "agent-2", Name = "Agent 2" });

        // Act
        var agents = await registry.GetAllAsync();

        // Assert
        agents.Should().HaveCount(2);
    }

    [Fact]
    public async Task InMemoryAgentRegistry_GetByCapability_Returns_Matching_Agents()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(new AgentInfo
        {
            Id = "agent-1",
            Name = "Agent 1",
            Capabilities = [new AgentCapability { Name = "process-data" }]
        });
        await registry.RegisterAsync(new AgentInfo
        {
            Id = "agent-2",
            Name = "Agent 2",
            Capabilities = [new AgentCapability { Name = "generate-report" }]
        });

        // Act
        var agents = await registry.GetByCapabilityAsync("process-data");

        // Assert
        agents.Should().HaveCount(1);
        agents[0].Id.Should().Be("agent-1");
    }

    [Fact]
    public async Task InMemoryAgentRegistry_UpdateStatus_Updates_Agent_Status()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(new AgentInfo
        {
            Id = "test-agent",
            Name = "Test Agent",
            Status = AgentStatus.Created
        });

        // Act
        await registry.UpdateStatusAsync("test-agent", AgentStatus.Ready);
        var agent = await registry.GetAsync("test-agent");

        // Assert
        agent.Should().NotBeNull();
        agent!.Status.Should().Be(AgentStatus.Ready);
    }
}
