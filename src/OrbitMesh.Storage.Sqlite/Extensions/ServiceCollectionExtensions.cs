using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.Storage;

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
}
