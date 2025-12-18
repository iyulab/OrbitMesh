using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Host.Hubs;
using OrbitMesh.Host.Services.P2P;

namespace OrbitMesh.Host.Extensions;

/// <summary>
/// Extension methods for configuring P2P services.
/// </summary>
public static class P2PServiceExtensions
{
    /// <summary>
    /// Adds P2P (peer-to-peer) transport services to the OrbitMesh server.
    /// This enables direct agent-to-agent communication with NAT traversal support.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configure">Optional configuration for P2P options.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddP2PTransport(
        this OrbitMeshServerBuilder builder,
        Action<P2PServerOptions>? configure = null)
    {
        var options = new P2PServerOptions { Enabled = true };
        configure?.Invoke(options);

        builder.Services.Configure<P2PServerOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.StunPort = options.StunPort;
            opt.StunSecondaryPort = options.StunSecondaryPort;
            opt.EnableEmbeddedStun = options.EnableEmbeddedStun;
            opt.ExternalStunServer = options.ExternalStunServer;
            opt.ExternalStunPort = options.ExternalStunPort;
            opt.EnableMetrics = options.EnableMetrics;
            opt.PeerHealthCheckIntervalSeconds = options.PeerHealthCheckIntervalSeconds;
            opt.NatDetectionTimeoutSeconds = options.NatDetectionTimeoutSeconds;
            opt.AllowRelayFallback = options.AllowRelayFallback;
            opt.MaxConnectionsPerAgent = options.MaxConnectionsPerAgent;
        });

        // Register P2P services
        builder.Services.AddSingleton<IPeerCoordinator, PeerCoordinator>();
        builder.Services.AddSingleton<IStunServer, EmbeddedStunServer>();

        // Register embedded STUN server as hosted service if enabled
        if (options.EnableEmbeddedStun)
        {
            builder.Services.AddHostedService<EmbeddedStunServer>(sp =>
                (EmbeddedStunServer)sp.GetRequiredService<IStunServer>());
        }

        return builder;
    }

    /// <summary>
    /// Adds P2P transport services with configuration from IConfiguration.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configuration">The configuration section.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddP2PTransport(
        this OrbitMeshServerBuilder builder,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("OrbitMesh:P2P");

        var options = new P2PServerOptions
        {
            Enabled = section.GetValue("Enabled", false),
            StunPort = section.GetValue("StunPort", 3478),
            StunSecondaryPort = section.GetValue("StunSecondaryPort", 3479),
            EnableEmbeddedStun = section.GetValue("EnableEmbeddedStun", true),
            ExternalStunServer = section["ExternalStunServer"],
            ExternalStunPort = section.GetValue("ExternalStunPort", 3478),
            EnableMetrics = section.GetValue("EnableMetrics", true),
            PeerHealthCheckIntervalSeconds = section.GetValue("PeerHealthCheckIntervalSeconds", 30),
            NatDetectionTimeoutSeconds = section.GetValue("NatDetectionTimeoutSeconds", 5),
            AllowRelayFallback = section.GetValue("AllowRelayFallback", true),
            MaxConnectionsPerAgent = section.GetValue("MaxConnectionsPerAgent", 0)
        };

        if (options.Enabled)
        {
            return builder.AddP2PTransport(o =>
            {
                o.Enabled = options.Enabled;
                o.StunPort = options.StunPort;
                o.StunSecondaryPort = options.StunSecondaryPort;
                o.EnableEmbeddedStun = options.EnableEmbeddedStun;
                o.ExternalStunServer = options.ExternalStunServer;
                o.ExternalStunPort = options.ExternalStunPort;
                o.EnableMetrics = options.EnableMetrics;
                o.PeerHealthCheckIntervalSeconds = options.PeerHealthCheckIntervalSeconds;
                o.NatDetectionTimeoutSeconds = options.NatDetectionTimeoutSeconds;
                o.AllowRelayFallback = options.AllowRelayFallback;
                o.MaxConnectionsPerAgent = options.MaxConnectionsPerAgent;
            });
        }

        return builder;
    }

    /// <summary>
    /// Maps the ICE signaling hub endpoint for P2P coordination.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the hub (default: /p2p).</param>
    /// <returns>The hub endpoint convention builder.</returns>
    public static HubEndpointConventionBuilder MapOrbitMeshP2PHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/p2p")
    {
        return endpoints.MapHub<IceSignalingHub>(pattern);
    }
}
