using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Host.Services.Security;

namespace OrbitMesh.Host.Extensions;

/// <summary>
/// Extension methods for registering security services.
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// Adds OrbitMesh security services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind security options
        services.Configure<SecurityOptions>(
            configuration.GetSection(SecurityOptions.SectionName));

        // Register security services
        services.AddSingleton<IBootstrapTokenService, InMemoryBootstrapTokenService>();
        services.AddSingleton<INodeCredentialService, InMemoryNodeCredentialService>();
        services.AddSingleton<INodeEnrollmentService, InMemoryNodeEnrollmentService>();

        // Register hosted service for security initialization and cleanup
        services.AddHostedService<SecurityInitializationService>();

        return services;
    }

    /// <summary>
    /// Adds OrbitMesh security services with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSecurity(
        this IServiceCollection services,
        Action<SecurityOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Register security services
        services.AddSingleton<IBootstrapTokenService, InMemoryBootstrapTokenService>();
        services.AddSingleton<INodeCredentialService, InMemoryNodeCredentialService>();
        services.AddSingleton<INodeEnrollmentService, InMemoryNodeEnrollmentService>();

        // Register hosted service for security initialization and cleanup
        services.AddHostedService<SecurityInitializationService>();

        return services;
    }

    /// <summary>
    /// Adds the OrbitMesh server builder extension for security.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddSecurity(
        this OrbitMeshServerBuilder builder,
        IConfiguration configuration)
    {
        builder.Services.AddOrbitMeshSecurity(configuration);
        return builder;
    }

    /// <summary>
    /// Adds the OrbitMesh server builder extension for security with custom options.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configureOptions">Options configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddSecurity(
        this OrbitMeshServerBuilder builder,
        Action<SecurityOptions> configureOptions)
    {
        builder.Services.AddOrbitMeshSecurity(configureOptions);
        return builder;
    }
}
