using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Tests;

public class CoreTests
{
    [Fact]
    public void IMeshHandler_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IMeshHandler);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void AgentInfo_Can_Be_Created()
    {
        // Arrange & Act
        var agent = new AgentInfo
        {
            Id = "test-agent",
            Name = "Test Agent"
        };

        // Assert
        agent.Id.Should().Be("test-agent");
        agent.Name.Should().Be("Test Agent");
        agent.Status.Should().Be(AgentStatus.Created);
    }

    [Fact]
    public void JobRequest_Create_Generates_Id()
    {
        // Arrange & Act
        var request = JobRequest.Create("test-command");

        // Assert
        request.Id.Should().NotBeNullOrEmpty();
        request.IdempotencyKey.Should().NotBeNullOrEmpty();
        request.Command.Should().Be("test-command");
        request.Pattern.Should().Be(ExecutionPattern.RequestResponse);
    }

    [Fact]
    public void JobResult_Success_Creates_Completed_Result()
    {
        // Arrange & Act
        var result = JobResult.Success("job-1", "agent-1");

        // Assert
        result.JobId.Should().Be("job-1");
        result.AgentId.Should().Be("agent-1");
        result.Status.Should().Be(JobStatus.Completed);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void JobResult_Failure_Creates_Failed_Result()
    {
        // Arrange & Act
        var result = JobResult.Failure("job-1", "agent-1", "Test error", "TEST_ERROR");

        // Assert
        result.JobId.Should().Be("job-1");
        result.AgentId.Should().Be("agent-1");
        result.Status.Should().Be(JobStatus.Failed);
        result.Error.Should().Be("Test error");
        result.ErrorCode.Should().Be("TEST_ERROR");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void AgentInfo_HasCapability_Returns_True_When_Present()
    {
        // Arrange
        var agent = new AgentInfo
        {
            Id = "test-agent",
            Name = "Test Agent",
            Capabilities = [new AgentCapability { Name = "process-data" }]
        };

        // Act & Assert
        agent.HasCapability("process-data").Should().BeTrue();
        agent.HasCapability("PROCESS-DATA").Should().BeTrue(); // Case insensitive
        agent.HasCapability("other").Should().BeFalse();
    }
}
