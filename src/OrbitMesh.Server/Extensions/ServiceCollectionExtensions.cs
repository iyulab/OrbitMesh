using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbitMesh.Server.Hubs;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Extensions;

/// <summary>
/// Extension methods for configuring OrbitMesh server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OrbitMesh server services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>An OrbitMesh builder for further configuration.</returns>
    public static OrbitMeshServerBuilder AddOrbitMeshServer(this IServiceCollection services)
    {
        // Add SignalR with MessagePack
        services.AddSignalR()
            .AddMessagePackProtocol();

        // Register core services (default: in-memory implementations)
        services.AddSingleton<IAgentRegistry, InMemoryAgentRegistry>();
        services.AddSingleton<IJobManager, InMemoryJobManager>();
        services.AddSingleton<IAgentRouter, AgentRouter>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.AddSingleton<IDeadLetterService, InMemoryDeadLetterService>();
        services.AddSingleton<IProgressService, InMemoryProgressService>();
        services.AddSingleton<IStreamingService, InMemoryStreamingService>();
        services.AddSingleton<IResilienceService, ResilienceService>();

        // Register dispatcher and orchestrator
        services.AddSingleton<IJobDispatcher, JobDispatcher>();
        services.AddSingleton<IJobOrchestrator, JobOrchestrator>();

        // Register client results service for bidirectional RPC
        services.AddSingleton<IClientResultsService, ClientResultsService>();

        // Register background services with default options
        services.Configure<WorkItemProcessorOptions>(_ => { });
        services.Configure<JobTimeoutMonitorOptions>(_ => { });
        services.AddHostedService<WorkItemProcessor>();
        services.AddHostedService<JobTimeoutMonitor>();

        return new OrbitMeshServerBuilder(services);
    }

    /// <summary>
    /// Adds OrbitMesh server services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshServer(
        this IServiceCollection services,
        Action<OrbitMeshServerBuilder> configure)
    {
        var builder = services.AddOrbitMeshServer();
        configure(builder);
        return services;
    }
}

/// <summary>
/// Builder for configuring OrbitMesh server services.
/// </summary>
public sealed class OrbitMeshServerBuilder
{
    private readonly IServiceCollection _services;

    internal OrbitMeshServerBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Uses a custom agent registry implementation.
    /// </summary>
    /// <typeparam name="TRegistry">The registry implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder UseAgentRegistry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRegistry>()
        where TRegistry : class, IAgentRegistry
    {
        // Remove existing registration
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        if (descriptor is not null)
        {
            _services.Remove(descriptor);
        }

        _services.AddSingleton<IAgentRegistry, TRegistry>();
        return this;
    }

    /// <summary>
    /// Uses a custom agent registry instance.
    /// </summary>
    /// <param name="registry">The registry instance.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder UseAgentRegistry(IAgentRegistry registry)
    {
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        if (descriptor is not null)
        {
            _services.Remove(descriptor);
        }

        _services.AddSingleton(registry);
        return this;
    }

    /// <summary>
    /// Configures SignalR options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureSignalR(Action<Microsoft.AspNetCore.SignalR.HubOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Adds OrbitMesh health checks.
    /// </summary>
    /// <param name="pendingJobThreshold">Threshold for degraded job queue status (default: 100).</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddHealthChecks(int pendingJobThreshold = 100)
    {
        _services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "orbitmesh-agents",
                sp => new AgentHealthCheck(sp.GetRequiredService<IAgentRegistry>()),
                HealthStatus.Degraded,
                ["orbitmesh", "agents"]))
            .Add(new HealthCheckRegistration(
                "orbitmesh-jobs",
                sp => new JobQueueHealthCheck(
                    sp.GetRequiredService<IJobManager>(),
                    pendingJobThreshold),
                HealthStatus.Degraded,
                ["orbitmesh", "jobs"]));

        return this;
    }

    /// <summary>
    /// Adds OrbitMesh health checks with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for health check options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddHealthChecks(Action<OrbitMeshHealthCheckOptions> configure)
    {
        var options = new OrbitMeshHealthCheckOptions();
        configure(options);
        return AddHealthChecks(options.PendingJobThreshold);
    }

    /// <summary>
    /// Configures the WorkItemProcessor background service.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureWorkItemProcessor(Action<WorkItemProcessorOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Configures the JobTimeoutMonitor background service.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureJobTimeoutMonitor(Action<JobTimeoutMonitorOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }
}

/// <summary>
/// Options for OrbitMesh health checks.
/// </summary>
public sealed class OrbitMeshHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the threshold for pending jobs before the health check reports degraded status.
    /// </summary>
    public int PendingJobThreshold { get; set; } = 100;
}

/// <summary>
/// Extension methods for configuring OrbitMesh endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the OrbitMesh agent hub endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the hub (default: /agent).</param>
    /// <returns>The hub endpoint convention builder.</returns>
    public static HubEndpointConventionBuilder MapOrbitMeshHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/agent")
    {
        return endpoints.MapHub<AgentHub>(pattern);
    }
}
