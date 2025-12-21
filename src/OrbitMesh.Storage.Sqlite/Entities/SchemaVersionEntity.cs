namespace OrbitMesh.Storage.Sqlite.Entities;

/// <summary>
/// Tracks database schema version for migrations.
/// </summary>
public sealed class SchemaVersionEntity
{
    /// <summary>
    /// Singleton ID (always 1).
    /// </summary>
    public int Id { get; set; } = 1;

    /// <summary>
    /// Current schema version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the schema was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Description of the last migration applied.
    /// </summary>
    public string? LastMigrationDescription { get; set; }
}
