namespace OrbitMesh.Update.Models;

/// <summary>
/// Information about a GitHub release.
/// </summary>
public sealed record ReleaseInfo
{
    /// <summary>
    /// The release tag name (e.g., "v0.1.2").
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Parsed semantic version.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Direct download URL for the release asset.
    /// </summary>
    public required Uri DownloadUrl { get; init; }

    /// <summary>
    /// Size of the release asset in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// When the release was published.
    /// </summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Release name/title.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether this is a pre-release.
    /// </summary>
    public bool IsPrerelease { get; init; }
}
