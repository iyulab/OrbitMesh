using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Execution;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for the workflow execution engine.
/// </summary>
public class WorkflowEngineTests
{
    private readonly InMemoryWorkflowInstanceStore _instanceStore = new();
    private readonly InMemoryWorkflowRegistry _registry = new();
    private readonly SimpleExpressionEvaluator _expressionEvaluator = new();
    private readonly NullLogger<WorkflowEngine> _logger = new();

    [Fact]
    public async Task StartAsync_SimpleWorkflow_CompletesSuccessfully()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "First Step",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "TestCommand" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.WorkflowId.Should().Be("test-workflow");
        instance.StepInstances!["step1"].Status.Should().Be(StepStatus.Completed);
    }

    [Fact]
    public async Task StartAsync_WorkflowWithInput_PassesInputToVariables()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "input-workflow",
            Name = "Input Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Step 1",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "TestCommand" }
                }
            ]
        };

        var input = new Dictionary<string, object?>
        {
            ["customerId"] = "123",
            ["amount"] = 100.50
        };

        // Act
        var instance = await engine.StartAsync(workflow, input);

        // Assert
        instance.Variables.Should().ContainKey("customerId");
        instance.Variables!["customerId"].Should().Be("123");
        instance.Variables["amount"].Should().Be(100.50);
    }

    [Fact]
    public async Task StartAsync_WorkflowWithDependencies_ExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var executorFactory = CreateTrackingExecutorFactory(executionOrder);
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "dep-workflow",
            Name = "Dependency Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Step 1",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "Cmd1" }
                },
                new WorkflowStep
                {
                    Id = "step2",
                    Name = "Step 2",
                    Type = StepType.Job,
                    DependsOn = ["step1"],
                    Config = new JobStepConfig { Command = "Cmd2" }
                },
                new WorkflowStep
                {
                    Id = "step3",
                    Name = "Step 3",
                    Type = StepType.Job,
                    DependsOn = ["step2"],
                    Config = new JobStepConfig { Command = "Cmd3" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Completed);
        executionOrder.Should().Equal("step1", "step2", "step3");
    }

    [Fact]
    public async Task StartAsync_StepWithFalseCondition_SkipsStep()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "cond-workflow",
            Name = "Conditional Workflow",
            Version = "1.0.0",
            Variables = new Dictionary<string, object?>
            {
                ["shouldRun"] = false
            },
            Steps =
            [
                new WorkflowStep
                {
                    Id = "conditional-step",
                    Name = "Conditional Step",
                    Type = StepType.Job,
                    Condition = "shouldRun",
                    Config = new JobStepConfig { Command = "ConditionalCommand" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.StepInstances!["conditional-step"].Status.Should().Be(StepStatus.Skipped);
    }

    [Fact]
    public async Task StartAsync_StepFails_WorkflowFails()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Failed("Test error"));
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "fail-workflow",
            Name = "Failing Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "failing-step",
                    Name = "Failing Step",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "FailingCommand" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.Error.Should().Be("Test error");
        instance.StepInstances!["failing-step"].Status.Should().Be(StepStatus.Failed);
    }

    [Fact]
    public async Task StartAsync_StepWaitsForEvent_WorkflowPauses()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.WaitingForEvent());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "event-workflow",
            Name = "Event Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "wait-step",
                    Name = "Wait Step",
                    Type = StepType.WaitForEvent,
                    Config = new WaitForEventStepConfig { EventType = "ApprovalReceived" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Status.Should().Be(WorkflowStatus.Paused);
        instance.StepInstances!["wait-step"].Status.Should().Be(StepStatus.WaitingForEvent);
    }

    [Fact]
    public async Task CancelAsync_RunningWorkflow_CancelsWorkflow()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.WaitingForEvent());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "cancel-workflow",
            Name = "Cancel Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "wait-step",
                    Name = "Wait Step",
                    Type = StepType.WaitForEvent,
                    Config = new WaitForEventStepConfig { EventType = "SomeEvent" }
                }
            ]
        };

        var instance = await engine.StartAsync(workflow);

        // Act
        var cancelledInstance = await engine.CancelAsync(instance.Id, "User cancelled");

        // Assert
        cancelledInstance.Status.Should().Be(WorkflowStatus.Cancelled);
        cancelledInstance.Error.Should().Be("User cancelled");
        cancelledInstance.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInstanceAsync_ExistingInstance_ReturnsInstance()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "get-workflow",
            Name = "Get Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Step 1",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "TestCommand" }
                }
            ]
        };

        var instance = await engine.StartAsync(workflow);

        // Act
        var retrievedInstance = await engine.GetInstanceAsync(instance.Id);

        // Assert
        retrievedInstance.Should().NotBeNull();
        retrievedInstance!.Id.Should().Be(instance.Id);
        retrievedInstance.WorkflowId.Should().Be("get-workflow");
    }

    [Fact]
    public async Task GetInstanceAsync_NonExistentInstance_ReturnsNull()
    {
        // Arrange
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed());
        var engine = CreateEngine(executorFactory);

        // Act
        var instance = await engine.GetInstanceAsync("non-existent-id");

        // Assert
        instance.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_StepWithOutputVariable_StoresOutput()
    {
        // Arrange
        var outputValue = new { result = "success", data = 42 };
        var executorFactory = CreateMockExecutorFactory(StepExecutionResult.Completed(outputValue));
        var engine = CreateEngine(executorFactory);

        var workflow = new WorkflowDefinition
        {
            Id = "output-workflow",
            Name = "Output Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Step 1",
                    Type = StepType.Job,
                    OutputVariable = "stepResult",
                    Config = new JobStepConfig { Command = "TestCommand" }
                }
            ]
        };

        // Act
        var instance = await engine.StartAsync(workflow);

        // Assert
        instance.Variables.Should().ContainKey("stepResult");
        instance.Variables!["stepResult"].Should().Be(outputValue);
    }

    private WorkflowEngine CreateEngine(IStepExecutorFactory executorFactory)
    {
        return new WorkflowEngine(
            _instanceStore,
            _registry,
            executorFactory,
            _expressionEvaluator,
            _logger);
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

    private static IStepExecutorFactory CreateTrackingExecutorFactory(List<string> executionOrder)
    {
        var executorFactory = new Mock<IStepExecutorFactory>();
        executorFactory.Setup(f => f.Create(It.IsAny<StepType>()))
            .Returns(() =>
            {
                var executor = new Mock<IStepExecutor>();
                executor.Setup(e => e.ExecuteAsync(It.IsAny<StepExecutionContext>(), It.IsAny<CancellationToken>()))
                    .Returns<StepExecutionContext, CancellationToken>((ctx, _) =>
                    {
                        executionOrder.Add(ctx.Step.Id);
                        return Task.FromResult(StepExecutionResult.Completed());
                    });
                return executor.Object;
            });
        return executorFactory.Object;
    }
}
