using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Server.Controllers;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests.Controllers;

public class JobsControllerTests
{
    private readonly Mock<IJobOrchestrator> _orchestratorMock;
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _orchestratorMock = new Mock<IJobOrchestrator>();
        _controller = new JobsController(_orchestratorMock.Object);
    }

    #region Submit Job Tests

    [Fact]
    public async Task SubmitJob_With_ValidRequest_Returns_Created()
    {
        // Arrange
        var request = JobRequest.Create("test-command");
        var expectedResult = JobSubmissionResult.Succeeded(request.Id);

        _orchestratorMock
            .Setup(o => o.SubmitJobAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SubmitJob(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(JobsController.GetJob));
        createdResult.RouteValues.Should().ContainKey("jobId");
        createdResult.RouteValues!["jobId"].Should().Be(request.Id);
        createdResult.Value.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task SubmitJob_When_Fails_Returns_BadRequest()
    {
        // Arrange
        var request = JobRequest.Create("test-command");
        var expectedResult = JobSubmissionResult.Failed("No agents available");

        _orchestratorMock
            .Setup(o => o.SubmitJobAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SubmitJob(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task SubmitJob_With_NullRequest_Returns_BadRequest()
    {
        // Act
        var result = await _controller.SubmitJob(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Get Job Tests

    [Fact]
    public async Task GetJob_With_ExistingJobId_Returns_Ok()
    {
        // Arrange
        var jobId = "job-123";
        var expectedJob = new Job
        {
            Id = jobId,
            Request = JobRequest.Create("test-command") with { Id = jobId },
            Status = JobStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _orchestratorMock
            .Setup(o => o.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJob);

        // Act
        var result = await _controller.GetJob(jobId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedJob);
    }

    [Fact]
    public async Task GetJob_With_NonExistingJobId_Returns_NotFound()
    {
        // Arrange
        var jobId = "non-existing";

        _orchestratorMock
            .Setup(o => o.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        // Act
        var result = await _controller.GetJob(jobId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetJob_With_EmptyJobId_Returns_BadRequest()
    {
        // Act
        var result = await _controller.GetJob(string.Empty);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region List Jobs Tests

    [Fact]
    public async Task ListJobs_Returns_All_Jobs()
    {
        // Arrange
        var jobs = new List<Job>
        {
            new() { Id = "job-1", Request = JobRequest.Create("cmd1") with { Id = "job-1" }, Status = JobStatus.Pending, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "job-2", Request = JobRequest.Create("cmd2") with { Id = "job-2" }, Status = JobStatus.Running, CreatedAt = DateTimeOffset.UtcNow }
        };

        _orchestratorMock
            .Setup(o => o.GetJobsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        var result = await _controller.ListJobs(null, null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(jobs);
    }

    [Fact]
    public async Task ListJobs_With_StatusFilter_Returns_Filtered_Jobs()
    {
        // Arrange
        var pendingJobs = new List<Job>
        {
            new() { Id = "job-1", Request = JobRequest.Create("cmd1") with { Id = "job-1" }, Status = JobStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }
        };

        _orchestratorMock
            .Setup(o => o.GetJobsAsync(JobStatus.Pending, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingJobs);

        // Act
        var result = await _controller.ListJobs(JobStatus.Pending, null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(pendingJobs);
    }

    [Fact]
    public async Task ListJobs_With_AgentFilter_Returns_Filtered_Jobs()
    {
        // Arrange
        var agentJobs = new List<Job>
        {
            new() { Id = "job-1", Request = JobRequest.Create("cmd1") with { Id = "job-1" }, Status = JobStatus.Running, AssignedAgentId = "agent-1", CreatedAt = DateTimeOffset.UtcNow }
        };

        _orchestratorMock
            .Setup(o => o.GetJobsAsync(null, "agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentJobs);

        // Act
        var result = await _controller.ListJobs(null, "agent-1");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(agentJobs);
    }

    #endregion

    #region Cancel Job Tests

    [Fact]
    public async Task CancelJob_With_ExistingJobId_Returns_Ok()
    {
        // Arrange
        var jobId = "job-123";

        _orchestratorMock
            .Setup(o => o.CancelJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelJob(jobId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<CancelJobResponse>().Subject;
        response.JobId.Should().Be(jobId);
        response.Cancelled.Should().BeTrue();
    }

    [Fact]
    public async Task CancelJob_When_NotCancellable_Returns_Ok_With_False()
    {
        // Arrange
        var jobId = "completed-job";

        _orchestratorMock
            .Setup(o => o.CancelJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CancelJob(jobId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<CancelJobResponse>().Subject;
        response.JobId.Should().Be(jobId);
        response.Cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task CancelJob_With_EmptyJobId_Returns_BadRequest()
    {
        // Act
        var result = await _controller.CancelJob(string.Empty);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}

public class AgentsControllerTests
{
    private readonly Mock<IAgentRegistry> _registryMock;
    private readonly AgentsController _controller;

    public AgentsControllerTests()
    {
        _registryMock = new Mock<IAgentRegistry>();
        _controller = new AgentsController(_registryMock.Object);
    }

    #region List Agents Tests

    [Fact]
    public async Task ListAgents_Returns_All_Agents()
    {
        // Arrange
        var agents = new List<AgentInfo>
        {
            CreateAgent("agent-1", "Agent 1"),
            CreateAgent("agent-2", "Agent 2")
        };

        _registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        // Act
        var result = await _controller.ListAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(agents);
    }

    [Fact]
    public async Task ListAgents_When_Empty_Returns_Empty_List()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentInfo>());

        // Act
        var result = await _controller.ListAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agents = okResult.Value.Should().BeAssignableTo<IEnumerable<AgentInfo>>().Subject;
        agents.Should().BeEmpty();
    }

    #endregion

    #region Get Agent Tests

    [Fact]
    public async Task GetAgent_With_ExistingId_Returns_Ok()
    {
        // Arrange
        var agentId = "agent-123";
        var agent = CreateAgent(agentId, "Test Agent");

        _registryMock
            .Setup(r => r.GetAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // Act
        var result = await _controller.GetAgent(agentId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(agent);
    }

    [Fact]
    public async Task GetAgent_With_NonExistingId_Returns_NotFound()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetAsync("non-existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentInfo?)null);

        // Act
        var result = await _controller.GetAgent("non-existing");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAgent_With_EmptyId_Returns_BadRequest()
    {
        // Act
        var result = await _controller.GetAgent(string.Empty);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Helper Methods

    private static AgentInfo CreateAgent(string id, string name)
    {
        return new AgentInfo
        {
            Id = id,
            Name = name,
            Capabilities = [new AgentCapability { Name = "test" }],
            Status = AgentStatus.Ready,
            ConnectionId = "conn-" + id,
            LastHeartbeat = DateTimeOffset.UtcNow
        };
    }

    #endregion
}
