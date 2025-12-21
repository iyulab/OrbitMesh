using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbitMesh.Host.Hubs;
using OrbitMesh.Host.Services;
using OrbitMesh.Host.Services.Adapters;
using OrbitMesh.Host.Services.Deployment;
using OrbitMesh.Host.Services.Workflows;
using OrbitMesh.Workflows;
using OrbitMesh.Workflows.Execution;
using WorkflowDispatcher = OrbitMesh.Workflows.Execution.IJobDispatcher;

namespace OrbitMesh.Host.Extensions;

/// <summary>
/// Extension methods for configuring OrbitMesh server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OrbitMesh server services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>An OrbitMesh builder for further configuration.</returns>
    public static OrbitMeshServerBuilder AddOrbitMeshServer(this IServiceCollection services)
    {
        // Add SignalR with MessagePack
        services.AddSignalR()
            .AddMessagePackProtocol();

        // Register core services (default: in-memory implementations)
        services.AddSingleton<IAgentRegistry, InMemoryAgentRegistry>();
        services.AddSingleton<IJobManager, InMemoryJobManager>();
        services.AddSingleton<IAgentRouter, AgentRouter>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.AddSingleton<IDeadLetterService, InMemoryDeadLetterService>();
        services.AddSingleton<IProgressService, InMemoryProgressService>();
        services.AddSingleton<IStreamingService, InMemoryStreamingService>();
        services.AddSingleton<ResilienceOptions>();
        services.AddSingleton<IResilienceService, ResilienceService>();

        // Register dispatcher and orchestrator
        services.AddSingleton<Services.IJobDispatcher, JobDispatcher>();
        services.AddSingleton<IJobOrchestrator, JobOrchestrator>();

        // Register client results service for bidirectional RPC
        services.AddSingleton<IClientResultsService, ClientResultsService>();

        // Register API token service
        services.AddSingleton<IApiTokenService, InMemoryApiTokenService>();

        // Register dashboard notifier for real-time UI updates
        services.AddSingleton<IDashboardNotifier, DashboardNotifier>();

        // Register background services with default options
        services.Configure<WorkItemProcessorOptions>(_ => { });
        services.Configure<JobTimeoutMonitorOptions>(_ => { });
        services.AddHostedService<WorkItemProcessor>();
        services.AddHostedService<JobTimeoutMonitor>();

        return new OrbitMeshServerBuilder(services);
    }

    /// <summary>
    /// Adds OrbitMesh server services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshServer(
        this IServiceCollection services,
        Action<OrbitMeshServerBuilder> configure)
    {
        var builder = services.AddOrbitMeshServer();
        configure(builder);
        return services;
    }
}

/// <summary>
/// Builder for configuring OrbitMesh server services.
/// </summary>
public sealed class OrbitMeshServerBuilder
{
    private readonly IServiceCollection _services;

    internal OrbitMeshServerBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Uses a custom agent registry implementation.
    /// </summary>
    /// <typeparam name="TRegistry">The registry implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder UseAgentRegistry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRegistry>()
        where TRegistry : class, IAgentRegistry
    {
        // Remove existing registration
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        if (descriptor is not null)
        {
            _services.Remove(descriptor);
        }

        _services.AddSingleton<IAgentRegistry, TRegistry>();
        return this;
    }

    /// <summary>
    /// Uses a custom agent registry instance.
    /// </summary>
    /// <param name="registry">The registry instance.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder UseAgentRegistry(IAgentRegistry registry)
    {
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IAgentRegistry));
        if (descriptor is not null)
        {
            _services.Remove(descriptor);
        }

        _services.AddSingleton(registry);
        return this;
    }

    /// <summary>
    /// Configures SignalR options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureSignalR(Action<Microsoft.AspNetCore.SignalR.HubOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Adds OrbitMesh health checks.
    /// </summary>
    /// <param name="pendingJobThreshold">Threshold for degraded job queue status (default: 100).</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddHealthChecks(int pendingJobThreshold = 100)
    {
        _services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "orbitmesh-agents",
                sp => new AgentHealthCheck(sp.GetRequiredService<IAgentRegistry>()),
                HealthStatus.Degraded,
                ["orbitmesh", "agents"]))
            .Add(new HealthCheckRegistration(
                "orbitmesh-jobs",
                sp => new JobQueueHealthCheck(
                    sp.GetRequiredService<IJobManager>(),
                    pendingJobThreshold),
                HealthStatus.Degraded,
                ["orbitmesh", "jobs"]));

        return this;
    }

    /// <summary>
    /// Adds OrbitMesh health checks with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for health check options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddHealthChecks(Action<OrbitMeshHealthCheckOptions> configure)
    {
        var options = new OrbitMeshHealthCheckOptions();
        configure(options);
        return AddHealthChecks(options.PendingJobThreshold);
    }

    /// <summary>
    /// Configures the WorkItemProcessor background service.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureWorkItemProcessor(Action<WorkItemProcessorOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Configures the JobTimeoutMonitor background service.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder ConfigureJobTimeoutMonitor(Action<JobTimeoutMonitorOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Adds file storage services with local filesystem backend.
    /// </summary>
    /// <param name="rootPath">Root path for file storage.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddFileStorage(string rootPath)
    {
        _services.AddSingleton<IFileStorageService>(sp =>
            new LocalFileStorageService(
                rootPath,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalFileStorageService>>()));
        return this;
    }

    /// <summary>
    /// Adds file storage services with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for file storage options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddFileStorage(Action<FileStorageOptions> configure)
    {
        var options = new FileStorageOptions();
        configure(options);
        return AddFileStorage(options.RootPath);
    }

    /// <summary>
    /// Adds file sync services for bidirectional server-agent file synchronization.
    /// </summary>
    /// <param name="storageRootPath">Root path for file storage.</param>
    /// <param name="configure">Optional configuration action for file sync options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddFileSync(string storageRootPath, Action<FileSyncServiceOptions>? configure = null)
    {
        var options = new FileSyncServiceOptions();
        configure?.Invoke(options);

        // Ensure file storage is registered
        var hasStorage = _services.Any(d => d.ServiceType == typeof(IFileStorageService));
        if (!hasStorage)
        {
            AddFileStorage(storageRootPath);
        }

        // Register server file watcher
        _services.AddSingleton<IServerFileWatcherService>(sp =>
            new ServerFileWatcherService(
                storageRootPath,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServerFileWatcherService>>(),
                options.DebounceMs));

        // Register file sync service
        _services.AddSingleton<FileSyncOptions>(new FileSyncOptions
        {
            ServerUrl = options.ServerUrl,
            AgentSyncPath = options.AgentSyncPath,
            WatchEnabled = options.WatchEnabled,
            WatchPath = options.WatchPath,
            WatchPattern = options.WatchPattern,
            IncludeSubdirectories = options.IncludeSubdirectories,
            DeleteOrphans = options.DeleteOrphans,
            DebounceMs = options.DebounceMs
        });

        _services.AddSingleton<IFileSyncService, FileSyncService>();
        _services.AddHostedService<FileSyncService>(sp => (FileSyncService)sp.GetRequiredService<IFileSyncService>());

        return this;
    }

    /// <summary>
    /// Adds file sync services with configuration from IConfiguration.
    /// </summary>
    /// <param name="configuration">The configuration section.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddFileSync(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var storageSection = configuration.GetSection("OrbitMesh:FileStorage");
        var syncSection = configuration.GetSection("OrbitMesh:Features:FileSync");

        var storageRootPath = storageSection["RootPath"] ?? "./files";
        var options = new FileSyncServiceOptions
        {
            Enabled = syncSection.GetValue<bool>("Enabled"),
            ServerUrl = syncSection["ServerUrl"] ?? "http://localhost:5000",
            AgentSyncPath = syncSection["AgentSyncPath"] ?? ".",
            WatchEnabled = syncSection.GetValue("WatchEnabled", true),
            WatchPath = syncSection["WatchPath"] ?? ".",
            WatchPattern = syncSection["WatchPattern"] ?? "*.*",
            IncludeSubdirectories = syncSection.GetValue("IncludeSubdirectories", true),
            DeleteOrphans = syncSection.GetValue("DeleteOrphans", false),
            DebounceMs = syncSection.GetValue("DebounceMs", 500)
        };

        if (options.Enabled)
        {
            return AddFileSync(storageRootPath, o =>
            {
                o.ServerUrl = options.ServerUrl;
                o.AgentSyncPath = options.AgentSyncPath;
                o.WatchEnabled = options.WatchEnabled;
                o.WatchPath = options.WatchPath;
                o.WatchPattern = options.WatchPattern;
                o.IncludeSubdirectories = options.IncludeSubdirectories;
                o.DeleteOrphans = options.DeleteOrphans;
                o.DebounceMs = options.DebounceMs;
            });
        }

        return this;
    }

    /// <summary>
    /// Adds workflow engine integration to the server.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddWorkflows()
    {
        // Add core workflow services
        _services.AddOrbitMeshWorkflows();

        // Register adapters that bridge server services to workflow interfaces
        _services.AddSingleton<WorkflowDispatcher, WorkflowJobDispatcherAdapter>();
        _services.AddSingleton<ISubWorkflowLauncher, WorkflowSubWorkflowLauncherAdapter>();

        // Register trigger services
        _services.AddSingleton<IWorkflowTriggerService, WorkflowTriggerService>();
        _services.Configure<ScheduleTriggerOptions>(_ => { });
        _services.AddHostedService<ScheduleTriggerHostedService>();

        return this;
    }

    /// <summary>
    /// Adds workflow engine integration with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for workflow services.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddWorkflows(Action<WorkflowOptions> configure)
    {
        AddWorkflows();

        var options = new WorkflowOptions();
        configure(options);

        // Apply custom options
        if (options.NotificationSenderType is not null)
        {
            var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(INotificationSender));
            if (descriptor is not null)
            {
                _services.Remove(descriptor);
            }
            _services.AddSingleton(typeof(INotificationSender), options.NotificationSenderType);
        }

        if (options.ApprovalNotifierType is not null)
        {
            var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IApprovalNotifier));
            if (descriptor is not null)
            {
                _services.Remove(descriptor);
            }
            _services.AddSingleton(typeof(IApprovalNotifier), options.ApprovalNotifierType);
        }

        return this;
    }

    /// <summary>
    /// Adds deployment profile management to the server.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddDeployments()
    {
        return AddDeployments(_ => { });
    }

    /// <summary>
    /// Adds deployment profile management with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for deployment options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrbitMeshServerBuilder AddDeployments(Action<DeploymentOptions> configure)
    {
        // Configure options
        _services.Configure(configure);

        // Ensure file storage is registered (required for deployment file operations)
        var hasStorage = _services.Any(d => d.ServiceType == typeof(IFileStorageService));
        if (!hasStorage)
        {
            // Use default deployment storage path
            var defaultPath = Path.Combine(AppContext.BaseDirectory, "deployments");
            AddFileStorage(defaultPath);
        }

        // Register deployment services
        _services.AddSingleton<IDeploymentService, DeploymentService>();

        // Register file watcher as hosted service
        _services.AddHostedService<DeploymentProfileWatcherService>();

        return this;
    }
}

/// <summary>
/// Options for OrbitMesh health checks.
/// </summary>
public sealed class OrbitMeshHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the threshold for pending jobs before the health check reports degraded status.
    /// </summary>
    public int PendingJobThreshold { get; set; } = 100;
}

/// <summary>
/// Options for file storage configuration.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the root path for file storage.
    /// </summary>
    public string RootPath { get; set; } = "./storage";

    /// <summary>
    /// Gets or sets the maximum file size in bytes.
    /// </summary>
    public long MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100MB
}

/// <summary>
/// Options for file sync configuration.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration property")]
public sealed class FileSyncServiceOptions
{
    /// <summary>
    /// Gets or sets whether file sync is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the server base URL for file API.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Gets or sets the path on agents where files should be synced to.
    /// </summary>
    public string AgentSyncPath { get; set; } = ".";

    /// <summary>
    /// Gets or sets whether to watch for server file changes.
    /// </summary>
    public bool WatchEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the path to watch on server (relative to storage root).
    /// </summary>
    public string WatchPath { get; set; } = ".";

    /// <summary>
    /// Gets or sets the file pattern to watch.
    /// </summary>
    public string WatchPattern { get; set; } = "*.*";

    /// <summary>
    /// Gets or sets whether to include subdirectories in watch.
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to delete files on target that don't exist on source.
    /// </summary>
    public bool DeleteOrphans { get; set; }

    /// <summary>
    /// Gets or sets the debounce delay in milliseconds.
    /// </summary>
    public int DebounceMs { get; set; } = 500;
}

/// <summary>
/// Options for workflow engine configuration.
/// </summary>
public sealed class WorkflowOptions
{
    /// <summary>
    /// Gets or sets a custom notification sender type.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? NotificationSenderType { get; set; }

    /// <summary>
    /// Gets or sets a custom approval notifier type.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ApprovalNotifierType { get; set; }

    /// <summary>
    /// Uses a custom notification sender.
    /// </summary>
    /// <typeparam name="T">The notification sender type.</typeparam>
    /// <returns>The options for chaining.</returns>
    public WorkflowOptions UseNotificationSender<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, INotificationSender
    {
        NotificationSenderType = typeof(T);
        return this;
    }

    /// <summary>
    /// Uses a custom approval notifier.
    /// </summary>
    /// <typeparam name="T">The approval notifier type.</typeparam>
    /// <returns>The options for chaining.</returns>
    public WorkflowOptions UseApprovalNotifier<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IApprovalNotifier
    {
        ApprovalNotifierType = typeof(T);
        return this;
    }
}

/// <summary>
/// Extension methods for configuring OrbitMesh endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the OrbitMesh agent hub endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the hub (default: /agent).</param>
    /// <returns>The hub endpoint convention builder.</returns>
    public static HubEndpointConventionBuilder MapOrbitMeshHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/agent")
    {
        return endpoints.MapHub<AgentHub>(pattern);
    }

    /// <summary>
    /// Maps the OrbitMesh dashboard hub endpoint for real-time UI updates.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the hub (default: /hub/dashboard).</param>
    /// <returns>The hub endpoint convention builder.</returns>
    public static HubEndpointConventionBuilder MapOrbitMeshDashboardHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/hub/dashboard")
    {
        return endpoints.MapHub<DashboardHub>(pattern);
    }
}
