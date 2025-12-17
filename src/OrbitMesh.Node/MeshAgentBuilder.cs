using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using OrbitMesh.Node.Security;

namespace OrbitMesh.Node;

/// <summary>
/// Builder for configuring and creating OrbitMesh agents.
/// </summary>
public sealed class MeshAgentBuilder
{
    private readonly string _serverUrl;
    private readonly HandlerRegistry _handlerRegistry = new();
    private readonly List<AgentCapability> _capabilities = [];
    private readonly List<string> _tags = [];
    private readonly Dictionary<string, string> _metadata = new();

    private string _agentId = Guid.NewGuid().ToString("N");
    private string _agentName = Environment.MachineName;
    private string? _group;
    private string? _accessToken;
    private string? _bootstrapToken;
    private string? _credentialsPath;
    private bool _autoEnroll = true;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private Action<IHubConnectionBuilder>? _configureConnection;
    private TimeSpan _connectionTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _enrollmentTimeout = TimeSpan.FromHours(24);

    /// <summary>
    /// Creates a new MeshAgentBuilder.
    /// </summary>
    /// <param name="serverUrl">The OrbitMesh server URL.</param>
    public MeshAgentBuilder(string serverUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        _serverUrl = serverUrl;
    }

    /// <summary>
    /// Creates a new MeshAgentBuilder.
    /// </summary>
    /// <param name="serverUrl">The OrbitMesh server URL.</param>
    public static MeshAgentBuilder Create(string serverUrl) => new(serverUrl);

    /// <summary>
    /// Sets the agent ID.
    /// </summary>
    public MeshAgentBuilder WithId(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        _agentId = agentId;
        return this;
    }

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public MeshAgentBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _agentName = name;
        return this;
    }

    /// <summary>
    /// Sets the agent group.
    /// </summary>
    public MeshAgentBuilder InGroup(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        _group = group;
        return this;
    }

    /// <summary>
    /// Sets the access token for server authentication (legacy mode).
    /// </summary>
    /// <param name="accessToken">The API token for authenticating with the server.</param>
    public MeshAgentBuilder WithAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        _accessToken = accessToken;
        return this;
    }

    /// <summary>
    /// Sets the bootstrap token for initial enrollment.
    /// This token is used once to enroll the node and receive a certificate.
    /// </summary>
    /// <param name="bootstrapToken">The one-time bootstrap token.</param>
    public MeshAgentBuilder WithBootstrapToken(string bootstrapToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapToken);
        _bootstrapToken = bootstrapToken;
        return this;
    }

    /// <summary>
    /// Sets the path where node credentials will be stored.
    /// </summary>
    /// <param name="path">The file path for credential storage.</param>
    public MeshAgentBuilder WithCredentialsPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _credentialsPath = path;
        return this;
    }

    /// <summary>
    /// Sets whether to automatically enroll when not enrolled.
    /// Default is true.
    /// </summary>
    /// <param name="autoEnroll">Whether to auto-enroll.</param>
    public MeshAgentBuilder WithAutoEnroll(bool autoEnroll)
    {
        _autoEnroll = autoEnroll;
        return this;
    }

    /// <summary>
    /// Sets the maximum time to wait for enrollment approval.
    /// </summary>
    /// <param name="timeout">The enrollment timeout.</param>
    public MeshAgentBuilder WithEnrollmentTimeout(TimeSpan timeout)
    {
        _enrollmentTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Adds metadata to the agent for enrollment.
    /// </summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    public MeshAgentBuilder WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    /// <param name="metadata">Metadata dictionary.</param>
    public MeshAgentBuilder WithMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        foreach (var kvp in metadata)
        {
            _metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Adds a capability to the agent.
    /// </summary>
    public MeshAgentBuilder WithCapability(string name, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _capabilities.Add(new AgentCapability { Name = name, Version = version });
        return this;
    }

    /// <summary>
    /// Adds a capability with a handler.
    /// </summary>
    public MeshAgentBuilder WithCapability(string name, IMeshHandler handler, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        _capabilities.Add(new AgentCapability { Name = name, Version = version });
        _handlerRegistry.Register(name, handler);
        return this;
    }

    /// <summary>
    /// Adds a capability with a handler function.
    /// </summary>
    public MeshAgentBuilder WithCapability(
        string name,
        Func<CommandContext, Task<byte[]?>> handler,
        string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        _capabilities.Add(new AgentCapability { Name = name, Version = version });
        _handlerRegistry.Register(name, new DelegateHandler(name, handler));
        return this;
    }

    /// <summary>
    /// Adds a capability with a synchronous handler function.
    /// </summary>
    public MeshAgentBuilder WithCapability(
        string name,
        Func<CommandContext, byte[]?> handler,
        string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        return WithCapability(
            name,
            ctx => Task.FromResult(handler(ctx)),
            version);
    }

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    public MeshAgentBuilder OnCommand(string command, IMeshHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(handler);

        _handlerRegistry.Register(command, handler);
        return this;
    }

    /// <summary>
    /// Registers a command handler function.
    /// </summary>
    public MeshAgentBuilder OnCommand(string command, Func<CommandContext, Task<byte[]?>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(handler);

        _handlerRegistry.Register(command, new DelegateHandler(command, handler));
        return this;
    }

    /// <summary>
    /// Adds a tag to the agent.
    /// </summary>
    public MeshAgentBuilder WithTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple tags to the agent.
    /// </summary>
    public MeshAgentBuilder WithTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
        {
            WithTag(tag);
        }
        return this;
    }

    /// <summary>
    /// Configures logging for the agent.
    /// </summary>
    public MeshAgentBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        return this;
    }

    /// <summary>
    /// Configures the SignalR connection.
    /// </summary>
    public MeshAgentBuilder ConfigureConnection(Action<IHubConnectionBuilder> configure)
    {
        _configureConnection = configure;
        return this;
    }

    /// <summary>
    /// Sets the connection timeout.
    /// </summary>
    public MeshAgentBuilder WithConnectionTimeout(TimeSpan timeout)
    {
        _connectionTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Builds the agent without connecting.
    /// </summary>
    public IMeshAgent Build()
    {
        // Create credential manager if bootstrap token or credentials path is specified
        var credentialManagerLogger = _loggerFactory.CreateLogger<NodeCredentialManager>();
        var credentialManager = new NodeCredentialManager(_credentialsPath, credentialManagerLogger);

        var connectionBuilder = new HubConnectionBuilder()
            .WithUrl(_serverUrl, options =>
            {
                // Set up authentication token provider
                options.AccessTokenProvider = () => GetAccessTokenAsync(credentialManager);
            })
            .WithAutomaticReconnect()
            .AddMessagePackProtocol();

        _configureConnection?.Invoke(connectionBuilder);

        var connection = connectionBuilder.Build();

        var agentInfo = new AgentInfo
        {
            Id = _agentId,
            Name = _agentName,
            Group = _group,
            Status = AgentStatus.Created,
            Capabilities = [.. _capabilities],
            Tags = [.. _tags],
            Hostname = Environment.MachineName,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        var config = new MeshAgentConfiguration
        {
            BootstrapToken = _bootstrapToken,
            AutoEnroll = _autoEnroll,
            EnrollmentTimeout = _enrollmentTimeout,
            RequestedCapabilities = _capabilities.Select(c => c.Name).ToList(),
            Metadata = new Dictionary<string, string>(_metadata)
        };

        var enrollmentLogger = _loggerFactory.CreateLogger<EnrollmentService>();
        var enrollmentService = new EnrollmentService(connection, credentialManager, enrollmentLogger);
        enrollmentService.MaxWaitTime = _enrollmentTimeout;

        var logger = _loggerFactory.CreateLogger<MeshAgent>();

        return new MeshAgent(
            connection,
            agentInfo,
            _handlerRegistry,
            credentialManager,
            enrollmentService,
            config,
            logger);
    }

    private async Task<string?> GetAccessTokenAsync(NodeCredentialManager credentialManager)
    {
        // Priority 1: Certificate-based authentication
        if (credentialManager.IsEnrolled && credentialManager.Credentials?.Certificate is not null)
        {
            return $"cert:{credentialManager.Credentials.Certificate}";
        }

        // Priority 2: Bootstrap token for enrollment
        if (!string.IsNullOrEmpty(_bootstrapToken))
        {
            return $"bootstrap:{_bootstrapToken}";
        }

        // Priority 3: Legacy API token
        if (!string.IsNullOrEmpty(_accessToken))
        {
            return _accessToken;
        }

        return null;
    }

    /// <summary>
    /// Builds the agent and connects to the server.
    /// </summary>
    public async Task<IMeshAgent> BuildAndConnectAsync(CancellationToken cancellationToken = default)
    {
        var agent = Build();

        using var timeoutCts = new CancellationTokenSource(_connectionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        await agent.ConnectAsync(linkedCts.Token);

        return agent;
    }

    /// <summary>
    /// Simple delegate-based handler implementation.
    /// </summary>
    private sealed class DelegateHandler(string command, Func<CommandContext, Task<byte[]?>> handler) : IMeshHandler
    {
        public string Command => command;

        public Task<byte[]?> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
            => handler(context);
    }
}

/// <summary>
/// Configuration for MeshAgent.
/// </summary>
public sealed record MeshAgentConfiguration
{
    /// <summary>
    /// Bootstrap token for initial enrollment.
    /// </summary>
    public string? BootstrapToken { get; init; }

    /// <summary>
    /// Whether to automatically enroll when not enrolled.
    /// </summary>
    public bool AutoEnroll { get; init; } = true;

    /// <summary>
    /// Maximum time to wait for enrollment approval.
    /// </summary>
    public TimeSpan EnrollmentTimeout { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Capabilities to request during enrollment.
    /// </summary>
    public IReadOnlyList<string> RequestedCapabilities { get; init; } = [];

    /// <summary>
    /// Node metadata for enrollment.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
