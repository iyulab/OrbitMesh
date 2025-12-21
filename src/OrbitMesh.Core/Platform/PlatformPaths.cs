namespace OrbitMesh.Core.Platform;

/// <summary>
/// Platform-specific path provider for OrbitMesh data storage.
/// Provides consistent paths across Windows, Linux, and macOS.
/// </summary>
public sealed class PlatformPaths : IPlatformPaths
{
    private const string OrbitMeshDirName = "orbit-mesh";
    private const string DatabaseFileName = "orbitmesh.db";

    /// <inheritdoc />
    public string BasePath { get; }

    /// <inheritdoc />
    public string BinPath { get; }

    /// <inheritdoc />
    public string DataPath { get; }

    /// <inheritdoc />
    public string FilesPath { get; }

    /// <inheritdoc />
    public string LogsPath { get; }

    /// <inheritdoc />
    public string ConfigPath { get; }

    /// <inheritdoc />
    public string BackupsPath { get; }

    /// <inheritdoc />
    public string UpdatePath { get; }

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <summary>
    /// Creates a new PlatformPaths instance.
    /// </summary>
    /// <param name="customBasePath">Optional custom base path. If null, uses platform default.</param>
    public PlatformPaths(string? customBasePath = null)
    {
        BasePath = customBasePath ?? GetDefaultBasePath();
        BinPath = Path.Combine(BasePath, "bin");
        DataPath = Path.Combine(BasePath, "data");
        FilesPath = Path.Combine(BasePath, "files");
        LogsPath = Path.Combine(BasePath, "logs");
        ConfigPath = Path.Combine(BasePath, "config");
        BackupsPath = Path.Combine(BasePath, "backups");
        UpdatePath = Path.Combine(BasePath, "update");
        DatabasePath = Path.Combine(DataPath, DatabaseFileName);
    }

    /// <inheritdoc />
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(BasePath);
        Directory.CreateDirectory(BinPath);
        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(FilesPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(ConfigPath);
        Directory.CreateDirectory(BackupsPath);
        Directory.CreateDirectory(UpdatePath);
    }

    /// <summary>
    /// Gets the default base path for the current platform.
    /// </summary>
    private static string GetDefaultBasePath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            // Windows: %USERPROFILE%\orbit-mesh\
            return Path.Combine(userProfile, OrbitMeshDirName);
        }
        else
        {
            // Unix/macOS: ~/.orbit-mesh/
            return Path.Combine(userProfile, $".{OrbitMeshDirName}");
        }
    }

    /// <summary>
    /// Gets a path relative to the base path.
    /// </summary>
    /// <param name="relativePath">Relative path segments.</param>
    /// <returns>Full path.</returns>
    public string GetPath(params string[] relativePath)
    {
        var parts = new string[relativePath.Length + 1];
        parts[0] = BasePath;
        Array.Copy(relativePath, 0, parts, 1, relativePath.Length);
        return Path.Combine(parts);
    }
}
