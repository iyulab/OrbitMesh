using OrbitMesh.Core.Models;

namespace OrbitMesh.Agent.Tests;

public class ProgressReporterTests
{
    #region Interface Tests

    [Fact]
    public void IProgressReporter_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IProgressReporter);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    #endregion

    #region Progress Reporter Tests

    [Fact]
    public async Task ProgressReporter_ReportProgress_Invokes_Callback()
    {
        // Arrange
        var reportedProgress = new List<JobProgress>();
        var reporter = new ProgressReporter("job-1", async progress =>
        {
            reportedProgress.Add(progress);
            await Task.CompletedTask;
        });

        // Act
        await reporter.ReportAsync(50, "Halfway");

        // Assert
        reportedProgress.Should().HaveCount(1);
        reportedProgress[0].JobId.Should().Be("job-1");
        reportedProgress[0].Percentage.Should().Be(50);
        reportedProgress[0].Message.Should().Be("Halfway");
    }

    [Fact]
    public async Task ProgressReporter_ReportStep_Calculates_Percentage()
    {
        // Arrange
        var reportedProgress = new List<JobProgress>();
        var reporter = new ProgressReporter("job-1", async progress =>
        {
            reportedProgress.Add(progress);
            await Task.CompletedTask;
        });

        // Act
        await reporter.ReportStepAsync(2, 4, "Step 2 of 4");

        // Assert
        reportedProgress.Should().HaveCount(1);
        reportedProgress[0].Percentage.Should().Be(50);
        reportedProgress[0].CurrentStepNumber.Should().Be(2);
        reportedProgress[0].TotalSteps.Should().Be(4);
        reportedProgress[0].CurrentStep.Should().Be("Step 2 of 4");
    }

    [Fact]
    public void ProgressReporter_AsProgress_Returns_IProgress()
    {
        // Arrange
        var reporter = new ProgressReporter("job-1", _ => Task.CompletedTask);

        // Act
        var progress = reporter.AsProgress();

        // Assert
        progress.Should().NotBeNull();
        progress.Should().BeAssignableTo<IProgress<JobProgress>>();
    }

    [Fact]
    public async Task ProgressReporter_AsProgress_Reports_Through_Callback()
    {
        // Arrange
        var reported = new List<JobProgress>();
        var reporter = new ProgressReporter("job-1", async p =>
        {
            reported.Add(p);
            await Task.CompletedTask;
        });

        var progress = reporter.AsProgress();

        // Act
        progress.Report(JobProgress.Create("job-1", 75, "Almost done"));

        // Wait for async callback to complete
        await Task.Delay(50);

        // Assert
        reported.Should().HaveCount(1);
        reported[0].Percentage.Should().Be(75);
    }

    [Fact]
    public async Task ProgressReporter_ReportProgress_Clamps_Percentage()
    {
        // Arrange
        var reportedProgress = new List<JobProgress>();
        var reporter = new ProgressReporter("job-1", async progress =>
        {
            reportedProgress.Add(progress);
            await Task.CompletedTask;
        });

        // Act
        await reporter.ReportAsync(150); // Over 100
        await reporter.ReportAsync(-10); // Under 0

        // Assert
        reportedProgress.Should().HaveCount(2);
        reportedProgress[0].Percentage.Should().Be(100);
        reportedProgress[1].Percentage.Should().Be(0);
    }

    [Fact]
    public async Task ProgressReporter_Multiple_Reports_Maintains_JobId()
    {
        // Arrange
        var reportedProgress = new List<JobProgress>();
        var reporter = new ProgressReporter("my-job-id", async progress =>
        {
            reportedProgress.Add(progress);
            await Task.CompletedTask;
        });

        // Act
        await reporter.ReportAsync(25);
        await reporter.ReportAsync(50);
        await reporter.ReportAsync(75);
        await reporter.ReportAsync(100);

        // Assert
        reportedProgress.Should().HaveCount(4);
        reportedProgress.Should().AllSatisfy(p => p.JobId.Should().Be("my-job-id"));
    }

    #endregion
}
