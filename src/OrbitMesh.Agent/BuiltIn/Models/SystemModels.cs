using MessagePack;

namespace OrbitMesh.Agent.BuiltIn.Models;

/// <summary>
/// Health check result.
/// </summary>
[MessagePackObject]
public sealed record HealthCheckResult
{
    /// <summary>
    /// Whether the agent is healthy.
    /// </summary>
    [Key(0)]
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Agent uptime.
    /// </summary>
    [Key(1)]
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Individual health checks.
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<string, HealthCheckEntry>? Checks { get; init; }

    /// <summary>
    /// Overall error message if unhealthy.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }
}

/// <summary>
/// Individual health check entry.
/// </summary>
[MessagePackObject]
public sealed record HealthCheckEntry
{
    /// <summary>
    /// Whether this check passed.
    /// </summary>
    [Key(0)]
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Check description or result.
    /// </summary>
    [Key(1)]
    public string? Description { get; init; }

    /// <summary>
    /// Additional data.
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<string, string>? Data { get; init; }
}

/// <summary>
/// Agent version information.
/// </summary>
[MessagePackObject]
public sealed record VersionInfo
{
    /// <summary>
    /// Agent application version.
    /// </summary>
    [Key(0)]
    public required string Version { get; init; }

    /// <summary>
    /// OrbitMesh SDK version.
    /// </summary>
    [Key(1)]
    public required string SdkVersion { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    [Key(2)]
    public required string RuntimeVersion { get; init; }

    /// <summary>
    /// Operating system description.
    /// </summary>
    [Key(3)]
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// Platform identifier (e.g., "win-x64", "linux-x64").
    /// </summary>
    [Key(4)]
    public required string Platform { get; init; }

    /// <summary>
    /// Machine hostname.
    /// </summary>
    [Key(5)]
    public required string Hostname { get; init; }

    /// <summary>
    /// Build timestamp.
    /// </summary>
    [Key(6)]
    public DateTimeOffset? BuildTime { get; init; }
}

/// <summary>
/// System metrics result.
/// </summary>
[MessagePackObject]
public sealed record SystemMetrics
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    [Key(0)]
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Total physical memory in bytes.
    /// </summary>
    [Key(1)]
    public long TotalMemoryBytes { get; init; }

    /// <summary>
    /// Available physical memory in bytes.
    /// </summary>
    [Key(2)]
    public long AvailableMemoryBytes { get; init; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    [Key(3)]
    public double MemoryUsagePercent { get; init; }

    /// <summary>
    /// Disk information.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<DiskInfo>? Disks { get; init; }

    /// <summary>
    /// Current process memory in bytes.
    /// </summary>
    [Key(5)]
    public long ProcessMemoryBytes { get; init; }

    /// <summary>
    /// Timestamp of metrics collection.
    /// </summary>
    [Key(6)]
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Disk information.
/// </summary>
[MessagePackObject]
public sealed record DiskInfo
{
    /// <summary>
    /// Drive name or mount point.
    /// </summary>
    [Key(0)]
    public required string Name { get; init; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    [Key(1)]
    public long TotalBytes { get; init; }

    /// <summary>
    /// Available space in bytes.
    /// </summary>
    [Key(2)]
    public long AvailableBytes { get; init; }

    /// <summary>
    /// Usage percentage (0-100).
    /// </summary>
    [Key(3)]
    public double UsagePercent { get; init; }
}

/// <summary>
/// Request to execute a shell command.
/// </summary>
[MessagePackObject]
public sealed record ExecuteRequest
{
    /// <summary>
    /// Command to execute.
    /// </summary>
    [Key(0)]
    public required string Command { get; init; }

    /// <summary>
    /// Command arguments.
    /// </summary>
    [Key(1)]
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>
    /// Working directory.
    /// </summary>
    [Key(2)]
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Environment variables.
    /// </summary>
    [Key(3)]
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    [Key(4)]
    public int TimeoutSeconds { get; init; } = 60;
}

/// <summary>
/// Result of command execution.
/// </summary>
[MessagePackObject]
public sealed record ExecuteResult
{
    /// <summary>
    /// Whether execution was successful (exit code 0).
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Exit code.
    /// </summary>
    [Key(1)]
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output.
    /// </summary>
    [Key(2)]
    public string? StandardOutput { get; init; }

    /// <summary>
    /// Standard error.
    /// </summary>
    [Key(3)]
    public string? StandardError { get; init; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    [Key(4)]
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if execution failed to start.
    /// </summary>
    [Key(5)]
    public string? Error { get; init; }
}

/// <summary>
/// Ping response.
/// </summary>
[MessagePackObject]
public sealed record PingResult
{
    /// <summary>
    /// Agent ID.
    /// </summary>
    [Key(0)]
    public required string AgentId { get; init; }

    /// <summary>
    /// Response timestamp.
    /// </summary>
    [Key(1)]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Agent uptime.
    /// </summary>
    [Key(2)]
    public TimeSpan Uptime { get; init; }
}

/// <summary>
/// Service control request.
/// </summary>
[MessagePackObject]
public sealed record ServiceControlRequest
{
    /// <summary>
    /// Service name or identifier.
    /// </summary>
    [Key(0)]
    public required string ServiceName { get; init; }

    /// <summary>
    /// Timeout for the operation.
    /// </summary>
    [Key(1)]
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Service control result.
/// </summary>
[MessagePackObject]
public sealed record ServiceControlResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Service name.
    /// </summary>
    [Key(1)]
    public required string ServiceName { get; init; }

    /// <summary>
    /// Current service state.
    /// </summary>
    [Key(2)]
    public ServiceState State { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Key(3)]
    public string? Error { get; init; }
}

/// <summary>
/// Service state.
/// </summary>
public enum ServiceState
{
    Unknown = 0,
    Stopped = 1,
    Starting = 2,
    Running = 3,
    Stopping = 4,
    Paused = 5
}
