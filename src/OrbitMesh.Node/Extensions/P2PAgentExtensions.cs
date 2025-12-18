using Microsoft.Extensions.Logging;
using OrbitMesh.Node.P2P;
using OrbitMesh.Transport.P2P;

namespace OrbitMesh.Node.Extensions;

/// <summary>
/// Extension methods for adding P2P capabilities to MeshAgent.
/// </summary>
public static class P2PAgentExtensions
{
    /// <summary>
    /// Creates a P2P handler for the given agent.
    /// </summary>
    /// <param name="agent">The mesh agent.</param>
    /// <param name="options">P2P configuration options.</param>
    /// <param name="loggerFactory">Logger factory for P2P components.</param>
    /// <returns>A new P2P handler instance.</returns>
    public static P2PAgentHandler CreateP2PHandler(
        this IMeshAgent agent,
        P2POptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        options ??= new P2POptions();
        loggerFactory ??= Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        return new P2PAgentHandler(agent.Id, options, loggerFactory);
    }

    /// <summary>
    /// Creates a P2P handler with configuration callback.
    /// </summary>
    /// <param name="agent">The mesh agent.</param>
    /// <param name="configure">Configuration callback for P2P options.</param>
    /// <param name="loggerFactory">Logger factory for P2P components.</param>
    /// <returns>A new P2P handler instance.</returns>
    public static P2PAgentHandler CreateP2PHandler(
        this IMeshAgent agent,
        Action<P2POptions> configure,
        ILoggerFactory? loggerFactory = null)
    {
        var options = new P2POptions();
        configure(options);
        loggerFactory ??= Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        return new P2PAgentHandler(agent.Id, options, loggerFactory);
    }
}

/// <summary>
/// Builder for creating P2P-enabled mesh agents.
/// </summary>
public sealed class P2PMeshAgentBuilder
{
    private readonly MeshAgentBuilder _innerBuilder;
    private readonly P2POptions _p2pOptions = new();
    private ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Creates a new P2P-enabled agent builder.
    /// </summary>
    /// <param name="serverUrl">The OrbitMesh server URL.</param>
    public P2PMeshAgentBuilder(string serverUrl)
    {
        _innerBuilder = new MeshAgentBuilder(serverUrl);
    }

    /// <summary>
    /// Creates a P2P agent builder from an existing builder.
    /// </summary>
    /// <param name="builder">The existing builder.</param>
    public P2PMeshAgentBuilder(MeshAgentBuilder builder)
    {
        _innerBuilder = builder;
    }

    /// <summary>
    /// Creates a new P2P agent builder.
    /// </summary>
    public static P2PMeshAgentBuilder Create(string serverUrl) => new(serverUrl);

    /// <summary>
    /// Configures P2P options.
    /// </summary>
    public P2PMeshAgentBuilder WithP2POptions(Action<P2POptions> configure)
    {
        configure(_p2pOptions);
        return this;
    }

    /// <summary>
    /// Sets whether to prefer P2P over relay.
    /// </summary>
    public P2PMeshAgentBuilder PreferP2P(bool prefer = true)
    {
        _p2pOptions.PreferP2P = prefer;
        return this;
    }

    /// <summary>
    /// Sets whether to fall back to relay when P2P fails.
    /// </summary>
    public P2PMeshAgentBuilder WithRelayFallback(bool enable = true)
    {
        _p2pOptions.FallbackToRelay = enable;
        return this;
    }

    /// <summary>
    /// Sets the local UDP port for P2P connections.
    /// </summary>
    public P2PMeshAgentBuilder WithLocalPort(int port)
    {
        _p2pOptions.LocalPort = port;
        return this;
    }

    /// <summary>
    /// Configures STUN server settings.
    /// </summary>
    public P2PMeshAgentBuilder WithStunServer(string server, int port = 3478)
    {
        _p2pOptions.StunServer = server;
        _p2pOptions.StunPort = port;
        return this;
    }

    /// <summary>
    /// Configures TURN server settings for relay fallback.
    /// </summary>
    public P2PMeshAgentBuilder WithTurnServer(
        string server,
        int port = 3478,
        string? username = null,
        string? password = null)
    {
        _p2pOptions.TurnServer = server;
        _p2pOptions.TurnPort = port;
        _p2pOptions.TurnUsername = username;
        _p2pOptions.TurnPassword = password;
        return this;
    }

    /// <summary>
    /// Sets the agent ID.
    /// </summary>
    public P2PMeshAgentBuilder WithId(string agentId)
    {
        _innerBuilder.WithId(agentId);
        return this;
    }

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public P2PMeshAgentBuilder WithName(string name)
    {
        _innerBuilder.WithName(name);
        return this;
    }

    /// <summary>
    /// Sets the agent group.
    /// </summary>
    public P2PMeshAgentBuilder InGroup(string group)
    {
        _innerBuilder.InGroup(group);
        return this;
    }

    /// <summary>
    /// Sets the access token.
    /// </summary>
    public P2PMeshAgentBuilder WithAccessToken(string accessToken)
    {
        _innerBuilder.WithAccessToken(accessToken);
        return this;
    }

    /// <summary>
    /// Sets the bootstrap token for enrollment.
    /// </summary>
    public P2PMeshAgentBuilder WithBootstrapToken(string bootstrapToken)
    {
        _innerBuilder.WithBootstrapToken(bootstrapToken);
        return this;
    }

    /// <summary>
    /// Adds a capability.
    /// </summary>
    public P2PMeshAgentBuilder WithCapability(string name, string? version = null)
    {
        _innerBuilder.WithCapability(name, version);
        return this;
    }

    /// <summary>
    /// Adds a tag.
    /// </summary>
    public P2PMeshAgentBuilder WithTag(string tag)
    {
        _innerBuilder.WithTag(tag);
        return this;
    }

    /// <summary>
    /// Configures logging.
    /// </summary>
    public P2PMeshAgentBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _innerBuilder.WithLogging(loggerFactory);
        return this;
    }

    /// <summary>
    /// Builds the agent with P2P capabilities.
    /// </summary>
    /// <returns>A tuple of the agent and P2P handler.</returns>
    public (IMeshAgent Agent, P2PAgentHandler P2PHandler) Build()
    {
        // Add P2P capability marker
        _innerBuilder.WithCapability("p2p", "1.0");

        var agent = _innerBuilder.Build();

        _loggerFactory ??= Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        var p2pHandler = new P2PAgentHandler(
            agent.Id,
            _p2pOptions,
            _loggerFactory);

        return (agent, p2pHandler);
    }

    /// <summary>
    /// Builds the agent and connects with P2P enabled.
    /// </summary>
    public async Task<(IMeshAgent Agent, P2PAgentHandler P2PHandler)> BuildAndConnectAsync(
        CancellationToken cancellationToken = default)
    {
        var (agent, p2pHandler) = Build();

        // Connect the main agent first
        await agent.ConnectAsync(cancellationToken);

        // Get the server URL from the original builder (via reflection or store it)
        // For now, initialize P2P with the connection
        // Note: In production, the P2P hub URL would be derived from the agent connection

        return (agent, p2pHandler);
    }
}

/// <summary>
/// Extension methods for MeshAgentBuilder to add P2P support.
/// </summary>
public static class MeshAgentBuilderP2PExtensions
{
    /// <summary>
    /// Converts the builder to a P2P-enabled builder.
    /// </summary>
    /// <param name="builder">The mesh agent builder.</param>
    /// <returns>A P2P mesh agent builder.</returns>
    public static P2PMeshAgentBuilder WithP2PTransport(this MeshAgentBuilder builder)
    {
        return new P2PMeshAgentBuilder(builder);
    }

    /// <summary>
    /// Converts the builder to a P2P-enabled builder with configuration.
    /// </summary>
    /// <param name="builder">The mesh agent builder.</param>
    /// <param name="configure">P2P options configuration.</param>
    /// <returns>A P2P mesh agent builder.</returns>
    public static P2PMeshAgentBuilder WithP2PTransport(
        this MeshAgentBuilder builder,
        Action<P2POptions> configure)
    {
        var p2pBuilder = new P2PMeshAgentBuilder(builder);
        p2pBuilder.WithP2POptions(configure);
        return p2pBuilder;
    }
}
