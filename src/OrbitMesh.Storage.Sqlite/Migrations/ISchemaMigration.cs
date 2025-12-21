namespace OrbitMesh.Storage.Sqlite.Migrations;

/// <summary>
/// Defines a database schema migration.
/// </summary>
public interface ISchemaMigration
{
    /// <summary>
    /// The version number this migration upgrades to.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Description of what this migration does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the migration.
    /// </summary>
    Task ExecuteAsync(OrbitMeshDbContext context, CancellationToken ct = default);
}
