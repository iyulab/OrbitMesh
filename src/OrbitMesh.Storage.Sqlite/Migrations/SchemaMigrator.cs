using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Migrations;

/// <summary>
/// Manages database schema migrations.
/// </summary>
public sealed class SchemaMigrator
{
    /// <summary>
    /// Current schema version. Increment when adding migrations.
    /// </summary>
    public const int CurrentVersion = 1;

    private readonly ILogger<SchemaMigrator> _logger;
    private readonly List<ISchemaMigration> _migrations;

    public SchemaMigrator(ILogger<SchemaMigrator> logger)
    {
        _logger = logger;
        _migrations =
        [
            // Add migrations here in order
            // Example: new V2_AddColumnMigration(),
        ];
    }

    /// <summary>
    /// Ensures the schema version table exists and runs pending migrations.
    /// </summary>
    public async Task MigrateAsync(OrbitMeshDbContext context, CancellationToken ct = default)
    {
        // Ensure SchemaVersion table exists
        await EnsureSchemaVersionTableAsync(context, ct);

        // Get current version
        var currentVersion = await GetCurrentVersionAsync(context, ct);
        _logger.LogInformation("Current schema version: {Version}, Target version: {Target}",
            currentVersion, CurrentVersion);

        if (currentVersion >= CurrentVersion)
        {
            _logger.LogDebug("Schema is up to date");
            return;
        }

        // Run pending migrations in order
        var pendingMigrations = _migrations
            .Where(m => m.Version > currentVersion)
            .OrderBy(m => m.Version)
            .ToList();

        foreach (var migration in pendingMigrations)
        {
            _logger.LogInformation("Running migration v{Version}: {Description}",
                migration.Version, migration.Description);

            try
            {
                await migration.ExecuteAsync(context, ct);
                await UpdateSchemaVersionAsync(context, migration.Version, migration.Description, ct);
                _logger.LogInformation("Migration v{Version} completed successfully", migration.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration v{Version} failed", migration.Version);
                throw;
            }
        }

        // If no migrations but version is behind, just update to current
        // This handles the initial state where CurrentVersion is set
        if (pendingMigrations.Count == 0 && currentVersion < CurrentVersion)
        {
            await UpdateSchemaVersionAsync(context, CurrentVersion, "Initial schema", ct);
        }
    }

    private static async Task EnsureSchemaVersionTableAsync(OrbitMeshDbContext context, CancellationToken ct)
    {
        // Check if table exists
        var tableExists = await context.Database.ExecuteSqlRawAsync(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='SchemaVersion'", ct) > 0;

        // Alternative check using raw SQL
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
        await context.Database.OpenConnectionAsync(ct);
        var result = await command.ExecuteScalarAsync(ct);

        if (result == null)
        {
            // Create the table
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SchemaVersion" (
                    "Id" INTEGER NOT NULL PRIMARY KEY,
                    "Version" INTEGER NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "LastMigrationDescription" TEXT NULL
                )
                """, ct);

            // Insert initial row with version 0
            await context.Database.ExecuteSqlRawAsync("""
                INSERT OR IGNORE INTO "SchemaVersion" ("Id", "Version", "UpdatedAt", "LastMigrationDescription")
                VALUES (1, 0, datetime('now'), 'Initial')
                """, ct);
        }
    }

    private static async Task<int> GetCurrentVersionAsync(OrbitMeshDbContext context, CancellationToken ct)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"Version\" FROM \"SchemaVersion\" WHERE \"Id\" = 1";
        await context.Database.OpenConnectionAsync(ct);
        var result = await command.ExecuteScalarAsync(ct);
        return result is long version ? (int)version : 0;
    }

    private static async Task UpdateSchemaVersionAsync(
        OrbitMeshDbContext context,
        int version,
        string description,
        CancellationToken ct)
    {
        var entity = await context.Set<SchemaVersionEntity>().FindAsync([1], ct);
        if (entity != null)
        {
            entity.Version = version;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.LastMigrationDescription = description;
            await context.SaveChangesAsync(ct);
        }
        else
        {
            // Fallback to raw SQL with parameters to prevent SQL injection
            await context.Database.ExecuteSqlAsync(
                $"""
                UPDATE "SchemaVersion"
                SET "Version" = {version},
                    "UpdatedAt" = datetime('now'),
                    "LastMigrationDescription" = {description}
                WHERE "Id" = 1
                """, ct);
        }
    }
}
