namespace OrbitMesh.Storage.Sqlite;

/// <summary>
/// Configuration options for SQLite storage.
/// All recommended options are enabled by default.
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
    /// Default: true (recommended)
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Whether to automatically apply migrations on startup.
    /// Default: true (recommended)
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

    /// <summary>
    /// Whether to enable SQLite-backed security stores (bootstrap tokens, enrollments, certificates).
    /// When enabled, replaces in-memory security implementations with persistent storage.
    /// Default: true (recommended for production)
    /// </summary>
    public bool EnableSecurityStores { get; set; } = true;

    /// <summary>
    /// Whether to enable core storage (jobs, agents, workflows, events).
    /// Default: true
    /// </summary>
    public bool EnableCoreStores { get; set; } = true;

    /// <summary>
    /// Whether to enable deployment profile storage (deployment profiles, executions).
    /// Default: true (recommended for deployment features)
    /// </summary>
    public bool EnableDeploymentStores { get; set; } = true;
}
