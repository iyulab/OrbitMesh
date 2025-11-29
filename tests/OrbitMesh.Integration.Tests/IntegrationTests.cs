using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Hubs;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Integration.Tests;

public class IntegrationTests
{
    [Fact]
    public void All_Core_Types_Are_Available()
    {
        // Arrange & Act & Assert
        typeof(IMeshHandler).Should().NotBeNull();
        typeof(IAgentClient).Should().NotBeNull();
        typeof(IServerHub).Should().NotBeNull();
        typeof(AgentInfo).Should().NotBeNull();
        typeof(JobRequest).Should().NotBeNull();
        typeof(JobResult).Should().NotBeNull();
    }

    [Fact]
    public void Server_Types_Are_Available()
    {
        // Arrange & Act & Assert
        typeof(AgentHub).Should().NotBeNull();
        typeof(IAgentRegistry).Should().NotBeNull();
        typeof(InMemoryAgentRegistry).Should().NotBeNull();
    }

    [Fact]
    public void Agent_Types_Are_Available()
    {
        // Arrange & Act & Assert
        typeof(Agent.IMeshAgent).Should().NotBeNull();
        typeof(Agent.MeshAgentBuilder).Should().NotBeNull();
        typeof(Agent.IHandlerRegistry).Should().NotBeNull();
    }

    [Fact]
    public void Storage_Types_Are_Available()
    {
        // This test validates the Storage.Sqlite assembly is referenced correctly
        // Storage implementations will be added in Phase 3
        typeof(OrbitMesh.Storage.Sqlite.SqliteStorageMarker).Should().NotBeNull();
        OrbitMesh.Storage.Sqlite.SqliteStorageMarker.IsAvailable.Should().BeTrue();
    }
}
