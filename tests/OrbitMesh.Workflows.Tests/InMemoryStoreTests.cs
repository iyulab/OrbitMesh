using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for in-memory workflow stores.
/// </summary>
public class InMemoryStoreTests
{
    [Fact]
    public async Task WorkflowInstanceStore_SaveAndGet_ReturnsInstance()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        var instance = CreateInstance("test-1");

        // Act
        await store.SaveAsync(instance);
        var retrieved = await store.GetAsync("test-1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test-1");
        retrieved.WorkflowId.Should().Be("test-workflow");
    }

    [Fact]
    public async Task WorkflowInstanceStore_Update_UpdatesInstance()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        var instance = CreateInstance("test-2");
        await store.SaveAsync(instance);

        // Act
        var updated = instance with
        {
            Status = WorkflowStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };
        await store.UpdateAsync(updated);

        var retrieved = await store.GetAsync("test-2");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(WorkflowStatus.Completed);
        retrieved.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task WorkflowInstanceStore_Query_FiltersByWorkflowId()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        await store.SaveAsync(CreateInstance("inst-1", "workflow-a"));
        await store.SaveAsync(CreateInstance("inst-2", "workflow-a"));
        await store.SaveAsync(CreateInstance("inst-3", "workflow-b"));

        // Act
        var query = new WorkflowInstanceQuery { WorkflowId = "workflow-a" };
        var results = await store.QueryAsync(query);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.WorkflowId.Should().Be("workflow-a"));
    }

    [Fact]
    public async Task WorkflowInstanceStore_Query_FiltersByStatus()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        await store.SaveAsync(CreateInstance("inst-1") with { Status = WorkflowStatus.Running });
        await store.SaveAsync(CreateInstance("inst-2") with { Status = WorkflowStatus.Completed });
        await store.SaveAsync(CreateInstance("inst-3") with { Status = WorkflowStatus.Running });

        // Act
        var query = new WorkflowInstanceQuery { Status = WorkflowStatus.Running };
        var results = await store.QueryAsync(query);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Status.Should().Be(WorkflowStatus.Running));
    }

    [Fact]
    public async Task WorkflowInstanceStore_Query_SupportsPagination()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        for (var i = 0; i < 10; i++)
        {
            await store.SaveAsync(CreateInstance($"inst-{i}"));
        }

        // Act
        var query = new WorkflowInstanceQuery { Skip = 3, Take = 5 };
        var results = await store.QueryAsync(query);

        // Assert
        results.Should().HaveCount(5);
    }

    [Fact]
    public async Task WorkflowInstanceStore_GetWaitingForEvent_ReturnsPausedInstancesWithWaitingSteps()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();

        // Create a paused instance with a step waiting for event
        var waitingInstance = CreateInstance("waiting-1") with
        {
            Status = WorkflowStatus.Paused,
            StepInstances = new Dictionary<string, StepInstance>
            {
                ["wait-step"] = new StepInstance
                {
                    StepId = "wait-step",
                    Status = StepStatus.WaitingForEvent
                }
            }
        };

        // Create another paused instance without waiting steps
        var pausedNoWaitingInstance = CreateInstance("paused-no-wait") with
        {
            Status = WorkflowStatus.Paused,
            StepInstances = new Dictionary<string, StepInstance>
            {
                ["step1"] = new StepInstance
                {
                    StepId = "step1",
                    Status = StepStatus.Completed
                }
            }
        };

        var runningInstance = CreateInstance("running-1") with
        {
            Status = WorkflowStatus.Running,
            StepInstances = new Dictionary<string, StepInstance>
            {
                ["step1"] = new StepInstance
                {
                    StepId = "step1",
                    Status = StepStatus.Running
                }
            }
        };

        await store.SaveAsync(waitingInstance);
        await store.SaveAsync(pausedNoWaitingInstance);
        await store.SaveAsync(runningInstance);

        // Act - Note: Current implementation returns paused instances with WaitingForEvent steps
        // without filtering by eventType or correlationKey (basic implementation)
        var results = await store.GetWaitingForEventAsync("AnyEventType");

        // Assert - Only instances that are Paused AND have WaitingForEvent steps
        results.Should().HaveCount(1);
        results[0].Id.Should().Be("waiting-1");
    }

    [Fact]
    public async Task WorkflowInstanceStore_GetNonExistent_ReturnsNull()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();

        // Act
        var result = await store.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowInstanceStore_Clear_RemovesAllInstances()
    {
        // Arrange
        var store = new InMemoryWorkflowInstanceStore();
        await store.SaveAsync(CreateInstance("inst-1"));
        await store.SaveAsync(CreateInstance("inst-2"));

        // Act
        store.Clear();
        var result = await store.GetAsync("inst-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowRegistry_RegisterAndGet_ReturnsWorkflow()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();
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
                    Name = "Step 1",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "TestCommand" }
                }
            ]
        };

        // Act
        await registry.RegisterAsync(workflow);
        var retrieved = await registry.GetAsync("test-workflow", "1.0.0");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test-workflow");
        retrieved.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task WorkflowRegistry_GetLatestVersion_ReturnsLatest()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();

        await registry.RegisterAsync(new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            Version = "1.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J1" } }]
        });

        await registry.RegisterAsync(new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            Version = "2.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J2" } }]
        });

        // Act
        var retrieved = await registry.GetAsync("test-workflow");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task WorkflowRegistry_List_ReturnsAllWorkflows()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();

        await registry.RegisterAsync(new WorkflowDefinition
        {
            Id = "workflow-a",
            Name = "Workflow A",
            Version = "1.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J1" } }]
        });

        await registry.RegisterAsync(new WorkflowDefinition
        {
            Id = "workflow-b",
            Name = "Workflow B",
            Version = "1.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J2" } }]
        });

        // Act
        var workflows = await registry.ListAsync();

        // Assert
        workflows.Should().HaveCount(2);
    }

    [Fact]
    public async Task WorkflowRegistry_Remove_RemovesWorkflow()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();
        var workflow = new WorkflowDefinition
        {
            Id = "to-remove",
            Name = "To Remove",
            Version = "1.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J1" } }]
        };

        await registry.RegisterAsync(workflow);

        // Act
        await registry.RemoveAsync("to-remove", "1.0.0");
        var retrieved = await registry.GetAsync("to-remove", "1.0.0");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowRegistry_GetNonExistent_ReturnsNull()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();

        // Act
        var result = await registry.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkflowRegistry_Clear_RemovesAllWorkflows()
    {
        // Arrange
        var registry = new InMemoryWorkflowRegistry();
        await registry.RegisterAsync(new WorkflowDefinition
        {
            Id = "workflow-1",
            Name = "Workflow 1",
            Version = "1.0.0",
            Steps = [new WorkflowStep { Id = "s1", Name = "S1", Type = StepType.Job, Config = new JobStepConfig { Command = "J1" } }]
        });

        // Act
        registry.Clear();
        var result = await registry.GetAsync("workflow-1");

        // Assert
        result.Should().BeNull();
    }

    private static WorkflowInstance CreateInstance(string id, string workflowId = "test-workflow")
    {
        return new WorkflowInstance
        {
            Id = id,
            WorkflowId = workflowId,
            WorkflowVersion = "1.0.0",
            Status = WorkflowStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
