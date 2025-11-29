namespace OrbitMesh.Agent.Tests;

public class AgentTests
{
    [Fact]
    public void IMeshAgent_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IMeshAgent);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void MeshAgentBuilder_Creates_Agent()
    {
        // Arrange
        var builder = MeshAgentBuilder.Create("http://localhost:5000");

        // Act
        var agent = builder
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithCapability("test-capability")
            .WithTag("test-tag")
            .Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Id.Should().Be("test-agent");
        agent.Name.Should().Be("Test Agent");
    }

    [Fact]
    public void MeshAgentBuilder_Can_Chain_Configuration()
    {
        // Arrange & Act
        var builder = MeshAgentBuilder.Create("http://localhost:5000")
            .WithId("agent-1")
            .WithName("Agent One")
            .InGroup("test-group")
            .WithCapability("capability-1")
            .WithCapability("capability-2")
            .WithTag("environment:test")
            .WithTag("version:1.0");

        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Id.Should().Be("agent-1");
        agent.Name.Should().Be("Agent One");
    }
}
