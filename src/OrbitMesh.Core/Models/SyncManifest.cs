using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Manifest describing files for synchronization.
/// Uses content-based versioning to be completely time-agnostic.
/// </summary>
[MessagePackObject]
public sealed class SyncManifest
{
    /// <summary>
    /// Content-based version hash derived from all file checksums.
    /// Changes when any file is added, removed, or modified.
    /// This is the primary comparison mechanism - NOT timestamps.
    /// </summary>
    [Key(0)]
    public required string ContentHash { get; set; }

    /// <summary>
    /// Monotonically increasing sequence number for this path.
    /// Used to detect stale manifests in concurrent scenarios.
    /// </summary>
    [Key(1)]
    public long SequenceNumber { get; set; }

    /// <summary>
    /// UTC timestamp when this manifest was generated.
    /// For informational/debugging purposes only - NOT used for sync decisions.
    /// </summary>
    [Key(2)]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// List of files in the directory.
    /// </summary>
    [Key(3)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for MessagePack serialization")]
    public IList<SyncFileEntry> Files { get; set; } = new List<SyncFileEntry>();

    /// <summary>
    /// Total size in bytes of all files.
    /// </summary>
    [Key(4)]
    public long TotalSize { get; set; }

    /// <summary>
    /// Total number of files.
    /// </summary>
    [Key(5)]
    public int FileCount { get; set; }

    /// <summary>
    /// Computes a content hash from all file entries.
    /// This provides a time-agnostic way to detect changes.
    /// </summary>
    public static string ComputeContentHash(IEnumerable<SyncFileEntry> files)
    {
        // Sort by path for deterministic ordering
        var sortedFiles = files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase);

        // Build a deterministic string from all file paths and checksums
        var builder = new StringBuilder();
        foreach (var file in sortedFiles)
        {
            builder.Append(file.Path);
            builder.Append('|');
            builder.Append(file.Checksum ?? "null");
            builder.Append('|');
            builder.Append(file.Size);
            builder.Append('\n');
        }

        // Hash the combined content
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Determines if this manifest differs from another based on content hash.
    /// Time-agnostic comparison - ignores timestamps entirely.
    /// </summary>
    public bool DiffersFrom(SyncManifest? other)
    {
        if (other is null) return true;
        return !string.Equals(ContentHash, other.ContentHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets files that need to be added or updated on the target.
    /// </summary>
    public IEnumerable<SyncFileEntry> GetFilesToSync(SyncManifest? targetManifest)
    {
        if (targetManifest is null)
        {
            // No target manifest - sync all files
            return Files;
        }

        var targetLookup = targetManifest.Files
            .ToDictionary(f => f.Path, f => f, StringComparer.OrdinalIgnoreCase);

        return Files.Where(sourceFile =>
        {
            if (!targetLookup.TryGetValue(sourceFile.Path, out var targetFile))
            {
                // File doesn't exist on target
                return true;
            }

            // Compare by checksum (content-based, time-agnostic)
            return !string.Equals(
                sourceFile.Checksum,
                targetFile.Checksum,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Gets files that exist on target but not in source (orphans).
    /// </summary>
    public IEnumerable<SyncFileEntry> GetOrphanFiles(SyncManifest? targetManifest)
    {
        if (targetManifest is null)
        {
            return [];
        }

        var sourcePaths = new HashSet<string>(
            Files.Select(f => f.Path),
            StringComparer.OrdinalIgnoreCase);

        return targetManifest.Files.Where(f => !sourcePaths.Contains(f.Path));
    }
}

/// <summary>
/// Entry in sync manifest representing a single file.
/// </summary>
[MessagePackObject]
public sealed class SyncFileEntry
{
    /// <summary>
    /// Relative path from manifest root.
    /// Uses forward slashes for cross-platform compatibility.
    /// </summary>
    [Key(0)]
    public required string Path { get; set; }

    /// <summary>
    /// SHA256 checksum of the file content.
    /// This is the primary identity mechanism - NOT timestamps.
    /// </summary>
    [Key(1)]
    public string? Checksum { get; set; }

    /// <summary>
    /// File size in bytes.
    /// Used for progress tracking and quick change detection.
    /// </summary>
    [Key(2)]
    public long Size { get; set; }
}
