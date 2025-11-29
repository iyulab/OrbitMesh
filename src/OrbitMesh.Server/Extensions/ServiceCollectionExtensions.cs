using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

        // Register agent registry (default: in-memory)
        services.AddSingleton<IAgentRegistry, InMemoryAgentRegistry>();

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
