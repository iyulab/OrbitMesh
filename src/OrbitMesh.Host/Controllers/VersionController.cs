using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// Public API controller for version information.
/// Does not require authentication for health checks and update verification.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class VersionController : ControllerBase
{
    private static readonly Lazy<VersionInfo> CachedVersion = new(GetVersionInfo);

    /// <summary>
    /// Gets the current server version information.
    /// This endpoint is public and does not require authentication.
    /// </summary>
    /// <returns>Version information.</returns>
    /// <response code="200">Version information retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(VersionInfo), StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        return Ok(CachedVersion.Value);
    }

    /// <summary>
    /// Gets minimal version string for quick checks.
    /// </summary>
    /// <returns>Version string.</returns>
    /// <response code="200">Version string retrieved successfully.</response>
    [HttpGet("short")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult GetShortVersion()
    {
        return Ok(CachedVersion.Value.Version);
    }

    private static VersionInfo GetVersionInfo()
    {
        var assembly = typeof(VersionController).Assembly;
        var version = assembly.GetName().Version;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Extract git commit from informational version (format: 0.1.0+abc1234)
        string? gitCommit = null;
        if (informationalVersion?.Contains('+', StringComparison.Ordinal) == true)
        {
            gitCommit = informationalVersion[(informationalVersion.IndexOf('+', StringComparison.Ordinal) + 1)..];
        }

        var buildDate = GetBuildDate(assembly);

        return new VersionInfo
        {
            Version = version?.ToString(3) ?? "0.0.0",
            FullVersion = informationalVersion ?? version?.ToString() ?? "0.0.0",
            GitCommit = gitCommit,
            BuildDate = buildDate?.ToString("o"),
            Runtime = RuntimeInformation.FrameworkDescription,
            Os = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            Product = "OrbitMesh Server",
        };
    }

    private static DateTimeOffset? GetBuildDate(Assembly assembly)
    {
        // Try to get build date from assembly metadata
        var attribute = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
        if (attribute?.Key == "BuildDate" && DateTimeOffset.TryParse(attribute.Value, out var date))
        {
            return date;
        }

        // Fallback to file last write time using AppContext.BaseDirectory (single-file app compatible)
        var assemblyName = assembly.GetName().Name;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var dllPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
            if (System.IO.File.Exists(dllPath))
            {
                return new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(dllPath), TimeSpan.Zero);
            }
        }

        return null;
    }
}

/// <summary>
/// Version information response.
/// </summary>
public sealed record VersionInfo
{
    /// <summary>
    /// Semantic version (e.g., "0.1.0").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Full version including pre-release and build metadata (e.g., "0.1.0+abc1234").
    /// </summary>
    public required string FullVersion { get; init; }

    /// <summary>
    /// Git commit hash if available.
    /// </summary>
    public string? GitCommit { get; init; }

    /// <summary>
    /// Build date in ISO 8601 format.
    /// </summary>
    public string? BuildDate { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string Runtime { get; init; }

    /// <summary>
    /// Operating system information.
    /// </summary>
    public required string Os { get; init; }

    /// <summary>
    /// Product name.
    /// </summary>
    public required string Product { get; init; }
}
