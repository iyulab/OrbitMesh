using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OrbitMesh.Transport.P2P.Extensions;

/// <summary>
/// Extension methods for registering P2P transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds P2P transport services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddP2PTransport(
        this IServiceCollection services,
        Action<P2POptions>? configure = null)
    {
        // Register options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<P2POptions>(_ => { });
        }

        // Register ICE gatherer
        services.TryAddSingleton<IceGatherer>();

        // Note: LiteNetP2PTransport and PeerConnectionManager are typically
        // instantiated per-agent, not as singletons. They should be created
        // by the MeshAgent when P2P is enabled.

        return services;
    }
}
