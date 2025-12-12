using System.Diagnostics.CodeAnalysis;

namespace OrbitMesh.Host.Features;

/// <summary>
/// Configuration options for built-in features.
/// Maps to OrbitMesh:Features section in appsettings.json.
/// </summary>
internal sealed class BuiltInFeatureOptions
{
    /// <summary>
    /// File synchronization feature configuration.
    /// </summary>
    public FileSyncFeatureOptions? FileSync { get; set; }

    /// <summary>
    /// Health monitoring feature configuration.
    /// </summary>
    public HealthMonitorFeatureOptions? HealthMonitor { get; set; }

    /// <summary>
    /// Service management feature configuration.
    /// </summary>
    public ServiceManagementFeatureOptions? ServiceManagement { get; set; }

    /// <summary>
    /// Data collection feature configuration.
    /// </summary>
    public DataCollectionFeatureOptions? DataCollection { get; set; }

    /// <summary>
    /// Script execution feature configuration.
    /// </summary>
    public ScriptExecutionFeatureOptions? ScriptExecution { get; set; }

    /// <summary>
    /// Monitoring feature configuration.
    /// </summary>
    public MonitoringFeatureOptions? Monitoring { get; set; }

    /// <summary>
    /// Deployment feature configuration.
    /// </summary>
    public DeploymentFeatureOptions? Deployment { get; set; }

    /// <summary>
    /// Maintenance feature configuration.
    /// </summary>
    public MaintenanceFeatureOptions? Maintenance { get; set; }
}

/// <summary>
/// File synchronization feature options.
/// </summary>
internal sealed class FileSyncFeatureOptions
{
    /// <summary>
    /// Whether the file sync feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Root path for file storage on the server.
    /// </summary>
    public string RootPath { get; set; } = "./files";

    /// <summary>
    /// Whether to enable file watching for real-time sync.
    /// </summary>
    public bool WatchEnabled { get; set; }

    /// <summary>
    /// File patterns to watch (e.g., "*.txt", "**/*").
    /// </summary>
    public string WatchPattern { get; set; } = "*";

    /// <summary>
    /// Debounce delay in milliseconds for file watch events.
    /// </summary>
    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// Sync mode: TwoWay, ServerToAgents, AgentToServer.
    /// </summary>
    public string SyncMode { get; set; } = "TwoWay";
}

/// <summary>
/// Health monitoring feature options.
/// </summary>
internal sealed class HealthMonitorFeatureOptions
{
    /// <summary>
    /// Whether the health monitor feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Health check interval (e.g., "5m", "30s").
    /// </summary>
    public string Interval { get; set; } = "5m";

    /// <summary>
    /// Agent pattern to monitor (default: all agents).
    /// </summary>
    public string AgentPattern { get; set; } = "*";

    /// <summary>
    /// Whether to auto-restart unhealthy agents.
    /// </summary>
    public bool AutoRestart { get; set; }
}

/// <summary>
/// Service management feature options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class ServiceManagementFeatureOptions
{
    /// <summary>
    /// Whether the service management feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// List of services to manage.
    /// </summary>
    public List<ManagedServiceOptions> Services { get; set; } = [];
}

/// <summary>
/// Options for a managed service.
/// </summary>
internal sealed class ManagedServiceOptions
{
    /// <summary>
    /// Service name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Agent pattern where the service runs.
    /// </summary>
    public string AgentPattern { get; set; } = "*";

    /// <summary>
    /// Whether to enable health checks for this service.
    /// </summary>
    public bool HealthCheckEnabled { get; set; } = true;

    /// <summary>
    /// Whether to auto-restart on failure.
    /// </summary>
    public bool AutoRestart { get; set; }
}

/// <summary>
/// Data collection feature options.
/// </summary>
internal sealed class DataCollectionFeatureOptions
{
    /// <summary>
    /// Whether the data collection feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// System inventory collection options.
    /// </summary>
    public InventoryOptions? Inventory { get; set; }

    /// <summary>
    /// Metrics collection options.
    /// </summary>
    public MetricsCollectionOptions? Metrics { get; set; }

    /// <summary>
    /// Log collection options.
    /// </summary>
    public LogCollectionOptions? Logs { get; set; }
}

/// <summary>
/// System inventory collection options.
/// </summary>
internal sealed class InventoryOptions
{
    /// <summary>
    /// Whether inventory collection is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Collection interval (e.g., "24h", "12h").
    /// </summary>
    public string Interval { get; set; } = "24h";

    /// <summary>
    /// Agent pattern to collect from.
    /// </summary>
    public string AgentPattern { get; set; } = "*";
}

/// <summary>
/// Metrics collection options.
/// </summary>
internal sealed class MetricsCollectionOptions
{
    /// <summary>
    /// Whether metrics collection is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Collection interval (e.g., "1m", "5m").
    /// </summary>
    public string Interval { get; set; } = "1m";

    /// <summary>
    /// Agent pattern to collect from.
    /// </summary>
    public string AgentPattern { get; set; } = "*";

    /// <summary>
    /// CPU usage threshold for alerts.
    /// </summary>
    public int CpuAlertThreshold { get; set; } = 80;

    /// <summary>
    /// Memory usage threshold for alerts.
    /// </summary>
    public int MemoryAlertThreshold { get; set; } = 85;

    /// <summary>
    /// Disk usage threshold for alerts.
    /// </summary>
    public int DiskAlertThreshold { get; set; } = 90;
}

/// <summary>
/// Log collection options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class LogCollectionOptions
{
    /// <summary>
    /// Whether log collection is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Log paths to collect.
    /// </summary>
    public List<string> Paths { get; set; } = ["/var/log/*.log"];

    /// <summary>
    /// Number of lines to collect per log.
    /// </summary>
    public int LinesPerLog { get; set; } = 100;
}

/// <summary>
/// Script execution feature options.
/// </summary>
internal sealed class ScriptExecutionFeatureOptions
{
    /// <summary>
    /// Whether script execution is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default timeout for script execution in seconds.
    /// </summary>
    public int DefaultTimeout { get; set; } = 300;

    /// <summary>
    /// Whether to allow remote command execution.
    /// </summary>
    public bool AllowRemoteExec { get; set; } = true;

    /// <summary>
    /// Whether to allow script deployment.
    /// </summary>
    public bool AllowScriptDeploy { get; set; } = true;
}

/// <summary>
/// Monitoring feature options.
/// </summary>
internal sealed class MonitoringFeatureOptions
{
    /// <summary>
    /// Whether monitoring features are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Disk space monitoring options.
    /// </summary>
    public DiskSpaceMonitorOptions? DiskSpace { get; set; }

    /// <summary>
    /// Process monitoring options.
    /// </summary>
    public ProcessMonitorOptions? Process { get; set; }

    /// <summary>
    /// Connectivity monitoring options.
    /// </summary>
    public ConnectivityMonitorOptions? Connectivity { get; set; }
}

/// <summary>
/// Disk space monitoring options.
/// </summary>
internal sealed class DiskSpaceMonitorOptions
{
    /// <summary>
    /// Whether disk space monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Check interval (e.g., "15m").
    /// </summary>
    public string Interval { get; set; } = "15m";

    /// <summary>
    /// Warning threshold percentage.
    /// </summary>
    public int WarningThreshold { get; set; } = 80;

    /// <summary>
    /// Critical threshold percentage.
    /// </summary>
    public int CriticalThreshold { get; set; } = 90;
}

/// <summary>
/// Process monitoring options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class ProcessMonitorOptions
{
    /// <summary>
    /// Whether process monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Check interval (e.g., "2m").
    /// </summary>
    public string Interval { get; set; } = "2m";

    /// <summary>
    /// Process names to monitor.
    /// </summary>
    public List<string> Processes { get; set; } = [];

    /// <summary>
    /// CPU threshold for alerting.
    /// </summary>
    public int CpuThreshold { get; set; } = 80;

    /// <summary>
    /// Memory threshold for alerting.
    /// </summary>
    public int MemoryThreshold { get; set; } = 80;
}

/// <summary>
/// Connectivity monitoring options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class ConnectivityMonitorOptions
{
    /// <summary>
    /// Whether connectivity monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Check interval (e.g., "10m").
    /// </summary>
    public string Interval { get; set; } = "10m";

    /// <summary>
    /// Endpoints to check connectivity to.
    /// </summary>
    public List<string> Endpoints { get; set; } = [];

    /// <summary>
    /// Timeout per endpoint check in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Deployment feature options.
/// </summary>
internal sealed class DeploymentFeatureOptions
{
    /// <summary>
    /// Whether deployment features are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default deployment strategy.
    /// </summary>
    public string DefaultStrategy { get; set; } = "rolling";

    /// <summary>
    /// Health check command for deployments.
    /// </summary>
    public string? HealthCheckCommand { get; set; }

    /// <summary>
    /// Whether to rollback on failure.
    /// </summary>
    public bool RollbackOnFailure { get; set; } = true;

    /// <summary>
    /// Canary deployment percentage.
    /// </summary>
    public int CanaryPercentage { get; set; } = 10;
}

/// <summary>
/// Maintenance feature options.
/// </summary>
internal sealed class MaintenanceFeatureOptions
{
    /// <summary>
    /// Whether maintenance features are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cleanup options.
    /// </summary>
    public CleanupOptions? Cleanup { get; set; }

    /// <summary>
    /// Log rotation options.
    /// </summary>
    public LogRotationOptions? LogRotation { get; set; }
}

/// <summary>
/// Cleanup options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class CleanupOptions
{
    /// <summary>
    /// Whether cleanup is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cleanup interval (e.g., "24h").
    /// </summary>
    public string Interval { get; set; } = "24h";

    /// <summary>
    /// Paths to clean.
    /// </summary>
    public List<string> Paths { get; set; } = ["/tmp", "/var/tmp"];

    /// <summary>
    /// Delete files older than this many days.
    /// </summary>
    public int OlderThanDays { get; set; } = 7;
}

/// <summary>
/// Log rotation options.
/// </summary>
[SuppressMessage("CA1002", "CA1002:DoNotExposeGenericLists", Justification = "Required for configuration binding")]
[SuppressMessage("CA2227", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Required for configuration binding")]
internal sealed class LogRotationOptions
{
    /// <summary>
    /// Whether log rotation is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Rotation interval (e.g., "24h").
    /// </summary>
    public string Interval { get; set; } = "24h";

    /// <summary>
    /// Log paths to rotate.
    /// </summary>
    public List<string> Paths { get; set; } = ["/var/log/*.log"];

    /// <summary>
    /// Max log size in MB before rotation.
    /// </summary>
    public int MaxSizeMb { get; set; } = 100;

    /// <summary>
    /// Number of rotated logs to keep.
    /// </summary>
    public int KeepRotations { get; set; } = 5;

    /// <summary>
    /// Whether to compress rotated logs.
    /// </summary>
    public bool Compress { get; set; } = true;
}
