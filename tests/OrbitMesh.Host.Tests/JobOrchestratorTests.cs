using Moq;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class JobOrchestratorTests
{
    #region Interface Tests

    [Fact]
    public void IJobOrchestrator_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IJobOrchestrator);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void JobOrchestrator_Implements_Interface()
    {
        // Arrange & Act
        var orchestratorType = typeof(JobOrchestrator);

        // Assert
        orchestratorType.GetInterfaces().Should().Contain(typeof(IJobOrchestrator));
    }

    #endregion

    #region Submit Job Tests

    [Fact]
    public async Task SubmitJobAsync_Creates_Job_And_Returns_Id()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var request = JobRequest.Create("test-command");

        // Act
        var result = await orchestrator.SubmitJobAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().NotBeNullOrEmpty();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitJobAsync_Stores_Job_In_Manager()
    {
        // Arrange
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.EnqueueAsync(It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobRequest r, CancellationToken _) => Job.FromRequest(r));

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);
        var request = JobRequest.Create("test-command");

        // Act
        await orchestrator.SubmitJobAsync(request);

        // Assert
        jobManager.Verify(m => m.EnqueueAsync(
            It.Is<JobRequest>(r => r.Command == "test-command"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitJobAsync_Uses_IdempotencyKey_When_Provided()
    {
        // Arrange
        var idempotencyService = new Mock<IIdempotencyService>();
        idempotencyService.Setup(s => s.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = CreateOrchestrator(idempotencyService: idempotencyService.Object);
        var request = JobRequest.Create("test-command") with { IdempotencyKey = "unique-key" };

        // Act
        await orchestrator.SubmitJobAsync(request);

        // Assert
        idempotencyService.Verify(s => s.TryAcquireLockAsync("unique-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitJobAsync_Returns_Existing_Result_For_Duplicate_IdempotencyKey()
    {
        // Arrange
        var idempotencyService = new Mock<IIdempotencyService>();
        idempotencyService.Setup(s => s.TryAcquireLockAsync("duplicate-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        idempotencyService.Setup(s => s.GetResultAsync<JobSubmissionResult>("duplicate-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobSubmissionResult { JobId = "existing-job", Success = true });

        var orchestrator = CreateOrchestrator(idempotencyService: idempotencyService.Object);
        var request = JobRequest.Create("test-command") with { IdempotencyKey = "duplicate-key" };

        // Act
        var result = await orchestrator.SubmitJobAsync(request);

        // Assert
        result.JobId.Should().Be("existing-job");
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Dispatch Job Tests

    [Fact]
    public async Task SubmitJobAsync_Dispatches_Job_When_Agent_Available()
    {
        // Arrange
        var agent = CreateTestAgent("agent-1", "test-command");
        var router = new Mock<IAgentRouter>();
        router.Setup(r => r.SelectAgentAsync(It.IsAny<RoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var dispatcher = new Mock<IJobDispatcher>();
        var orchestrator = CreateOrchestrator(router: router.Object, dispatcher: dispatcher.Object);
        var request = JobRequest.Create("test-command");

        // Act
        await orchestrator.SubmitJobAsync(request);

        // Assert
        dispatcher.Verify(d => d.DispatchAsync(
            It.IsAny<Job>(),
            agent,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitJobAsync_Queues_Job_When_No_Agent_Available()
    {
        // Arrange
        var router = new Mock<IAgentRouter>();
        router.Setup(r => r.SelectAgentAsync(It.IsAny<RoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentInfo?)null);

        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.EnqueueAsync(It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobRequest r, CancellationToken _) => Job.FromRequest(r));

        var orchestrator = CreateOrchestrator(router: router.Object, jobManager: jobManager.Object);
        var request = JobRequest.Create("test-command");

        // Act
        var result = await orchestrator.SubmitJobAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Status.Should().Be(JobStatus.Pending);
    }

    #endregion

    #region Handle Result Tests

    [Fact]
    public async Task HandleResultAsync_Updates_Job_Status()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command");
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);
        var result = JobResult.Success("job-1", "agent-1", null);

        // Act
        await orchestrator.HandleResultAsync(result);

        // Assert
        jobManager.Verify(m => m.CompleteAsync(
            "job-1",
            It.Is<JobResult>(r => r.Status == JobStatus.Completed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleResultAsync_Retries_Failed_Job_Within_Limit()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command", retryCount: 1, maxRetries: 3);
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);
        var result = JobResult.Failure("job-1", "agent-1", "Transient error", "ERR001");

        // Act
        await orchestrator.HandleResultAsync(result);

        // Assert
        jobManager.Verify(m => m.RequeueAsync("job-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleResultAsync_Sends_To_DLQ_After_MaxRetries()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command", retryCount: 3, maxRetries: 3);
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var deadLetterService = new Mock<IDeadLetterService>();
        var orchestrator = CreateOrchestrator(
            jobManager: jobManager.Object,
            deadLetterService: deadLetterService.Object);

        var result = JobResult.Failure("job-1", "agent-1", "Persistent failure", "ERR001");

        // Act
        await orchestrator.HandleResultAsync(result);

        // Assert
        deadLetterService.Verify(d => d.EnqueueAsync(
            It.Is<Job>(j => j.Id == "job-1"),
            It.Is<string>(s => s.Contains("Max retries")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Handle Progress Tests

    [Fact]
    public async Task HandleProgressAsync_Updates_Progress_Service()
    {
        // Arrange
        var progressService = new Mock<IProgressService>();
        var orchestrator = CreateOrchestrator(progressService: progressService.Object);
        var progress = JobProgress.Create("job-1", 50, "Halfway");

        // Act
        await orchestrator.HandleProgressAsync(progress);

        // Assert
        progressService.Verify(p => p.ReportProgressAsync(
            It.Is<JobProgress>(pr => pr.JobId == "job-1" && pr.Percentage == 50),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cancel Job Tests

    [Fact]
    public async Task CancelJobAsync_Sends_Cancel_To_Agent()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command") with
        {
            Status = JobStatus.Running,
            AssignedAgentId = "agent-1"
        };
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dispatcher = new Mock<IJobDispatcher>();
        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object, dispatcher: dispatcher.Object);

        // Act
        var result = await orchestrator.CancelJobAsync("job-1");

        // Assert
        result.Should().BeTrue();
        dispatcher.Verify(d => d.SendCancelToAgentAsync("job-1", "agent-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelJobAsync_Returns_False_For_Completed_Job()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command") with { Status = JobStatus.Completed };
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);

        // Act
        var result = await orchestrator.CancelJobAsync("job-1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Get Job Tests

    [Fact]
    public async Task GetJobAsync_Returns_Job_From_Manager()
    {
        // Arrange
        var job = CreateTestJob("job-1", "test-command");
        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);

        // Act
        var result = await orchestrator.GetJobAsync("job-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("job-1");
    }

    [Fact]
    public async Task GetJobsAsync_Returns_Jobs_With_Filter()
    {
        // Arrange
        var jobs = new List<Job>
        {
            CreateTestJob("job-1", "cmd1") with { Status = JobStatus.Running },
            CreateTestJob("job-2", "cmd2") with { Status = JobStatus.Completed }
        };

        var jobManager = new Mock<IJobManager>();
        jobManager.Setup(m => m.GetJobsAsync(JobStatus.Running, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs.Where(j => j.Status == JobStatus.Running).ToList());

        var orchestrator = CreateOrchestrator(jobManager: jobManager.Object);

        // Act
        var result = await orchestrator.GetJobsAsync(status: JobStatus.Running);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("job-1");
    }

    #endregion

    #region Helper Methods

    private static JobOrchestrator CreateOrchestrator(
        IJobManager? jobManager = null,
        IJobDispatcher? dispatcher = null,
        IAgentRouter? router = null,
        IIdempotencyService? idempotencyService = null,
        IDeadLetterService? deadLetterService = null,
        IProgressService? progressService = null,
        IResilienceService? resilienceService = null)
    {
        return new JobOrchestrator(
            jobManager ?? CreateDefaultJobManager(),
            dispatcher ?? new Mock<IJobDispatcher>().Object,
            router ?? CreateDefaultRouter(),
            idempotencyService ?? CreateDefaultIdempotencyService(),
            deadLetterService ?? new Mock<IDeadLetterService>().Object,
            progressService ?? new Mock<IProgressService>().Object,
            resilienceService ?? CreateDefaultResilienceService());
    }

    private static IIdempotencyService CreateDefaultIdempotencyService()
    {
        var mock = new Mock<IIdempotencyService>();
        mock.Setup(s => s.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock.Object;
    }

    private static IJobManager CreateDefaultJobManager()
    {
        var mock = new Mock<IJobManager>();
        mock.Setup(m => m.EnqueueAsync(It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobRequest r, CancellationToken _) => Job.FromRequest(r));
        return mock.Object;
    }

    private static IAgentRouter CreateDefaultRouter()
    {
        var mock = new Mock<IAgentRouter>();
        mock.Setup(r => r.SelectAgentAsync(It.IsAny<RoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestAgent("agent-1", "test-command"));
        return mock.Object;
    }

    private static IResilienceService CreateDefaultResilienceService()
    {
        var mock = new Mock<IResilienceService>();
        mock.Setup(r => r.ExecuteWithResilienceAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<JobSubmissionResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<JobSubmissionResult>> op, CancellationToken ct) => op(ct));
        return mock.Object;
    }

    private static AgentInfo CreateTestAgent(string id, params string[] capabilities)
    {
        return new AgentInfo
        {
            Id = id,
            Name = $"Agent {id}",
            Capabilities = capabilities.Select(c => new AgentCapability { Name = c }).ToList(),
            Status = AgentStatus.Ready
        };
    }

    private static Job CreateTestJob(string id, string command, int retryCount = 0, int maxRetries = 3)
    {
        return Job.FromRequest(JobRequest.Create(command) with { MaxRetries = maxRetries }) with
        {
            Id = id,
            RetryCount = retryCount
        };
    }

    #endregion
}
