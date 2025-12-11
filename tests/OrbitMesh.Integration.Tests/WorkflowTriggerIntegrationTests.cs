using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Host.Extensions;
using OrbitMesh.Host.Services.Workflows;
using OrbitMesh.Workflows;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Integration.Tests;

/// <summary>
/// End-to-end integration tests for workflow trigger system.
/// </summary>
public class WorkflowTriggerIntegrationTests
{
    [Fact]
    public async Task TriggerService_CanRegisterAndUnregisterTriggers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "trigger-test-workflow",
            Name = "Trigger Test Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Test Step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new EventTrigger
                {
                    Id = "event-trigger-1",
                    Name = "Test Event Trigger",
                    EventType = "order.created",
                    IsEnabled = true
                },
                new WebhookTrigger
                {
                    Id = "webhook-trigger-1",
                    Name = "Test Webhook Trigger",
                    Path = "/webhooks/test",
                    Methods = ["POST"],
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);

        // Act
        await triggerService.RegisterTriggersAsync(workflow);
        var registeredTriggers = await triggerService.GetRegisteredTriggersAsync();

        // Assert
        registeredTriggers.Should().ContainKey(workflow.Id);
        registeredTriggers[workflow.Id].Should().HaveCount(2);

        // Act - Unregister
        await triggerService.UnregisterTriggersAsync(workflow.Id);
        var afterUnregister = await triggerService.GetRegisteredTriggersAsync();

        // Assert
        afterUnregister.Should().NotContainKey(workflow.Id);
    }

    [Fact]
    public async Task TriggerService_CanProcessEventTrigger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();
        var engine = provider.GetRequiredService<IWorkflowEngine>();

        var workflow = new WorkflowDefinition
        {
            Id = "event-triggered-workflow",
            Name = "Event Triggered Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Process order",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new EventTrigger
                {
                    Id = "order-created-trigger",
                    Name = "Order Created Trigger",
                    EventType = "order.created",
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act
        var triggeredInstances = await triggerService.ProcessEventAsync(
            "order.created",
            new { orderId = "ORD-123", amount = 100.0 });

        // Assert
        triggeredInstances.Should().HaveCount(1);

        // Verify instance was created
        var instance = await engine.GetInstanceAsync(triggeredInstances[0]);
        instance.Should().NotBeNull();
        instance!.WorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public async Task TriggerService_CanProcessWebhookTrigger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();
        var engine = provider.GetRequiredService<IWorkflowEngine>();

        var workflow = new WorkflowDefinition
        {
            Id = "webhook-triggered-workflow",
            Name = "Webhook Triggered Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Process webhook",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new WebhookTrigger
                {
                    Id = "github-webhook",
                    Name = "GitHub Webhook",
                    Path = "/webhooks/github",
                    Methods = ["POST", "PUT"],
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act
        var triggeredInstances = await triggerService.ProcessWebhookAsync(
            "/webhooks/github",
            "POST",
            new { action = "push", repository = "test-repo" },
            new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        // Assert
        triggeredInstances.Should().HaveCount(1);

        // Verify instance was created
        var instance = await engine.GetInstanceAsync(triggeredInstances[0]);
        instance.Should().NotBeNull();
        instance!.WorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public async Task TriggerService_WebhookWithSecret_ValidatesSecret()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "secure-webhook-workflow",
            Name = "Secure Webhook Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Process secure webhook",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new WebhookTrigger
                {
                    Id = "secure-webhook",
                    Name = "Secure Webhook",
                    Path = "/webhooks/secure",
                    Methods = ["POST"],
                    Secret = "my-secret-key",
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act - Without secret (should not trigger)
        var noSecretResult = await triggerService.ProcessWebhookAsync(
            "/webhooks/secure",
            "POST",
            new { data = "test" },
            null);

        // Act - With wrong secret (should not trigger)
        var wrongSecretResult = await triggerService.ProcessWebhookAsync(
            "/webhooks/secure",
            "POST",
            new { data = "test" },
            new Dictionary<string, string> { ["X-Webhook-Secret"] = "wrong-secret" });

        // Act - With correct secret (should trigger)
        var correctSecretResult = await triggerService.ProcessWebhookAsync(
            "/webhooks/secure",
            "POST",
            new { data = "test" },
            new Dictionary<string, string> { ["X-Webhook-Secret"] = "my-secret-key" });

        // Assert
        noSecretResult.Should().BeEmpty();
        wrongSecretResult.Should().BeEmpty();
        correctSecretResult.Should().HaveCount(1);
    }

    [Fact]
    public async Task TriggerService_CanTriggerWorkflowManually()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();
        var engine = provider.GetRequiredService<IWorkflowEngine>();

        var workflow = new WorkflowDefinition
        {
            Id = "manual-workflow",
            Name = "Manual Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Manual step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-trigger",
                    Name = "Manual Trigger",
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);

        // Act
        var instanceId = await triggerService.TriggerManuallyAsync(
            workflow.Id,
            new Dictionary<string, object?> { ["param1"] = "value1" },
            "test-user");

        // Assert
        instanceId.Should().NotBeNullOrEmpty();

        var instance = await engine.GetInstanceAsync(instanceId);
        instance.Should().NotBeNull();
        instance!.WorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public async Task TriggerService_ManualTrigger_ValidatesRequiredInput()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "validated-workflow",
            Name = "Validated Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Validated step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "validated-trigger",
                    Name = "Validated Trigger",
                    IsEnabled = true,
                    InputSchema = new Dictionary<string, InputParameterDefinition>
                    {
                        ["requiredParam"] = new InputParameterDefinition
                        {
                            Type = InputParameterType.StringValue,
                            Required = true,
                            Description = "A required parameter"
                        }
                    }
                }
            ]
        };

        await registry.RegisterAsync(workflow);

        // Act & Assert - Missing required parameter should throw
        var act = () => triggerService.TriggerManuallyAsync(
            workflow.Id,
            new Dictionary<string, object?> { ["optionalParam"] = "value" },
            "test-user");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*requiredParam*");

        // Act - With required parameter should succeed
        var instanceId = await triggerService.TriggerManuallyAsync(
            workflow.Id,
            new Dictionary<string, object?> { ["requiredParam"] = "value" },
            "test-user");

        instanceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TriggerService_CanEnableAndDisableTriggers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "toggle-workflow",
            Name = "Toggle Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Toggle step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new EventTrigger
                {
                    Id = "toggle-trigger",
                    Name = "Toggle Trigger",
                    EventType = "toggle.event",
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act - Trigger should work when enabled
        var enabledResult = await triggerService.ProcessEventAsync("toggle.event", null);
        enabledResult.Should().HaveCount(1);

        // Disable the trigger
        await triggerService.SetTriggerEnabledAsync(workflow.Id, "toggle-trigger", false);

        // Act - Trigger should not work when disabled
        var disabledResult = await triggerService.ProcessEventAsync("toggle.event", null);
        disabledResult.Should().BeEmpty();

        // Re-enable the trigger
        await triggerService.SetTriggerEnabledAsync(workflow.Id, "toggle-trigger", true);

        // Act - Trigger should work again
        var reenabledResult = await triggerService.ProcessEventAsync("toggle.event", null);
        reenabledResult.Should().HaveCount(1);
    }

    [Fact]
    public async Task TriggerService_EventFilter_OnlyTriggersMatchingEvents()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "filtered-workflow",
            Name = "Filtered Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Filtered step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new EventTrigger
                {
                    Id = "filtered-trigger",
                    Name = "Filtered Trigger",
                    EventType = "order.event",
                    Filter = "$.status",
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act - Event with filter property
        var withFilter = await triggerService.ProcessEventAsync(
            "order.event",
            new { status = "completed", orderId = "123" });

        // Assert
        withFilter.Should().HaveCount(1);
    }

    [Fact]
    public async Task TriggerService_WebhookMethodFiltering_OnlyTriggersMatchingMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddOrbitMeshServer(builder => builder.AddWorkflows());
        var provider = services.BuildServiceProvider();

        var triggerService = provider.GetRequiredService<IWorkflowTriggerService>();
        var registry = provider.GetRequiredService<IWorkflowRegistry>();

        var workflow = new WorkflowDefinition
        {
            Id = "method-workflow",
            Name = "Method Filtered Workflow",
            Version = "1.0.0",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "step-1",
                    Name = "Method step",
                    Type = StepType.Delay,
                    Config = new DelayStepConfig { Duration = TimeSpan.FromMilliseconds(10) }
                }
            ],
            Triggers =
            [
                new WebhookTrigger
                {
                    Id = "post-only-webhook",
                    Name = "POST Only Webhook",
                    Path = "/webhooks/post-only",
                    Methods = ["POST"],
                    IsEnabled = true
                }
            ]
        };

        await registry.RegisterAsync(workflow);
        await triggerService.RegisterTriggersAsync(workflow);

        // Act - POST should trigger
        var postResult = await triggerService.ProcessWebhookAsync(
            "/webhooks/post-only", "POST", null, null);

        // Act - GET should not trigger
        var getResult = await triggerService.ProcessWebhookAsync(
            "/webhooks/post-only", "GET", null, null);

        // Assert
        postResult.Should().HaveCount(1);
        getResult.Should().BeEmpty();
    }
}
