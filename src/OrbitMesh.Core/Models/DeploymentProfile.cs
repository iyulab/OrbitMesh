using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a deployment profile configuration for automated app deployment.
/// </summary>
[MessagePackObject]
public sealed record DeploymentProfile
{
    /// <summary>
    /// Unique identifier for the profile.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the profile.
    /// </summary>
    [Key(1)]
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of the deployment profile.
    /// </summary>
    [Key(2)]
    public string? Description { get; init; }

    /// <summary>
    /// Source path on the server where deployment files are located.
    /// </summary>
    [Key(3)]
    public required string SourcePath { get; init; }

    /// <summary>
    /// Target agent pattern for matching agents (e.g., "customer-a-*").
    /// </summary>
    [Key(4)]
    public required string TargetAgentPattern { get; init; }

    /// <summary>
    /// Target path on the agent where files will be deployed.
    /// </summary>
    [Key(5)]
    public required string TargetPath { get; init; }

    /// <summary>
    /// Whether to watch for file changes and auto-deploy.
    /// </summary>
    [Key(6)]
    public bool WatchForChanges { get; init; } = true;

    /// <summary>
    /// Debounce delay in milliseconds for file watch events.
    /// </summary>
    [Key(7)]
    public int DebounceMs { get; init; } = 500;

    /// <summary>
    /// File patterns to include in sync (e.g., "*.dll", "wwwroot/**").
    /// </summary>
    [Key(8)]
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>
    /// File patterns to exclude from sync (e.g., "*.pdb", "*.log").
    /// </summary>
    [Key(9)]
    public IReadOnlyList<string>? ExcludePatterns { get; init; }

    /// <summary>
    /// Whether to delete files on agent that don't exist in source.
    /// </summary>
    [Key(10)]
    public bool DeleteOrphans { get; init; }

    /// <summary>
    /// Script to execute before file sync (e.g., stop IIS).
    /// </summary>
    [Key(11)]
    public DeploymentScript? PreDeployScript { get; init; }

    /// <summary>
    /// Script to execute after file sync (e.g., start IIS).
    /// </summary>
    [Key(12)]
    public DeploymentScript? PostDeployScript { get; init; }

    /// <summary>
    /// File transfer mode preference.
    /// </summary>
    [Key(13)]
    public FileTransferMode TransferMode { get; init; } = FileTransferMode.Auto;

    /// <summary>
    /// Whether the profile is enabled.
    /// </summary>
    [Key(14)]
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Timestamp when the profile was created.
    /// </summary>
    [Key(15)]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp of the last deployment.
    /// </summary>
    [Key(16)]
    public DateTimeOffset? LastDeployedAt { get; init; }

    /// <summary>
    /// Generates a new unique profile ID.
    /// </summary>
    public static string GenerateId() => Guid.NewGuid().ToString("N")[..12];
}

/// <summary>
/// Script configuration for pre/post deployment execution.
/// </summary>
[MessagePackObject]
public sealed record DeploymentScript
{
    /// <summary>
    /// Command to execute (e.g., "powershell.exe", "bash").
    /// </summary>
    [Key(0)]
    public required string Command { get; init; }

    /// <summary>
    /// Command arguments.
    /// </summary>
    [Key(1)]
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>
    /// Working directory for script execution.
    /// </summary>
    [Key(2)]
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Timeout in seconds for script execution.
    /// </summary>
    [Key(3)]
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Whether to continue deployment if script fails.
    /// </summary>
    [Key(4)]
    public bool ContinueOnError { get; init; }
}

/// <summary>
/// File transfer mode options.
/// </summary>
public enum FileTransferMode
{
    /// <summary>
    /// Automatically select the best available transfer mode.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Direct P2P connection (fastest when available).
    /// </summary>
    P2PDirect = 1,

    /// <summary>
    /// P2P via TURN relay (NAT-friendly).
    /// </summary>
    P2PTurn = 2,

    /// <summary>
    /// Chunked transfer via SignalR relay.
    /// </summary>
    SignalRRelay = 3,

    /// <summary>
    /// HTTP download from server (universal fallback).
    /// </summary>
    Http = 4
}
