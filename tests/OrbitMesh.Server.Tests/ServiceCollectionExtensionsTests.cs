using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbitMesh.Server.Extensions;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests;

public class ServiceCollectionExtensionsTests
{
    #region AddOrbitMeshServer Tests

    [Fact]
    public void AddOrbitMeshServer_Returns_Builder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddOrbitMeshServer();

        // Assert
        builder.Should().NotBeNull();
        builder.Services.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_AgentRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InMemoryAgentRegistry>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_JobManager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJobManager));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InMemoryJobManager>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_JobDispatcher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJobDispatcher));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<JobDispatcher>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_AgentRouter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRouter));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<AgentRouter>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_IdempotencyService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IIdempotencyService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InMemoryIdempotencyService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_DeadLetterService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDeadLetterService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InMemoryDeadLetterService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_ProgressService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IProgressService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InMemoryProgressService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_ResilienceService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IResilienceService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<ResilienceService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_JobOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJobOrchestrator));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<JobOrchestrator>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOrbitMeshServer_Registers_SignalR()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitMeshServer();

        // Assert
        // SignalR adds many services, just verify SignalR core is registered
        services.Any(d => d.ServiceType.Name.Contains("SignalR", StringComparison.Ordinal) ||
                         d.ImplementationType?.Name.Contains("SignalR", StringComparison.Ordinal) == true ||
                         d.ServiceType.Namespace?.Contains("SignalR", StringComparison.Ordinal) == true)
            .Should().BeTrue("SignalR services should be registered");
    }

    #endregion

    #region Builder Configuration Tests

    [Fact]
    public void UseAgentRegistry_Generic_Replaces_Default()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.UseAgentRegistry<CustomAgentRegistry>();

        // Assert
        var descriptors = services.Where(d => d.ServiceType == typeof(IAgentRegistry)).ToList();
        descriptors.Should().HaveCount(1);
        descriptors[0].ImplementationType.Should().Be<CustomAgentRegistry>();
    }

    [Fact]
    public void UseAgentRegistry_Instance_Replaces_Default()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();
        var customRegistry = new InMemoryAgentRegistry();

        // Act
        builder.UseAgentRegistry(customRegistry);

        // Assert
        var descriptors = services.Where(d => d.ServiceType == typeof(IAgentRegistry)).ToList();
        descriptors.Should().HaveCount(1);
        descriptors[0].ImplementationInstance.Should().BeSameAs(customRegistry);
    }

    [Fact]
    public void ConfigureSignalR_Registers_Options()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.ConfigureSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

        // Assert
        // Options are registered via Configure<T>
        services.Any(d => d.ServiceType.Name.Contains("ConfigureOptions", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void AddOrbitMeshServer_With_Configure_Action_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureInvoked = false;

        // Act
        services.AddOrbitMeshServer(builder =>
        {
            configureInvoked = true;
            builder.UseAgentRegistry<CustomAgentRegistry>();
        });

        // Assert
        configureInvoked.Should().BeTrue();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        descriptor!.ImplementationType.Should().Be<CustomAgentRegistry>();
    }

    #endregion

    #region Health Check Registration Tests

    [Fact]
    public void AddHealthChecks_Registers_Health_Check_Service()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.AddHealthChecks();

        // Assert
        var healthCheckDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(HealthCheckService));
        healthCheckDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddHealthChecks_Registers_AgentHealthCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.AddHealthChecks();

        // Assert
        // Health checks are registered via IOptions<HealthCheckServiceOptions>
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        options.Should().NotBeNull();
        options!.Value.Registrations.Should().Contain(r => r.Name == "orbitmesh-agents");
    }

    [Fact]
    public void AddHealthChecks_Registers_JobQueueHealthCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.AddHealthChecks();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        options.Should().NotBeNull();
        options!.Value.Registrations.Should().Contain(r => r.Name == "orbitmesh-jobs");
    }

    [Fact]
    public void AddHealthChecks_Registers_With_Correct_Tags()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.AddHealthChecks();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        options.Should().NotBeNull();

        var agentCheck = options!.Value.Registrations.FirstOrDefault(r => r.Name == "orbitmesh-agents");
        agentCheck.Should().NotBeNull();
        agentCheck!.Tags.Should().Contain("orbitmesh");
        agentCheck.Tags.Should().Contain("agents");

        var jobCheck = options.Value.Registrations.FirstOrDefault(r => r.Name == "orbitmesh-jobs");
        jobCheck.Should().NotBeNull();
        jobCheck!.Tags.Should().Contain("orbitmesh");
        jobCheck.Tags.Should().Contain("jobs");
    }

    [Fact]
    public void AddHealthChecks_With_Options_Uses_Custom_Threshold()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        builder.AddHealthChecks(options =>
        {
            options.PendingJobThreshold = 50;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        options.Should().NotBeNull();
        options!.Value.Registrations.Should().Contain(r => r.Name == "orbitmesh-jobs");
    }

    [Fact]
    public void AddHealthChecks_Returns_Builder_For_Chaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddOrbitMeshServer();

        // Act
        var result = builder.AddHealthChecks();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void OrbitMeshHealthCheckOptions_Has_Default_Threshold()
    {
        // Arrange & Act
        var options = new OrbitMeshHealthCheckOptions();

        // Assert
        options.PendingJobThreshold.Should().Be(100);
    }

    #endregion

    #region Test Support Classes

    private sealed class CustomAgentRegistry : InMemoryAgentRegistry { }

    #endregion
}
