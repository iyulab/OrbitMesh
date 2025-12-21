#if WINDOWS
using Microsoft.Win32;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// Manages Windows auto-start registry settings.
/// </summary>
internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Checks if auto-start is enabled for the specified application.
    /// </summary>
    public static bool IsAutoStartEnabled(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(appName) != null;
    }

    /// <summary>
    /// Enables or disables auto-start for the current application.
    /// </summary>
    public static void SetAutoStart(string appName, bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(appName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(appName, false);
        }
    }

    /// <summary>
    /// Gets the current executable path registered for auto-start.
    /// </summary>
    public static string? GetAutoStartPath(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(appName) as string;
    }
}
#endif
