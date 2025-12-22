#if WINDOWS
using System.IO;
using System.Text.Json;
using OrbitMesh.Core.Platform;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// Agent settings persisted to disk.
/// </summary>
internal sealed class AgentSettings
{
    private const string SettingsFileName = "agent-settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Server URL (e.g., http://localhost:5000 or http://localhost:5000/agent)
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets the normalized server URL with /agent path appended if missing.
    /// </summary>
    public string NormalizedServerUrl => NormalizeServerUrl(ServerUrl);

    /// <summary>
    /// Normalizes the server URL to ensure it ends with /agent.
    /// </summary>
    private static string NormalizeServerUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        url = url.TrimEnd('/');

        if (!url.EndsWith("/agent", StringComparison.OrdinalIgnoreCase))
        {
            url += "/agent";
        }

        return url;
    }

    /// <summary>
    /// Bootstrap token for TOFU enrollment
    /// </summary>
    public string BootstrapToken { get; set; } = string.Empty;

    /// <summary>
    /// Agent name (defaults to machine name if empty)
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Whether settings have been configured
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServerUrl);

    /// <summary>
    /// Gets the settings file path.
    /// </summary>
    public static string GetSettingsPath()
    {
        var platformPaths = new PlatformPaths();
        return Path.Combine(platformPaths.ConfigPath, SettingsFileName);
    }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    public static AgentSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new AgentSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AgentSettings>(json, JsonOptions) ?? new AgentSettings();
        }
        catch
        {
            return new AgentSettings();
        }
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void Save()
    {
        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }
}
#endif
