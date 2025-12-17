using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.Storage;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Stores;

namespace OrbitMesh.Storage.Sqlite.Extensions;

/// <summary>
/// Extension methods for registering SQLite storage services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQLite storage services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string. Default: "Data Source=orbitmesh.db"</param>
    /// <param name="configureOptions">Optional action to configure storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSqliteStorage(
        this IServiceCollection services,
        string connectionString = "Data Source=orbitmesh.db",
        Action<SqliteStorageOptions>? configureOptions = null)
    {
        // Configure options
        var options = new SqliteStorageOptions { ConnectionString = connectionString };
        configureOptions?.Invoke(options);
        services.Configure<SqliteStorageOptions>(opt =>
        {
            opt.ConnectionString = options.ConnectionString;
            opt.EnableWalMode = options.EnableWalMode;
            opt.AutoMigrate = options.AutoMigrate;
            opt.CacheSize = options.CacheSize;
            opt.BusyTimeout = options.BusyTimeout;
        });

        // Register DbContext factory
        services.AddDbContextFactory<OrbitMeshDbContext>(dbOptions =>
        {
            dbOptions.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });

#if DEBUG
            dbOptions.EnableSensitiveDataLogging();
            dbOptions.EnableDetailedErrors();
#endif
        });

        // Register storage
        services.AddSingleton<IOrbitMeshStorage, SqliteOrbitMeshStorage>();
        services.AddSingleton<IJobStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Jobs);
        services.AddSingleton<IAgentStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Agents);
        services.AddSingleton<IWorkflowStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Workflows);
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<IOrbitMeshStorage>().Events);

        return services;
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

    /// <summary>
    /// Adds SQLite-backed security services (bootstrap tokens, enrollments, certificates).
    /// This replaces the in-memory security implementations with persistent storage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSqliteSecurityStores(
        this IServiceCollection services)
    {
        // Remove any existing in-memory implementations if registered
        var bootstrapTokenDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IBootstrapTokenService));
        if (bootstrapTokenDescriptor != null)
        {
            services.Remove(bootstrapTokenDescriptor);
        }

        var enrollmentDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(INodeEnrollmentService));
        if (enrollmentDescriptor != null)
        {
            services.Remove(enrollmentDescriptor);
        }

        var credentialDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(INodeCredentialService));
        if (credentialDescriptor != null)
        {
            services.Remove(credentialDescriptor);
        }

        // Register SQLite-backed security stores
        services.AddScoped<IBootstrapTokenService, SqliteBootstrapTokenStore>();
        services.AddScoped<INodeCredentialService, SqliteNodeCredentialStore>();
        services.AddScoped<INodeEnrollmentService, SqliteNodeEnrollmentStore>();

        return services;
    }

    /// <summary>
    /// Adds SQLite storage with security stores.
    /// This is a convenience method that calls both AddOrbitMeshSqliteStorage and AddOrbitMeshSqliteSecurityStores.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string. Default: "Data Source=orbitmesh.db"</param>
    /// <param name="configureOptions">Optional action to configure storage options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshSqliteStorageWithSecurity(
        this IServiceCollection services,
        string connectionString = "Data Source=orbitmesh.db",
        Action<SqliteStorageOptions>? configureOptions = null)
    {
        services.AddOrbitMeshSqliteStorage(connectionString, configureOptions);
        services.AddOrbitMeshSqliteSecurityStores();
        return services;
    }
}
