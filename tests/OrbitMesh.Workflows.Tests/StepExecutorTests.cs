using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrbitMesh.Workflows.Execution;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for individual step executors.
/// </summary>
public class StepExecutorTests
{
    [Fact]
    public async Task DelayStepExecutor_ExecutesDelay_ReturnsCompleted()
    {
        // Arrange
        var executor = new DelayStepExecutor();
        var context = CreateContext(StepType.Delay, new DelayStepConfig
        {
            Duration = TimeSpan.FromMilliseconds(10)
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
    }

    [Fact]
    public async Task DelayStepExecutor_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var executor = new DelayStepExecutor();
        var context = CreateContext(StepType.Delay, new DelayStepConfig
        {
            Duration = TimeSpan.FromSeconds(10)
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => executor.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task TransformStepExecutor_TransformsData_ReturnsOutput()
    {
        // Arrange
        var expressionEvaluator = new SimpleExpressionEvaluator();
        var executor = new TransformStepExecutor(expressionEvaluator);

        var context = CreateContext(StepType.Transform, new TransformStepConfig
        {
            Expression = "greeting"
        });

        context.Variables["greeting"] = "Hello, World!";

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task WaitForEventStepExecutor_WaitsForEvent_ReturnsWaitingStatus()
    {
        // Arrange
        var executor = new WaitForEventStepExecutor();
        var context = CreateContext(StepType.WaitForEvent, new WaitForEventStepConfig
        {
            EventType = "OrderApproved",
            Timeout = TimeSpan.FromMinutes(30)
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.WaitingForEvent);
    }

    [Fact]
    public async Task ApprovalStepExecutor_WaitsForApproval_ReturnsWaitingStatus()
    {
        // Arrange
        var notifier = new Mock<IApprovalNotifier>();
        notifier.Setup(n => n.NotifyApproversAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var executor = new ApprovalStepExecutor(notifier.Object);
        var context = CreateContext(StepType.Approval, new ApprovalStepConfig
        {
            Approvers = ["Manager"],
            Message = "Please approve this request"
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.WaitingForApproval);
    }

    [Fact]
    public async Task JobStepExecutor_DispatchesJob_ReturnsCompleted()
    {
        // Arrange
        var dispatcher = new Mock<IJobDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobDispatchResult
            {
                Success = true,
                JobId = "job-123",
                JobResult = new { status = "completed" }
            });

        var expressionEvaluator = new SimpleExpressionEvaluator();
        var logger = Mock.Of<ILogger<JobStepExecutor>>();

        var executor = new JobStepExecutor(dispatcher.Object, expressionEvaluator, logger);
        var context = CreateContext(StepType.Job, new JobStepConfig
        {
            Command = "ProcessOrder",
            Pattern = "worker-*",
            Priority = 5
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.JobId.Should().Be("job-123");
    }

    [Fact]
    public async Task JobStepExecutor_DispatchFails_ReturnsFailed()
    {
        // Arrange
        var dispatcher = new Mock<IJobDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobDispatchResult
            {
                Success = false,
                Error = "No agents available"
            });

        var expressionEvaluator = new SimpleExpressionEvaluator();
        var logger = Mock.Of<ILogger<JobStepExecutor>>();

        var executor = new JobStepExecutor(dispatcher.Object, expressionEvaluator, logger);
        var context = CreateContext(StepType.Job, new JobStepConfig
        {
            Command = "ProcessOrder",
            Pattern = "worker-*",
            Priority = 5
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Failed);
        result.Error.Should().Be("No agents available");
    }

    [Fact]
    public async Task ConditionalStepExecutor_TrueCondition_ExecutesThenBranch()
    {
        // Arrange
        var expressionEvaluator = new SimpleExpressionEvaluator();
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var executor = new ConditionalStepExecutor(executorFactory, expressionEvaluator);

        var context = CreateContext(StepType.Conditional, new ConditionalStepConfig
        {
            Expression = "shouldProcess",
            ThenBranch =
            [
                new WorkflowStep
                {
                    Id = "then-step",
                    Name = "Then Step",
                    Type = StepType.Transform,
                    Config = new TransformStepConfig { Expression = "'processed'" }
                }
            ]
        });

        context.Variables["shouldProcess"] = true;

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task ConditionalStepExecutor_FalseCondition_ExecutesElseBranch()
    {
        // Arrange
        var expressionEvaluator = new SimpleExpressionEvaluator();
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var executor = new ConditionalStepExecutor(executorFactory, expressionEvaluator);

        var context = CreateContext(StepType.Conditional, new ConditionalStepConfig
        {
            Expression = "shouldProcess",
            ThenBranch =
            [
                new WorkflowStep
                {
                    Id = "then-step",
                    Name = "Then Step",
                    Type = StepType.Transform,
                    Config = new TransformStepConfig { Expression = "'processed'" }
                }
            ],
            ElseBranch =
            [
                new WorkflowStep
                {
                    Id = "else-step",
                    Name = "Else Step",
                    Type = StepType.Transform,
                    Config = new TransformStepConfig { Expression = "'skipped'" }
                }
            ]
        });

        context.Variables["shouldProcess"] = false;

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.Output.Should().NotBeNull();
    }

    [Fact]
    public async Task ForEachStepExecutor_IteratesCollection_ExecutesForEachItem()
    {
        // Arrange
        var expressionEvaluator = new SimpleExpressionEvaluator();
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed(new { processed = true }));
        var executor = new ForEachStepExecutor(executorFactory, expressionEvaluator);

        var context = CreateContext(StepType.ForEach, new ForEachStepConfig
        {
            Collection = "items",
            ItemVariable = "item",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "process-item",
                    Name = "Process Item",
                    Type = StepType.Transform,
                    Config = new TransformStepConfig { Expression = "item" }
                }
            ]
        });

        context.Variables["items"] = new List<object> { "item1", "item2", "item3" };

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.Branches.Should().NotBeNull();
        result.Branches!.Count.Should().Be(3);
    }

    [Fact]
    public async Task NotifyStepExecutor_SendsNotification_ReturnsCompleted()
    {
        // Arrange
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(
            It.IsAny<NotifyChannel>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expressionEvaluator = new SimpleExpressionEvaluator();
        var executor = new NotifyStepExecutor(sender.Object, expressionEvaluator);
        var context = CreateContext(StepType.Notify, new NotifyStepConfig
        {
            Channel = NotifyChannel.Email,
            Target = "user@example.com",
            Message = "Order ORD-123 confirmed"
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
    }

    [Fact]
    public async Task SubWorkflowStepExecutor_LaunchesSubWorkflow_ReturnsCompleted()
    {
        // Arrange
        var launcher = new Mock<ISubWorkflowLauncher>();
        launcher.Setup(l => l.LaunchAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyDictionary<string, object?>?>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubWorkflowResult
            {
                Success = true,
                SubWorkflowInstanceId = "sub-workflow-instance-123",
                Output = new { completed = true }
            });

        var executor = new SubWorkflowStepExecutor(launcher.Object);
        var context = CreateContext(StepType.SubWorkflow, new SubWorkflowStepConfig
        {
            WorkflowId = "child-workflow",
            Version = "1.0.0"
        });

        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(StepStatus.Completed);
        result.SubWorkflowInstanceId.Should().Be("sub-workflow-instance-123");
    }

    [Fact]
    public void StepExecutorFactory_CreatesCorrectExecutor_ForEachStepType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IExpressionEvaluator, SimpleExpressionEvaluator>();
        services.AddSingleton(Mock.Of<IJobDispatcher>());
        services.AddSingleton(Mock.Of<INotificationSender>());
        services.AddSingleton(Mock.Of<ISubWorkflowLauncher>());
        services.AddSingleton(Mock.Of<IApprovalNotifier>());
        services.AddSingleton(Mock.Of<ILogger<JobStepExecutor>>());
        services.AddSingleton<IStepExecutorFactory>(sp => new StepExecutorFactory(sp));

        services.AddSingleton<JobStepExecutor>();
        services.AddSingleton<DelayStepExecutor>();
        services.AddSingleton<TransformStepExecutor>();
        services.AddSingleton<WaitForEventStepExecutor>();
        services.AddSingleton<ApprovalStepExecutor>();
        services.AddSingleton<ParallelStepExecutor>();
        services.AddSingleton<ConditionalStepExecutor>();
        services.AddSingleton<ForEachStepExecutor>();
        services.AddSingleton<SubWorkflowStepExecutor>();
        services.AddSingleton<NotifyStepExecutor>();

        var provider = services.BuildServiceProvider();
        var factory = new StepExecutorFactory(provider);

        // Act & Assert
        factory.Create(StepType.Job).Should().BeOfType<JobStepExecutor>();
        factory.Create(StepType.Delay).Should().BeOfType<DelayStepExecutor>();
        factory.Create(StepType.Transform).Should().BeOfType<TransformStepExecutor>();
        factory.Create(StepType.WaitForEvent).Should().BeOfType<WaitForEventStepExecutor>();
        factory.Create(StepType.Approval).Should().BeOfType<ApprovalStepExecutor>();
        factory.Create(StepType.Parallel).Should().BeOfType<ParallelStepExecutor>();
        factory.Create(StepType.Conditional).Should().BeOfType<ConditionalStepExecutor>();
        factory.Create(StepType.ForEach).Should().BeOfType<ForEachStepExecutor>();
        factory.Create(StepType.SubWorkflow).Should().BeOfType<SubWorkflowStepExecutor>();
        factory.Create(StepType.Notify).Should().BeOfType<NotifyStepExecutor>();
    }

    private static StepExecutionContext CreateContext(StepType stepType, StepConfig config)
    {
        var instance = new WorkflowInstance
        {
            Id = "test-instance",
            WorkflowId = "test-workflow",
            WorkflowVersion = "1.0.0",
            Status = WorkflowStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var step = new WorkflowStep
        {
            Id = "test-step",
            Name = "Test Step",
            Type = stepType,
            Config = config
        };

        var stepInstance = new StepInstance
        {
            StepId = "test-step",
            Status = StepStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        return new StepExecutionContext
        {
            WorkflowInstance = instance,
            Step = step,
            StepInstance = stepInstance,
            Variables = new Dictionary<string, object?>()
        };
    }

    private static IStepExecutorFactory CreateMockExecutorFactory(StepExecutionResult result)
    {
        var executorFactory = new Mock<IStepExecutorFactory>();
        var executor = new Mock<IStepExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<StepExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        executorFactory.Setup(f => f.Create(It.IsAny<StepType>()))
            .Returns(executor.Object);
        return executorFactory.Object;
    }
}
