using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.FileTransfer;
using OrbitMesh.Host.Extensions;

namespace OrbitMesh.Host.Services.FileTransfer;

/// <summary>
/// Extension methods for registering file transfer services.
/// </summary>
public static class FileTransferServiceExtensions
{
    /// <summary>
    /// Adds the centralized file transfer service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for the file transfer service.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileTransferService(
        this IServiceCollection services,
        Action<SmartFileTransferOptions>? configure = null)
    {
        var options = new SmartFileTransferOptions();
        configure?.Invoke(options);

        services.Configure<SmartFileTransferOptions>(opt =>
        {
            opt.EnableP2P = options.EnableP2P;
            opt.ServerBaseUrl = options.ServerBaseUrl;
            opt.ChunkSize = options.ChunkSize;
            opt.P2PFailureThreshold = options.P2PFailureThreshold;
            opt.MaxConcurrentTransfers = options.MaxConcurrentTransfers;
            opt.DefaultTimeout = options.DefaultTimeout;
        });

        services.AddSingleton<IFileTransferService, SmartFileTransferService>();

        return services;
    }

    /// <summary>
    /// Adds the file transfer service to the OrbitMesh server builder.
    /// </summary>
    /// <param name="builder">The server builder.</param>
    /// <param name="configure">Optional configuration for the file transfer service.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddFileTransfer(
        this OrbitMeshServerBuilder builder,
        Action<SmartFileTransferOptions>? configure = null)
    {
        builder.Services.AddFileTransferService(configure);
        return builder;
    }
}
