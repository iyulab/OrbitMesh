using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.Storage;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Migrations;
using OrbitMesh.Storage.Sqlite.Stores;

namespace OrbitMesh.Storage.Sqlite.Extensions;

/// <summary>
/// Extension methods for registering SQLite storage services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OrbitMesh SQLite storage with all recommended features enabled by default.
    /// <para>
    /// Default configuration (all enabled):
    /// <list type="bullet">
    ///   <item><description>Core stores (jobs, agents, workflows, events)</description></item>
    ///   <item><description>Security stores (bootstrap tokens, enrollments, certificates)</description></item>
    ///   <item><description>WAL mode for better concurrency</description></item>
    ///   <item><description>Auto-migration on startup</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// To disable specific features, use the configureOptions parameter:
    /// <code>
    /// services.AddOrbitMeshSqliteStorage(opt => {
    ///     opt.EnableSecurityStores = false;  // Use in-memory security instead
    ///     opt.EnableWalMode = false;         // Disable WAL mode
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure/override storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSqliteStorage(
        this IServiceCollection services,
        Action<SqliteStorageOptions>? configureOptions = null)
    {
        // Create options with defaults (all recommended features enabled)
        var options = new SqliteStorageOptions();
        configureOptions?.Invoke(options);

        // Register options for DI
        services.Configure<SqliteStorageOptions>(opt =>
        {
            opt.ConnectionString = options.ConnectionString;
            opt.EnableWalMode = options.EnableWalMode;
            opt.AutoMigrate = options.AutoMigrate;
            opt.CacheSize = options.CacheSize;
            opt.BusyTimeout = options.BusyTimeout;
            opt.EnableSecurityStores = options.EnableSecurityStores;
            opt.EnableCoreStores = options.EnableCoreStores;
            opt.EnableDeploymentStores = options.EnableDeploymentStores;
        });

        // Register pooled DbContext factory for singleton services (like security stores)
        // Pooled factory is designed for use from singleton services and SignalR hubs
        services.AddPooledDbContextFactory<OrbitMeshDbContext>(dbOptions =>
        {
            dbOptions.UseSqlite(options.ConnectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });

#if DEBUG
            dbOptions.EnableSensitiveDataLogging();
            dbOptions.EnableDetailedErrors();
#endif
        });

        // Also register scoped DbContext resolved via the factory for any code that needs it
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<OrbitMeshDbContext>>().CreateDbContext());

        // Register schema migrator for database versioning
        services.AddSingleton<SchemaMigrator>();

        // Register core storage (enabled by default)
        if (options.EnableCoreStores)
        {
            services.AddSingleton<IOrbitMeshStorage, SqliteOrbitMeshStorage>();
            services.AddSingleton<IJobStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Jobs);
            services.AddSingleton<IAgentStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Agents);
            services.AddSingleton<IWorkflowStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Workflows);
            services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Events);
        }

        // Register security stores (enabled by default)
        if (options.EnableSecurityStores)
        {
            RegisterSecurityStores(services);
        }

        // Register deployment stores (enabled by default)
        if (options.EnableDeploymentStores)
        {
            RegisterDeploymentStores(services);
        }

        return services;
    }

    /// <summary>
    /// Adds OrbitMesh SQLite storage with a specific connection string.
    /// All recommended features are enabled by default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="configureOptions">Optional action to configure/override additional options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSqliteStorage(
        this IServiceCollection services,
        string connectionString,
        Action<SqliteStorageOptions>? configureOptions = null)
    {
        return services.AddOrbitMeshSqliteStorage(opt =>
        {
            opt.ConnectionString = connectionString;
            configureOptions?.Invoke(opt);
        });
    }

    /// <summary>
    /// Initializes the OrbitMesh storage (applies migrations, configures SQLite).
    /// Call this during application startup.
    /// </summary>
    public static async Task InitializeOrbitMeshStorageAsync(
        this IServiceProvider services,
        CancellationToken ct = default)
    {
        var storage = services.GetRequiredService<IOrbitMeshStorage>();
        await storage.InitializeAsync(ct);
    }

    private static void RegisterSecurityStores(IServiceCollection services)
    {
        // Remove any existing in-memory implementations if registered
        RemoveExistingService<IBootstrapTokenService>(services);
        RemoveExistingService<INodeEnrollmentService>(services);
        RemoveExistingService<INodeCredentialService>(services);

        // Register SQLite-backed security stores as Singleton
        // These stores use IDbContextFactory internally, so they can be safely
        // registered as singletons and work correctly with SignalR hubs
        services.AddSingleton<IBootstrapTokenService, SqliteBootstrapTokenStore>();
        services.AddSingleton<INodeCredentialService, SqliteNodeCredentialStore>();
        services.AddSingleton<INodeEnrollmentService, SqliteNodeEnrollmentStore>();

        // Register SecurityInitializationService to initialize server keys on startup
        // This is critical - server keys must be initialized before agents can enroll
        RemoveExistingHostedService<SecurityInitializationService>(services);
        services.AddHostedService<SecurityInitializationService>();
    }

    private static void RemoveExistingHostedService<TService>(IServiceCollection services)
        where TService : class
    {
        // Remove any existing hosted service registration for TService
        var descriptors = services
            .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                        d.ImplementationType == typeof(TService))
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveExistingService<TService>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
    }

    private static void RegisterDeploymentStores(IServiceCollection services)
    {
        // Register SQLite-backed deployment stores as Singleton
        // These stores use IDbContextFactory internally, so they can be safely
        // registered as singletons and work correctly with SignalR hubs
        services.AddSingleton<IDeploymentProfileStore, SqliteDeploymentProfileStore>();
        services.AddSingleton<IDeploymentExecutionStore, SqliteDeploymentExecutionStore>();
    }
}
