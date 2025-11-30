using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Agent.Tests;

public class AgentCancellationTests
{
    #region CommandContext Cancellation Tests

    [Fact]
    public void CommandContext_Should_Have_CancellationToken_Property()
    {
        // Arrange
        var request = CreateTestRequest("job-1", "test-command");
        using var cts = new CancellationTokenSource();

        // Act
        var context = CommandContext.FromRequest(request, "agent-1", cts.Token);

        // Assert
        context.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void CommandContext_Default_CancellationToken_Is_None()
    {
        // Arrange
        var request = CreateTestRequest("job-1", "test-command");

        // Act
        var context = CommandContext.FromRequest(request, "agent-1");

        // Assert
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void CommandContext_CancellationToken_IsCancellationRequested_Reflects_Source()
    {
        // Arrange
        var request = CreateTestRequest("job-1", "test-command");
        using var cts = new CancellationTokenSource();

        // Act
        var context = CommandContext.FromRequest(request, "agent-1", cts.Token);
        cts.Cancel();

        // Assert
        context.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region JobCancellationManager Tests

    [Fact]
    public void JobCancellationManager_RegisterJob_Returns_CancellationToken()
    {
        // Arrange
        using var manager = new JobCancellationManager();

        // Act
        var token = manager.RegisterJob("job-1");

        // Assert
        token.Should().NotBe(CancellationToken.None);
        token.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public void JobCancellationManager_CancelJob_Triggers_CancellationToken()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        var token = manager.RegisterJob("job-1");

        // Act
        var result = manager.CancelJob("job-1");

        // Assert
        result.Should().BeTrue();
        token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void JobCancellationManager_CancelJob_Returns_False_For_Unknown_Job()
    {
        // Arrange
        using var manager = new JobCancellationManager();

        // Act
        var result = manager.CancelJob("unknown-job");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void JobCancellationManager_CompleteJob_Removes_Registration()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        manager.RegisterJob("job-1");

        // Act
        manager.CompleteJob("job-1");
        var result = manager.CancelJob("job-1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void JobCancellationManager_IsJobRunning_Returns_True_For_Active_Job()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        manager.RegisterJob("job-1");

        // Act
        var result = manager.IsJobRunning("job-1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void JobCancellationManager_IsJobRunning_Returns_False_After_Completion()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        manager.RegisterJob("job-1");
        manager.CompleteJob("job-1");

        // Act
        var result = manager.IsJobRunning("job-1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void JobCancellationManager_GetRunningJobIds_Returns_All_Active_Jobs()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        manager.RegisterJob("job-1");
        manager.RegisterJob("job-2");
        manager.RegisterJob("job-3");
        manager.CompleteJob("job-2");

        // Act
        var runningJobs = manager.GetRunningJobIds();

        // Assert
        runningJobs.Should().HaveCount(2);
        runningJobs.Should().Contain(["job-1", "job-3"]);
    }

    [Fact]
    public void JobCancellationManager_CancelAllJobs_Cancels_All_Active_Jobs()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        var token1 = manager.RegisterJob("job-1");
        var token2 = manager.RegisterJob("job-2");

        // Act
        var count = manager.CancelAllJobs();

        // Assert
        count.Should().Be(2);
        token1.IsCancellationRequested.Should().BeTrue();
        token2.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void JobCancellationManager_Dispose_Cancels_All_Jobs()
    {
        // Arrange
        var manager = new JobCancellationManager();
        var token1 = manager.RegisterJob("job-1");
        var token2 = manager.RegisterJob("job-2");

        // Act
        manager.Dispose();

        // Assert
        token1.IsCancellationRequested.Should().BeTrue();
        token2.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void JobCancellationManager_RegisterJob_Twice_Returns_Different_Tokens()
    {
        // Arrange
        using var manager = new JobCancellationManager();

        // Act
        var token1 = manager.RegisterJob("job-1");
        manager.CompleteJob("job-1");
        var token2 = manager.RegisterJob("job-1");

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public async Task JobCancellationManager_Thread_Safety_Concurrent_Operations()
    {
        // Arrange
        using var manager = new JobCancellationManager();
        var tasks = new List<Task>();

        // Act - Run concurrent registrations and completions
        for (int i = 0; i < 100; i++)
        {
            var jobId = $"job-{i}";
            tasks.Add(Task.Run(() =>
            {
                manager.RegisterJob(jobId);
                Thread.Sleep(1);
                manager.CompleteJob(jobId);
            }));
        }

        // Assert - Should not throw exceptions
        await Task.WhenAll(tasks);
        manager.GetRunningJobIds().Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static JobRequest CreateTestRequest(string id, string command)
    {
        return JobRequest.Create(command) with { IdempotencyKey = id };
    }

    #endregion
}
