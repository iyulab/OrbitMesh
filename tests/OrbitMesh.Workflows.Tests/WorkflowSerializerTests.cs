using OrbitMesh.Workflows.Models;
using OrbitMesh.Workflows.Parsing;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for the workflow YAML serializer.
/// </summary>
public class WorkflowSerializerTests
{
    private readonly WorkflowSerializer _serializer = new();
    private readonly WorkflowParser _parser = new();

    [Fact]
    public void Serialize_SimpleWorkflow_ProducesValidYaml()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            Version = "1.0.0",
            Description = "A test workflow",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "First Step",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "TestCommand",
                        Pattern = "agent-*"
                    }
                }
            ]
        };

        // Act
        var yaml = _serializer.Serialize(workflow);

        // Assert
        yaml.Should().Contain("id: test-workflow");
        yaml.Should().Contain("name: Test Workflow");
        yaml.Should().Contain("version: 1.0.0");
        yaml.Should().Contain("description: A test workflow");
    }

    [Fact]
    public void Serialize_ThenParse_PreservesWorkflowData()
    {
        // Arrange
        var original = new WorkflowDefinition
        {
            Id = "roundtrip-workflow",
            Name = "Roundtrip Test",
            Version = "2.0.0",
            Description = "Testing roundtrip serialization",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "job-step",
                    Name = "Job Step",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "ProcessData",
                        Pattern = "worker-*",
                        Priority = 5
                    }
                },
                new WorkflowStep
                {
                    Id = "delay-step",
                    Name = "Wait Step",
                    Type = StepType.Delay,
                    DependsOn = ["job-step"],
                    Config = new DelayStepConfig
                    {
                        Duration = TimeSpan.FromMinutes(5)
                    }
                }
            ],
            Variables = new Dictionary<string, object?>
            {
                ["setting1"] = "value1",
                ["setting2"] = 42
            }
        };

        // Act
        var yaml = _serializer.Serialize(original);
        var parsed = _parser.Parse(yaml);

        // Assert
        parsed.Id.Should().Be(original.Id);
        parsed.Name.Should().Be(original.Name);
        parsed.Version.Should().Be(original.Version);
        parsed.Steps.Should().HaveCount(2);
        parsed.Steps[0].Type.Should().Be(StepType.Job);
        parsed.Steps[1].Type.Should().Be(StepType.Delay);
        parsed.Steps[1].DependsOn.Should().Contain("job-step");
    }

    [Fact]
    public void Serialize_WorkflowWithTriggers_IncludesTriggers()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Id = "triggered-workflow",
            Name = "Triggered Workflow",
            Version = "1.0.0",
            Triggers =
            [
                new ScheduleTrigger
                {
                    Id = "schedule1",
                    Name = "Daily Run",
                    CronExpression = "0 0 * * *"
                },
                new EventTrigger
                {
                    Id = "event1",
                    Name = "Order Event",
                    EventType = "OrderCreated"
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Job Step",
                    Type = StepType.Job,
                    Config = new JobStepConfig { Command = "ProcessOrder" }
                }
            ]
        };

        // Act
        var yaml = _serializer.Serialize(workflow);

        // Assert - YAML serializer uses snake_case naming convention
        yaml.Should().Contain("triggers:");
        yaml.Should().Contain("cron: 0 0 * * *");
        yaml.Should().Contain("event_type: OrderCreated");
    }
}
