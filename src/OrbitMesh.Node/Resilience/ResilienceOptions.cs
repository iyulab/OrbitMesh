namespace OrbitMesh.Node.Resilience;

/// <summary>
/// Configuration options for agent resilience and connection handling.
/// </summary>
public sealed record ResilienceOptions
{
    /// <summary>
    /// Initial delay between reconnection attempts.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan InitialReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between reconnection attempts.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Multiplier for exponential backoff between reconnection attempts.
    /// Default: 2.0.
    /// </summary>
    public double ReconnectBackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Whether to queue commands when disconnected and replay them on reconnection.
    /// Default: true.
    /// </summary>
    public bool QueueCommandsWhenDisconnected { get; init; } = true;

    /// <summary>
    /// Maximum number of commands to queue when disconnected.
    /// Default: 100.
    /// </summary>
    public int MaxQueuedCommands { get; init; } = 100;

    /// <summary>
    /// Maximum age of queued commands. Commands older than this are discarded.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan MaxCommandAge { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to enable connection health monitoring.
    /// Default: true.
    /// </summary>
    public bool EnableHealthMonitoring { get; init; } = true;

    /// <summary>
    /// Interval for connection health checks.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of consecutive health check failures before forcing reconnection.
    /// Default: 3.
    /// </summary>
    public int MaxHealthCheckFailures { get; init; } = 3;

    /// <summary>
    /// Creates default resilience options.
    /// </summary>
    public static ResilienceOptions Default => new();

    /// <summary>
    /// Creates aggressive reconnection options for high-availability scenarios.
    /// </summary>
    public static ResilienceOptions HighAvailability => new()
    {
        InitialReconnectDelay = TimeSpan.FromMilliseconds(500),
        MaxReconnectDelay = TimeSpan.FromMinutes(1),
        ReconnectBackoffMultiplier = 1.5,
        MaxQueuedCommands = 500,
        MaxCommandAge = TimeSpan.FromMinutes(30),
        HealthCheckInterval = TimeSpan.FromSeconds(10),
        MaxHealthCheckFailures = 2
    };

    /// <summary>
    /// Creates conservative options for resource-constrained environments.
    /// </summary>
    public static ResilienceOptions Conservative => new()
    {
        InitialReconnectDelay = TimeSpan.FromSeconds(5),
        MaxReconnectDelay = TimeSpan.FromMinutes(10),
        ReconnectBackoffMultiplier = 2.5,
        MaxQueuedCommands = 50,
        MaxCommandAge = TimeSpan.FromHours(2),
        HealthCheckInterval = TimeSpan.FromMinutes(1),
        MaxHealthCheckFailures = 5
    };
}
