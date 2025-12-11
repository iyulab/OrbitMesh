using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Node.BuiltIn.FileWatcher;
using OrbitMesh.Node.BuiltIn.Handlers;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Node.BuiltIn;

/// <summary>
/// Extension methods for registering built-in handlers.
/// </summary>
public static class BuiltInExtensions
{
    /// <summary>
    /// Registers all built-in handlers.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="options">Options for built-in handler configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static MeshAgentBuilder WithBuiltInHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        return builder
            .WithSystemHandlers(options)
            .WithFileHandlers(options)
            .WithServiceHandlers(options)
            .WithUpdateHandlers(options)
            .WithFileWatchHandlers(options);
    }

    /// <summary>
    /// Registers system handlers (health, version, metrics, ping, execute).
    /// </summary>
    public static MeshAgentBuilder WithSystemHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;

        // Health check handler
        builder.OnCommand(Commands.System.HealthCheck, new HealthCheckHandler(
            options.HealthChecks ?? [new DefaultHealthCheck()],
            loggerFactory.CreateLogger<HealthCheckHandler>()));

        // Version handler
        builder.OnCommand(Commands.System.Version, new VersionHandler(options.AppVersion));

        // Metrics handler
        builder.OnCommand(Commands.System.Metrics, new MetricsHandler());

        // Ping handler
        builder.OnCommand(Commands.System.Ping, new PingHandler());

        // Execute handler (optional, disabled by default for security)
        if (options.EnableShellExecution)
        {
            builder.OnCommand(Commands.System.Execute, new ExecuteHandler(
                enabled: true,
                loggerFactory.CreateLogger<ExecuteHandler>()));
        }

        return builder;
    }

    /// <summary>
    /// Registers file handlers (download, upload, delete, list, info, exists, sync).
    /// </summary>
    public static MeshAgentBuilder WithFileHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
        var httpClient = options.HttpClient ?? new HttpClient();

        // Download handler
        builder.OnCommand(Commands.File.Download, new FileDownloadHandler(
            httpClient,
            loggerFactory.CreateLogger<FileDownloadHandler>()));

        // Upload handler
        builder.OnCommand(Commands.File.Upload, new FileUploadHandler(
            httpClient,
            loggerFactory.CreateLogger<FileUploadHandler>()));

        // Delete handler
        builder.OnCommand(Commands.File.Delete, new FileDeleteHandler(
            loggerFactory.CreateLogger<FileDeleteHandler>()));

        // List handler
        builder.OnCommand(Commands.File.List, new FileListHandler());

        // Info handler
        builder.OnCommand(Commands.File.Info, new FileInfoHandler());

        // Exists handler
        builder.OnCommand(Commands.File.Exists, new FileExistsHandler());

        // Sync handler
        builder.OnCommand(Commands.File.Sync, new FileSyncHandler(
            httpClient,
            loggerFactory.CreateLogger<FileSyncHandler>()));

        return builder;
    }

    /// <summary>
    /// Registers service control handlers (start, stop, restart, status).
    /// </summary>
    public static MeshAgentBuilder WithServiceHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;

        // Start handler
        builder.OnCommand(Commands.Service.Start, new ServiceStartHandler(
            loggerFactory.CreateLogger<ServiceStartHandler>()));

        // Stop handler
        builder.OnCommand(Commands.Service.Stop, new ServiceStopHandler(
            loggerFactory.CreateLogger<ServiceStopHandler>()));

        // Restart handler
        builder.OnCommand(Commands.Service.Restart, new ServiceRestartHandler(
            loggerFactory.CreateLogger<ServiceRestartHandler>()));

        // Status handler
        builder.OnCommand(Commands.Service.Status, new ServiceStatusHandler());

        return builder;
    }

    /// <summary>
    /// Registers update handlers (check, download, apply, rollback, status).
    /// </summary>
    public static MeshAgentBuilder WithUpdateHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
        var httpClient = options.HttpClient ?? new HttpClient();

        // Ensure update service is available
        var updateService = options.UpdateService ?? new DefaultUpdateService(
            httpClient,
            options.UpdateServerUrl ?? string.Empty,
            options.ApplicationPath ?? AppContext.BaseDirectory,
            loggerFactory.CreateLogger<DefaultUpdateService>());

        // Check handler
        builder.OnCommand(Commands.Update.Check, new UpdateCheckHandler(
            httpClient,
            updateService,
            loggerFactory.CreateLogger<UpdateCheckHandler>()));

        // Download handler
        builder.OnCommand(Commands.Update.Download, new UpdateDownloadHandler(
            httpClient,
            loggerFactory.CreateLogger<UpdateDownloadHandler>()));

        // Apply handler
        builder.OnCommand(Commands.Update.Apply, new UpdateApplyHandler(
            updateService,
            loggerFactory.CreateLogger<UpdateApplyHandler>()));

        // Rollback handler
        builder.OnCommand(Commands.Update.Rollback, new UpdateRollbackHandler(
            updateService,
            loggerFactory.CreateLogger<UpdateRollbackHandler>()));

        // Status handler
        builder.OnCommand(Commands.Update.Status, new UpdateStatusHandler(updateService));

        return builder;
    }

    /// <summary>
    /// Registers file watch handlers (start, stop, list).
    /// </summary>
    public static MeshAgentBuilder WithFileWatchHandlers(
        this MeshAgentBuilder builder,
        BuiltInHandlerOptions? options = null)
    {
        options ??= new BuiltInHandlerOptions();

        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;

        // Create or use existing FileWatcherService
        var watcherService = options.FileWatcherService ??
            new FileWatcherService(loggerFactory.CreateLogger<FileWatcherService>());

        // Start handler
        builder.OnCommand(Commands.FileWatch.Start, new StartFileWatchHandler(
            watcherService,
            loggerFactory.CreateLogger<StartFileWatchHandler>()));

        // Stop handler
        builder.OnCommand(Commands.FileWatch.Stop, new StopFileWatchHandler(
            watcherService,
            loggerFactory.CreateLogger<StopFileWatchHandler>()));

        // List handler
        builder.OnCommand(Commands.FileWatch.List, new ListFileWatchesHandler(
            watcherService,
            loggerFactory.CreateLogger<ListFileWatchesHandler>()));

        // Store the watcher service in options for external access
        options.FileWatcherService = watcherService;

        return builder;
    }

    /// <summary>
    /// Adds a custom health check.
    /// </summary>
    public static MeshAgentBuilder WithHealthCheck(
        this MeshAgentBuilder builder,
        IHealthCheck healthCheck)
    {
        ArgumentNullException.ThrowIfNull(healthCheck);
        // Note: Health checks are collected via BuiltInHandlerOptions
        return builder;
    }
}

/// <summary>
/// Options for configuring built-in handlers.
/// </summary>
public sealed class BuiltInHandlerOptions
{
    /// <summary>
    /// Logger factory for handler logging.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// HTTP client for network operations.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Application version string.
    /// </summary>
    public string AppVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Whether to enable shell command execution.
    /// Default is false for security.
    /// </summary>
    public bool EnableShellExecution { get; set; }

    /// <summary>
    /// Custom health checks to include.
    /// </summary>
    public IEnumerable<IHealthCheck>? HealthChecks { get; set; }

    /// <summary>
    /// Custom update service implementation.
    /// </summary>
    public IUpdateService? UpdateService { get; set; }

    /// <summary>
    /// Update server URL for checking updates.
    /// </summary>
#pragma warning disable CA1056 // URI properties should not be strings (configuration simplicity)
    public string? UpdateServerUrl { get; set; }
#pragma warning restore CA1056

    /// <summary>
    /// Application installation path for updates.
    /// </summary>
    public string? ApplicationPath { get; set; }

    /// <summary>
    /// File watcher service instance for monitoring file changes.
    /// </summary>
    public FileWatcherService? FileWatcherService { get; set; }
}
