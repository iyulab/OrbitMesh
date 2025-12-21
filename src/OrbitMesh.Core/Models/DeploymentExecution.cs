using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents the execution of a deployment profile.
/// </summary>
[MessagePackObject]
public sealed record DeploymentExecution
{
    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// The deployment profile ID that was executed.
    /// </summary>
    [Key(1)]
    public required string ProfileId { get; init; }

    /// <summary>
    /// Current status of the execution.
    /// </summary>
    [Key(2)]
    public DeploymentStatus Status { get; init; } = DeploymentStatus.Pending;

    /// <summary>
    /// What triggered this deployment.
    /// </summary>
    [Key(3)]
    public DeploymentTrigger Trigger { get; init; } = DeploymentTrigger.Manual;

    /// <summary>
    /// When the execution started.
    /// </summary>
    [Key(4)]
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the execution completed (success or failure).
    /// </summary>
    [Key(5)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Total number of target agents.
    /// </summary>
    [Key(6)]
    public int TotalAgents { get; init; }

    /// <summary>
    /// Number of agents that completed successfully.
    /// </summary>
    [Key(7)]
    public int SuccessfulAgents { get; init; }

    /// <summary>
    /// Number of agents that failed.
    /// </summary>
    [Key(8)]
    public int FailedAgents { get; init; }

    /// <summary>
    /// Per-agent execution results.
    /// </summary>
    [Key(9)]
    public IReadOnlyList<AgentDeploymentResult>? AgentResults { get; init; }

    /// <summary>
    /// Overall error message if the execution failed.
    /// </summary>
    [Key(10)]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total bytes transferred during this execution.
    /// </summary>
    [Key(11)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Total files transferred during this execution.
    /// </summary>
    [Key(12)]
    public int FilesTransferred { get; init; }

    /// <summary>
    /// Generates a new unique execution ID.
    /// </summary>
    public static string GenerateId() => Guid.NewGuid().ToString("N")[..16];
}

/// <summary>
/// Result of deploying to a single agent.
/// </summary>
[MessagePackObject]
public sealed record AgentDeploymentResult
{
    /// <summary>
    /// The agent ID that was deployed to.
    /// </summary>
    [Key(0)]
    public required string AgentId { get; init; }

    /// <summary>
    /// Display name of the agent.
    /// </summary>
    [Key(1)]
    public string? AgentName { get; init; }

    /// <summary>
    /// Status of this agent's deployment.
    /// </summary>
    [Key(2)]
    public AgentDeploymentStatus Status { get; init; } = AgentDeploymentStatus.Pending;

    /// <summary>
    /// Pre-deploy script execution result.
    /// </summary>
    [Key(3)]
    public ScriptExecutionResult? PreDeployResult { get; init; }

    /// <summary>
    /// File sync result.
    /// </summary>
    [Key(4)]
    public FileSyncExecutionResult? FileSyncResult { get; init; }

    /// <summary>
    /// Post-deploy script execution result.
    /// </summary>
    [Key(5)]
    public ScriptExecutionResult? PostDeployResult { get; init; }

    /// <summary>
    /// When deployment to this agent started.
    /// </summary>
    [Key(6)]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When deployment to this agent completed.
    /// </summary>
    [Key(7)]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Error message if deployment to this agent failed.
    /// </summary>
    [Key(8)]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a script execution (pre or post deploy).
/// </summary>
[MessagePackObject]
public sealed record ScriptExecutionResult
{
    /// <summary>
    /// Whether the script executed successfully.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Exit code of the script.
    /// </summary>
    [Key(1)]
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the script.
    /// </summary>
    [Key(2)]
    public string? StandardOutput { get; init; }

    /// <summary>
    /// Standard error from the script.
    /// </summary>
    [Key(3)]
    public string? StandardError { get; init; }

    /// <summary>
    /// Duration of script execution.
    /// </summary>
    [Key(4)]
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Result of file synchronization.
/// </summary>
[MessagePackObject]
public sealed record FileSyncExecutionResult
{
    /// <summary>
    /// Whether the sync completed successfully.
    /// </summary>
    [Key(0)]
    public bool Success { get; init; }

    /// <summary>
    /// Number of files created.
    /// </summary>
    [Key(1)]
    public int FilesCreated { get; init; }

    /// <summary>
    /// Number of files updated.
    /// </summary>
    [Key(2)]
    public int FilesUpdated { get; init; }

    /// <summary>
    /// Number of files deleted.
    /// </summary>
    [Key(3)]
    public int FilesDeleted { get; init; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    [Key(4)]
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Transfer mode used for this sync.
    /// </summary>
    [Key(5)]
    public FileTransferMode TransferMode { get; init; }

    /// <summary>
    /// Duration of the sync operation.
    /// </summary>
    [Key(6)]
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    [Key(7)]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Status of a deployment execution.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is queued but not started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Deployment is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Deployment completed successfully for all agents.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// Deployment completed but some agents failed.
    /// </summary>
    PartialSuccess = 3,

    /// <summary>
    /// Deployment failed for all agents.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Deployment was cancelled.
    /// </summary>
    Cancelled = 5
}

/// <summary>
/// What triggered a deployment.
/// </summary>
public enum DeploymentTrigger
{
    /// <summary>
    /// Manually triggered by user.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Triggered by file system change detection.
    /// </summary>
    FileWatch = 1,

    /// <summary>
    /// Triggered by API call.
    /// </summary>
    Api = 2,

    /// <summary>
    /// Triggered by scheduled task.
    /// </summary>
    Scheduled = 3
}

/// <summary>
/// Status of deployment to a single agent.
/// </summary>
public enum AgentDeploymentStatus
{
    /// <summary>
    /// Waiting to start.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Running pre-deploy script.
    /// </summary>
    RunningPreScript = 1,

    /// <summary>
    /// Syncing files.
    /// </summary>
    SyncingFiles = 2,

    /// <summary>
    /// Running post-deploy script.
    /// </summary>
    RunningPostScript = 3,

    /// <summary>
    /// Deployment succeeded.
    /// </summary>
    Succeeded = 4,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Agent was offline or unreachable.
    /// </summary>
    Unreachable = 6,

    /// <summary>
    /// Deployment was skipped.
    /// </summary>
    Skipped = 7
}
