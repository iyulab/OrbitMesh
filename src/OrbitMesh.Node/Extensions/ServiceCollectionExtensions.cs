using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Node.Extensions;

/// <summary>
/// Extension methods for configuring OrbitMesh agent services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an OrbitMesh agent to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serverUrl">The OrbitMesh server URL.</param>
    /// <param name="configure">Configuration action for the agent builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshAgent(
        this IServiceCollection services,
        string serverUrl,
        Action<MeshAgentBuilder> configure)
    {
        services.AddSingleton<IMeshAgent>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var builder = MeshAgentBuilder.Create(serverUrl);

            if (loggerFactory is not null)
            {
                builder.WithLogging(loggerFactory);
            }

            configure(builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds an OrbitMesh agent as a hosted service.
    /// The agent will automatically connect on startup and disconnect on shutdown.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serverUrl">The OrbitMesh server URL.</param>
    /// <param name="configure">Configuration action for the agent builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshAgentHostedService(
        this IServiceCollection services,
        string serverUrl,
        Action<MeshAgentBuilder> configure)
    {
        services.AddOrbitMeshAgent(serverUrl, configure);
        services.AddHostedService<MeshAgentHostedService>();
        return services;
    }
}

/// <summary>
/// Hosted service that manages the agent lifecycle with resilient connection handling.
/// Continuously retries connection with exponential backoff to avoid server load.
/// </summary>
internal sealed class MeshAgentHostedService : BackgroundService
{
    private readonly IMeshAgent _agent;
    private readonly ILogger<MeshAgentHostedService> _logger;

    // Retry configuration - exponential backoff to minimize server load
    private const int MaxRetryDelaySeconds = 300; // 5 minutes max
    private const int InitialRetryDelaySeconds = 5;
    private const double BackoffMultiplier = 1.5;

    public MeshAgentHostedService(
        IMeshAgent agent,
        ILogger<MeshAgentHostedService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting OrbitMesh agent. AgentId: {AgentId}, Name: {Name}",
            _agent.Id,
            _agent.Name);

        // Maintain connection in background with resilient retry
        await MaintainConnectionAsync(stoppingToken);
    }

    private async Task MaintainConnectionAsync(CancellationToken stoppingToken)
    {
        var currentRetryDelay = InitialRetryDelaySeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_agent.IsConnected)
                {
                    _logger.LogInformation("Attempting to connect to OrbitMesh server...");
                    await _agent.ConnectAsync(stoppingToken);
                    _logger.LogInformation("OrbitMesh agent connected successfully");

                    // Reset retry delay on successful connection
                    currentRetryDelay = InitialRetryDelaySeconds;
                }

                // Wait for disconnection or cancellation
                await _agent.WaitForShutdownAsync(stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Connection lost. Will attempt to reconnect...");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to connect to OrbitMesh server. Retrying in {Delay} seconds...",
                    currentRetryDelay);

                // Wait before retry with exponential backoff
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(currentRetryDelay), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                // Increase delay with exponential backoff (capped at max)
                currentRetryDelay = (int)Math.Min(
                    currentRetryDelay * BackoffMultiplier,
                    MaxRetryDelaySeconds);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OrbitMesh agent. AgentId: {AgentId}", _agent.Id);

        try
        {
            await _agent.DisconnectAsync(cancellationToken);
            _logger.LogInformation("OrbitMesh agent disconnected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during OrbitMesh agent disconnect");
        }

        await base.StopAsync(cancellationToken);
    }
}
