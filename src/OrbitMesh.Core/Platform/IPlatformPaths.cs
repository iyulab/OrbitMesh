namespace OrbitMesh.Core.Platform;

/// <summary>
/// Provides platform-specific paths for OrbitMesh data storage.
/// </summary>
public interface IPlatformPaths
{
    /// <summary>
    /// Base path for all OrbitMesh data.
    /// Windows: %USERPROFILE%\orbit-mesh\
    /// Unix: ~/.orbit-mesh/
    /// </summary>
    string BasePath { get; }

    /// <summary>
    /// Path for executable binaries.
    /// </summary>
    string BinPath { get; }

    /// <summary>
    /// Path for data files (database, state).
    /// </summary>
    string DataPath { get; }

    /// <summary>
    /// Path for file storage (synced files).
    /// </summary>
    string FilesPath { get; }

    /// <summary>
    /// Path for log files.
    /// </summary>
    string LogsPath { get; }

    /// <summary>
    /// Path for configuration files.
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    /// Path for version backups (rollback support).
    /// </summary>
    string BackupsPath { get; }

    /// <summary>
    /// Path for staging updates before applying.
    /// </summary>
    string UpdatePath { get; }

    /// <summary>
    /// Full path to the SQLite database file.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Ensures all directories exist.
    /// </summary>
    void EnsureDirectoriesExist();
}
