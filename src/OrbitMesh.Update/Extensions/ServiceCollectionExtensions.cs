using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.Platform;
using OrbitMesh.Update.Models;
using OrbitMesh.Update.Services;

namespace OrbitMesh.Update.Extensions;

/// <summary>
/// Extension methods for registering update services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OrbitMesh update services to the service collection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshUpdate(
        this IServiceCollection services,
        Action<UpdateOptions>? configure = null)
    {
        // Configure options
        var optionsBuilder = services.AddOptions<UpdateOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        // Register platform paths if not already registered
        services.AddSingleton<IPlatformPaths>(sp =>
        {
            var existing = sp.GetService<PlatformPaths>();
            if (existing is not null)
                return existing;

            var paths = new PlatformPaths();
            paths.EnsureDirectoriesExist();
            return paths;
        });

        // Register HTTP client for GitHub API
        services.AddHttpClient<IGitHubReleaseService, GitHubReleaseService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        // Register update service
        services.AddSingleton<IUpdateService, UpdateService>();

        return services;
    }

    /// <summary>
    /// Adds OrbitMesh update services with specific platform paths.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="platformPaths">Platform paths instance.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshUpdate(
        this IServiceCollection services,
        IPlatformPaths platformPaths,
        Action<UpdateOptions>? configure = null)
    {
        services.AddSingleton(platformPaths);
        return services.AddOrbitMeshUpdate(configure);
    }
}
