using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class ProgressServiceTests
{
    #region Interface Tests

    [Fact]
    public void IProgressService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IProgressService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void InMemoryProgressService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(InMemoryProgressService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IProgressService));
    }

    #endregion

    #region Report Progress Tests

    [Fact]
    public async Task ReportProgressAsync_Stores_Progress()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        var progress = JobProgress.Create("job-1", 50, "Halfway there");

        // Act
        await service.ReportProgressAsync(progress);

        // Assert
        var retrieved = await service.GetProgressAsync("job-1");
        retrieved.Should().NotBeNull();
        retrieved!.Percentage.Should().Be(50);
        retrieved.Message.Should().Be("Halfway there");
    }

    [Fact]
    public async Task ReportProgressAsync_Updates_Existing_Progress()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 25, "Quarter done"));

        // Act
        await service.ReportProgressAsync(JobProgress.Create("job-1", 75, "Almost done"));

        // Assert
        var retrieved = await service.GetProgressAsync("job-1");
        retrieved.Should().NotBeNull();
        retrieved!.Percentage.Should().Be(75);
        retrieved.Message.Should().Be("Almost done");
    }

    [Fact]
    public async Task ReportProgressAsync_Stores_History()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 25, "Step 1"));
        await service.ReportProgressAsync(JobProgress.Create("job-1", 50, "Step 2"));
        await service.ReportProgressAsync(JobProgress.Create("job-1", 75, "Step 3"));

        // Act
        var history = await service.GetProgressHistoryAsync("job-1");

        // Assert
        history.Should().HaveCount(3);
        history[0].Percentage.Should().Be(25);
        history[1].Percentage.Should().Be(50);
        history[2].Percentage.Should().Be(75);
    }

    #endregion

    #region Get Progress Tests

    [Fact]
    public async Task GetProgressAsync_Returns_Null_For_Unknown_Job()
    {
        // Arrange
        using var service = new InMemoryProgressService();

        // Act
        var result = await service.GetProgressAsync("unknown-job");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProgressHistoryAsync_Returns_Empty_For_Unknown_Job()
    {
        // Arrange
        using var service = new InMemoryProgressService();

        // Act
        var result = await service.GetProgressHistoryAsync("unknown-job");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Clear Progress Tests

    [Fact]
    public async Task ClearProgressAsync_Removes_Job_Progress()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 100, "Done"));

        // Act
        await service.ClearProgressAsync("job-1");

        // Assert
        var result = await service.GetProgressAsync("job-1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearProgressAsync_Does_Not_Affect_Other_Jobs()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 50, "Job 1"));
        await service.ReportProgressAsync(JobProgress.Create("job-2", 75, "Job 2"));

        // Act
        await service.ClearProgressAsync("job-1");

        // Assert
        var job2 = await service.GetProgressAsync("job-2");
        job2.Should().NotBeNull();
        job2!.Percentage.Should().Be(75);
    }

    #endregion

    #region Subscription Tests

    [Fact]
    public async Task Subscribe_Receives_Progress_Updates()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        var receivedProgress = new List<JobProgress>();

        // Act
        var subscription = service.Subscribe("job-1", progress =>
        {
            receivedProgress.Add(progress);
            return Task.CompletedTask;
        });

        await service.ReportProgressAsync(JobProgress.Create("job-1", 25));
        await service.ReportProgressAsync(JobProgress.Create("job-1", 50));
        await service.ReportProgressAsync(JobProgress.Create("job-1", 75));

        // Assert
        receivedProgress.Should().HaveCount(3);
        receivedProgress.Select(p => p.Percentage).Should().BeEquivalentTo([25, 50, 75]);

        subscription.Dispose();
    }

    [Fact]
    public async Task Subscribe_Only_Receives_Relevant_Job_Updates()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        var receivedProgress = new List<JobProgress>();

        // Act
        var subscription = service.Subscribe("job-1", progress =>
        {
            receivedProgress.Add(progress);
            return Task.CompletedTask;
        });

        await service.ReportProgressAsync(JobProgress.Create("job-1", 50));
        await service.ReportProgressAsync(JobProgress.Create("job-2", 100));

        // Assert
        receivedProgress.Should().HaveCount(1);
        receivedProgress[0].JobId.Should().Be("job-1");

        subscription.Dispose();
    }

    [Fact]
    public async Task Unsubscribe_Stops_Receiving_Updates()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        var receivedProgress = new List<JobProgress>();

        var subscription = service.Subscribe("job-1", progress =>
        {
            receivedProgress.Add(progress);
            return Task.CompletedTask;
        });

        await service.ReportProgressAsync(JobProgress.Create("job-1", 25));

        // Act
        subscription.Dispose();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 50));

        // Assert
        receivedProgress.Should().HaveCount(1);
        receivedProgress[0].Percentage.Should().Be(25);
    }

    [Fact]
    public async Task Multiple_Subscribers_Receive_Same_Updates()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        var receivedBySubscriber1 = new List<JobProgress>();
        var receivedBySubscriber2 = new List<JobProgress>();

        // Act
        var sub1 = service.Subscribe("job-1", p => { receivedBySubscriber1.Add(p); return Task.CompletedTask; });
        var sub2 = service.Subscribe("job-1", p => { receivedBySubscriber2.Add(p); return Task.CompletedTask; });

        await service.ReportProgressAsync(JobProgress.Create("job-1", 50));

        // Assert
        receivedBySubscriber1.Should().HaveCount(1);
        receivedBySubscriber2.Should().HaveCount(1);

        sub1.Dispose();
        sub2.Dispose();
    }

    #endregion

    #region History Limit Tests

    [Fact]
    public async Task ReportProgressAsync_Limits_History_Size()
    {
        // Arrange
        using var service = new InMemoryProgressService(maxHistorySize: 3);

        // Act - Report more than max history size
        for (int i = 1; i <= 5; i++)
        {
            await service.ReportProgressAsync(JobProgress.Create("job-1", i * 20));
        }

        // Assert - Only last 3 entries should be kept
        var history = await service.GetProgressHistoryAsync("job-1");
        history.Should().HaveCount(3);
        history[0].Percentage.Should().Be(60);
        history[1].Percentage.Should().Be(80);
        history[2].Percentage.Should().Be(100);
    }

    #endregion

    #region All Progress Tests

    [Fact]
    public async Task GetAllProgressAsync_Returns_All_Active_Jobs()
    {
        // Arrange
        using var service = new InMemoryProgressService();
        await service.ReportProgressAsync(JobProgress.Create("job-1", 25));
        await service.ReportProgressAsync(JobProgress.Create("job-2", 50));
        await service.ReportProgressAsync(JobProgress.Create("job-3", 75));

        // Act
        var all = await service.GetAllProgressAsync();

        // Assert
        all.Should().HaveCount(3);
        all.Select(p => p.JobId).Should().Contain(["job-1", "job-2", "job-3"]);
    }

    #endregion
}
