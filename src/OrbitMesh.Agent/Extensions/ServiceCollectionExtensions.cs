using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Agent.Extensions;

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
/// Hosted service that manages the agent lifecycle.
/// </summary>
internal sealed class MeshAgentHostedService : IHostedService
{
    private readonly IMeshAgent _agent;
    private readonly ILogger<MeshAgentHostedService> _logger;

    public MeshAgentHostedService(IMeshAgent agent, ILogger<MeshAgentHostedService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting OrbitMesh agent. AgentId: {AgentId}, Name: {Name}",
            _agent.Id,
            _agent.Name);

        try
        {
            await _agent.ConnectAsync(cancellationToken);
            _logger.LogInformation("OrbitMesh agent connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect OrbitMesh agent");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
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
    }
}
