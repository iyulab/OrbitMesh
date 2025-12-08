using OrbitMesh.Workflows.Models;
using OrbitMesh.Workflows.Parsing;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for the YAML workflow parser.
/// </summary>
public class WorkflowParserTests
{
    private readonly WorkflowParser _parser = new();

    [Fact]
    public void Parse_SimpleWorkflow_ReturnsValidDefinition()
    {
        // Arrange - Note: job properties like 'command' are at step level, not inside 'config'
        var yaml = """
            id: test-workflow
            name: Test Workflow
            version: "1.0.0"
            description: A simple test workflow
            steps:
              - id: step1
                name: First Step
                type: job
                command: TestCommand
                pattern: "*"
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Id.Should().Be("test-workflow");
        workflow.Name.Should().Be("Test Workflow");
        workflow.Version.Should().Be("1.0.0");
        workflow.Description.Should().Be("A simple test workflow");
        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].Id.Should().Be("step1");
        workflow.Steps[0].Type.Should().Be(StepType.Job);
    }

    [Fact]
    public void Parse_WorkflowWithDependencies_ParsesDependencies()
    {
        // Arrange - Note: use snake_case 'depends_on' not camelCase 'dependsOn'
        var yaml = """
            id: dep-workflow
            name: Dependency Workflow
            version: "1.0.0"
            steps:
              - id: step1
                name: First Step
                type: job
                command: Command1
              - id: step2
                name: Second Step
                type: job
                depends_on:
                  - step1
                command: Command2
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps.Should().HaveCount(2);
        workflow.Steps[1].DependsOn.Should().Contain("step1");
    }

    [Fact]
    public void Parse_WorkflowWithCondition_ParsesCondition()
    {
        // Arrange
        var yaml = """
            id: cond-workflow
            name: Conditional Workflow
            version: "1.0.0"
            steps:
              - id: conditional-step
                name: Conditional Step
                type: job
                condition: "${status} == 'success'"
                command: ConditionalCommand
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Condition.Should().Be("${status} == 'success'");
    }

    [Fact]
    public void Parse_WorkflowWithDelay_ParsesDelayConfig()
    {
        // Arrange - delay step uses 'duration' at step level
        var yaml = """
            id: delay-workflow
            name: Delay Workflow
            version: "1.0.0"
            steps:
              - id: delay-step
                name: Wait Step
                type: delay
                duration: "30s"
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.Delay);
        var config = workflow.Steps[0].Config as DelayStepConfig;
        config.Should().NotBeNull();
        config!.Duration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Parse_WorkflowWithScheduleTrigger_ParsesTrigger()
    {
        // Arrange - Note: use 'cron' not 'cronExpression'
        var yaml = """
            id: scheduled-workflow
            name: Scheduled Workflow
            version: "1.0.0"
            triggers:
              - id: daily-trigger
                type: schedule
                cron: "0 0 * * *"
            steps:
              - id: step1
                name: Daily Job
                type: job
                command: DailyCommand
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Triggers.Should().HaveCount(1);
        workflow.Triggers![0].Should().BeOfType<ScheduleTrigger>();
        var trigger = (ScheduleTrigger)workflow.Triggers[0];
        trigger.CronExpression.Should().Be("0 0 * * *");
    }

    [Fact]
    public void Parse_WorkflowWithEventTrigger_ParsesTrigger()
    {
        // Arrange - Note: use 'event_type' not 'eventType'
        var yaml = """
            id: event-workflow
            name: Event Workflow
            version: "1.0.0"
            triggers:
              - id: order-trigger
                type: event
                event_type: OrderCreated
            steps:
              - id: step1
                name: Process Order
                type: job
                command: ProcessOrder
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Triggers.Should().HaveCount(1);
        workflow.Triggers![0].Should().BeOfType<EventTrigger>();
        var trigger = (EventTrigger)workflow.Triggers[0];
        trigger.EventType.Should().Be("OrderCreated");
    }

    [Fact]
    public void Parse_WorkflowWithVariables_ParsesVariables()
    {
        // Arrange - Note: use snake_case for YAML property names
        // YAML parser may return integers as int or string depending on the implementation
        var yaml = """
            id: var-workflow
            name: Variable Workflow
            version: "1.0.0"
            variables:
              max_retries: 3
              timeout: 60
              enabled: true
            steps:
              - id: step1
                name: Job Step
                type: job
                command: Command1
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Variables.Should().NotBeNull();
        workflow.Variables!["max_retries"].Should().BeOneOf(3, "3", 3L);
        workflow.Variables["timeout"].Should().BeOneOf(60, "60", 60L);
        workflow.Variables["enabled"].Should().BeOneOf(true, "true", "True");
    }

    [Fact]
    public void Parse_WorkflowWithForEach_ParsesForEachConfig()
    {
        // Arrange - Note: use snake_case and 'loop_steps' instead of nested 'config'
        var yaml = """
            id: foreach-workflow
            name: ForEach Workflow
            version: "1.0.0"
            steps:
              - id: process-items
                name: Process Each Item
                type: foreach
                collection: "${items}"
                item_variable: item
                max_concurrency: 5
                loop_steps:
                  - id: process-item
                    name: Process Single Item
                    type: job
                    command: ProcessItem
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.ForEach);
        var config = workflow.Steps[0].Config as ForEachStepConfig;
        config.Should().NotBeNull();
        config!.Collection.Should().Be("${items}");
        config.ItemVariable.Should().Be("item");
        config.MaxConcurrency.Should().Be(5);
        config.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_InvalidYaml_ThrowsWorkflowParseException()
    {
        // Arrange
        var yaml = "invalid: [yaml: content";

        // Act & Assert
        var action = () => _parser.Parse(yaml);
        action.Should().Throw<WorkflowParseException>();
    }

    [Fact]
    public void Parse_MissingId_GeneratesId()
    {
        // Arrange - parser generates ID if not provided
        var yaml = """
            name: Auto ID Workflow
            version: "1.0.0"
            steps:
              - id: step1
                name: Step 1
                type: job
                command: Command1
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert - ID should be auto-generated (not null)
        workflow.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_WorkflowWithParallelSteps_ParsesBranches()
    {
        // Arrange
        var yaml = """
            id: parallel-workflow
            name: Parallel Workflow
            version: "1.0.0"
            steps:
              - id: parallel-step
                name: Parallel Tasks
                type: parallel
                max_concurrency: 3
                branches:
                  - id: branch1
                    name: Branch 1
                    type: job
                    command: Command1
                  - id: branch2
                    name: Branch 2
                    type: job
                    command: Command2
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.Parallel);
        var config = workflow.Steps[0].Config as ParallelStepConfig;
        config.Should().NotBeNull();
        config!.Branches.Should().HaveCount(2);
        config.MaxConcurrency.Should().Be(3);
    }

    [Fact]
    public void Parse_WorkflowWithConditional_ParsesThenElseBranches()
    {
        // Arrange
        var yaml = """
            id: conditional-workflow
            name: Conditional Workflow
            version: "1.0.0"
            steps:
              - id: conditional-step
                name: Conditional
                type: conditional
                expression: "status == 'success'"
                then:
                  - id: success-step
                    name: Success Handler
                    type: job
                    command: HandleSuccess
                else:
                  - id: failure-step
                    name: Failure Handler
                    type: job
                    command: HandleFailure
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.Conditional);
        var config = workflow.Steps[0].Config as ConditionalStepConfig;
        config.Should().NotBeNull();
        config!.Expression.Should().Be("status == 'success'");
        config.ThenBranch.Should().HaveCount(1);
        config.ElseBranch.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_WorkflowWithApproval_ParsesApprovalConfig()
    {
        // Arrange
        var yaml = """
            id: approval-workflow
            name: Approval Workflow
            version: "1.0.0"
            steps:
              - id: approval-step
                name: Manager Approval
                type: approval
                approvers:
                  - manager
                  - director
                required_approvals: 1
                message: Please approve this request
                timeout: 24h
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.Approval);
        var config = workflow.Steps[0].Config as ApprovalStepConfig;
        config.Should().NotBeNull();
        config!.Approvers.Should().Contain("manager");
        config.Approvers.Should().Contain("director");
        config.RequiredApprovals.Should().Be(1);
        config.Message.Should().Be("Please approve this request");
    }

    [Fact]
    public void Parse_WorkflowWithNotify_ParsesNotifyConfig()
    {
        // Arrange
        var yaml = """
            id: notify-workflow
            name: Notify Workflow
            version: "1.0.0"
            steps:
              - id: notify-step
                name: Send Notification
                type: notify
                channel: email
                target: admin@example.com
                message: Task completed successfully
                subject: Task Notification
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.Notify);
        var config = workflow.Steps[0].Config as NotifyStepConfig;
        config.Should().NotBeNull();
        config!.Channel.Should().Be(NotifyChannel.Email);
        config.Target.Should().Be("admin@example.com");
        config.Message.Should().Be("Task completed successfully");
        config.Subject.Should().Be("Task Notification");
    }

    [Fact]
    public void Parse_WorkflowWithSubWorkflow_ParsesSubWorkflowConfig()
    {
        // Arrange
        var yaml = """
            id: parent-workflow
            name: Parent Workflow
            version: "1.0.0"
            steps:
              - id: sub-workflow-step
                name: Launch Child Workflow
                type: sub_workflow
                workflow_id: child-workflow
                workflow_version: "1.0.0"
                wait_for_completion: true
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.SubWorkflow);
        var config = workflow.Steps[0].Config as SubWorkflowStepConfig;
        config.Should().NotBeNull();
        config!.WorkflowId.Should().Be("child-workflow");
        config.Version.Should().Be("1.0.0");
        config.WaitForCompletion.Should().BeTrue();
    }

    [Fact]
    public void Parse_WorkflowWithWaitForEvent_ParsesWaitForEventConfig()
    {
        // Arrange
        var yaml = """
            id: event-wait-workflow
            name: Event Wait Workflow
            version: "1.0.0"
            steps:
              - id: wait-step
                name: Wait for Order Approval
                type: wait_for_event
                event_type: OrderApproved
                correlation_key: order_id
                timeout: 1h
            """;

        // Act
        var workflow = _parser.Parse(yaml);

        // Assert
        workflow.Steps[0].Type.Should().Be(StepType.WaitForEvent);
        var config = workflow.Steps[0].Config as WaitForEventStepConfig;
        config.Should().NotBeNull();
        config!.EventType.Should().Be("OrderApproved");
        config.CorrelationKey.Should().Be("order_id");
        config.Timeout.Should().Be(TimeSpan.FromHours(1));
    }
}
