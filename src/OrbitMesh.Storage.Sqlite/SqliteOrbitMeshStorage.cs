using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Stores;

namespace OrbitMesh.Storage.Sqlite;

/// <summary>
/// SQLite implementation of IOrbitMeshStorage.
/// </summary>
public sealed class SqliteOrbitMeshStorage : IOrbitMeshStorage
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;
    private readonly SqliteStorageOptions _options;
    private readonly ILogger<SqliteOrbitMeshStorage> _logger;

    public IJobStore Jobs { get; }
    public IAgentStore Agents { get; }
    public IWorkflowStore Workflows { get; }
    public IEventStore Events { get; }

    public SqliteOrbitMeshStorage(
        IDbContextFactory<OrbitMeshDbContext> contextFactory,
        IOptions<SqliteStorageOptions> options,
        ILogger<SqliteOrbitMeshStorage> logger)
    {
        _contextFactory = contextFactory;
        _options = options.Value;
        _logger = logger;

        Jobs = new SqliteJobStore(contextFactory);
        Agents = new SqliteAgentStore(contextFactory);
        Workflows = new SqliteWorkflowStore(contextFactory);
        Events = new SqliteEventStore(contextFactory);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing SQLite storage...");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        if (_options.AutoMigrate)
        {
            _logger.LogInformation("Applying database migrations...");
            // First, ensure the database exists
            await context.Database.EnsureCreatedAsync(ct);

            // Then, ensure any missing tables are created
            await EnsureMissingTablesAsync(context, ct);
        }

        // Configure SQLite for optimal performance
        if (_options.EnableWalMode)
        {
            await ConfigureWalModeAsync(context, ct);
        }

        await ConfigurePragmasAsync(context, ct);

        _logger.LogInformation("SQLite storage initialized successfully");
    }

    private async Task EnsureMissingTablesAsync(OrbitMeshDbContext context, CancellationToken ct)
    {
        // Get all table names from the model
        var modelTables = context.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        // Get existing table names from the database
        var existingTables = new HashSet<string>();
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            await context.Database.OpenConnectionAsync(ct);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                existingTables.Add(reader.GetString(0));
            }
        }

        // Find missing tables
        var missingTables = modelTables.Except(existingTables).ToList();

        if (missingTables.Count > 0)
        {
            _logger.LogInformation("Creating missing tables: {Tables}", string.Join(", ", missingTables));

            // Generate and execute CREATE TABLE statements for missing tables
            var script = context.Database.GenerateCreateScript();
            var statements = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var statement in statements)
            {
                // Check if this statement is for a missing table
                var isForMissingTable = missingTables.Any(t =>
                    statement.Contains($"CREATE TABLE \"{t}\"", StringComparison.OrdinalIgnoreCase) ||
                    statement.Contains($"CREATE TABLE {t}", StringComparison.OrdinalIgnoreCase));

                if (isForMissingTable)
                {
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync(statement, ct);
                        _logger.LogDebug("Executed: {Statement}", statement[..Math.Min(100, statement.Length)]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to execute statement: {Statement}", statement[..Math.Min(100, statement.Length)]);
                    }
                }
            }
        }
    }

    public Task DisposeAsync()
    {
        // DbContextFactory handles connection pooling
        return Task.CompletedTask;
    }

    private async Task ConfigureWalModeAsync(OrbitMeshDbContext context, CancellationToken ct)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
            _logger.LogDebug("WAL mode enabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enable WAL mode");
        }
    }

    private async Task ConfigurePragmasAsync(OrbitMeshDbContext context, CancellationToken ct)
    {
        try
        {
            // Improve write performance
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);

            // Set cache size - values are validated/sanitized integers
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"PRAGMA cache_size={_options.CacheSize};", ct);

            // Set busy timeout
            await context.Database.ExecuteSqlRawAsync($"PRAGMA busy_timeout={_options.BusyTimeout};", ct);
#pragma warning restore EF1002

            // Enable foreign keys
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);

            // Optimize for concurrent reads
            await context.Database.ExecuteSqlRawAsync("PRAGMA read_uncommitted=true;", ct);

            _logger.LogDebug("SQLite pragmas configured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure some SQLite pragmas");
        }
    }
}
