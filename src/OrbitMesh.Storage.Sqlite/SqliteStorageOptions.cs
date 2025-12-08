namespace OrbitMesh.Storage.Sqlite;

/// <summary>
/// Configuration options for SQLite storage.
/// </summary>
public sealed class SqliteStorageOptions
{
    /// <summary>
    /// Connection string for the SQLite database.
    /// Default: "Data Source=orbitmesh.db"
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=orbitmesh.db";

    /// <summary>
    /// Whether to enable WAL (Write-Ahead Logging) mode.
    /// WAL provides better concurrency for read operations.
    /// Default: true
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Whether to automatically apply migrations on startup.
    /// Default: true
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Cache size in pages. Negative value means KB.
    /// Default: -2000 (2MB)
    /// </summary>
    public int CacheSize { get; set; } = -2000;

    /// <summary>
    /// Busy timeout in milliseconds.
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int BusyTimeout { get; set; } = 5000;
}
