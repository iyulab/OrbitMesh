using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// Defines methods that agents can invoke on the server hub.
/// </summary>
public interface IServerHub
{
    /// <summary>
    /// Registers an agent with the server.
    /// </summary>
    /// <param name="agentInfo">The agent information.</param>
    /// <returns>Registration confirmation with server-assigned data.</returns>
    Task<AgentRegistrationResult> RegisterAsync(AgentInfo agentInfo);

    /// <summary>
    /// Unregisters an agent from the server.
    /// </summary>
    /// <param name="agentId">The agent ID to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat to the server.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    Task HeartbeatAsync(string agentId);

    /// <summary>
    /// Acknowledges receipt of a job assignment.
    /// </summary>
    /// <param name="jobId">The job ID being acknowledged.</param>
    /// <param name="agentId">The agent ID acknowledging the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AcknowledgeJobAsync(
        string jobId,
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the result of a completed job.
    /// </summary>
    /// <param name="result">The job result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportResultAsync(JobResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports progress for a long-running job.
    /// </summary>
    /// <param name="progress">The progress information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the agent's current state to the server.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="reportedState">The reported state properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStateAsync(
        string agentId,
        IReadOnlyDictionary<string, string> reportedState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a stream item for a streaming job.
    /// </summary>
    /// <param name="item">The stream item to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStreamItemAsync(StreamItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests enrollment for a new node.
    /// Called during initial connection with a bootstrap token.
    /// </summary>
    /// <param name="request">The enrollment request with node info and public key.</param>
    /// <returns>Enrollment result with status and optional certificate.</returns>
    Task<NodeEnrollmentResult> RequestEnrollmentAsync(NodeEnrollmentRequest request);

    /// <summary>
    /// Checks the status of a pending enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment ID.</param>
    /// <returns>Current enrollment status.</returns>
    Task<NodeEnrollmentResult> CheckEnrollmentStatusAsync(string enrollmentId);
}

/// <summary>
/// Result of agent registration.
/// </summary>
public sealed record AgentRegistrationResult
{
    /// <summary>
    /// Whether the registration was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if registration failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Server-assigned connection metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ServerMetadata { get; init; }

    /// <summary>
    /// Recommended heartbeat interval.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a successful registration result.
    /// </summary>
    public static AgentRegistrationResult Succeeded(TimeSpan? heartbeatInterval = null) =>
        new()
        {
            Success = true,
            HeartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30)
        };

    /// <summary>
    /// Creates a failed registration result.
    /// </summary>
    public static AgentRegistrationResult Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Request for node enrollment.
/// </summary>
public sealed record NodeEnrollmentRequest
{
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Display name for the node.
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// Base64-encoded public key (Ed25519).
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// Capabilities the node is requesting.
    /// </summary>
    public IReadOnlyList<string> RequestedCapabilities { get; init; } = [];

    /// <summary>
    /// Node metadata (hostname, platform, version, etc.).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Signature of the request (for verification).
    /// </summary>
    public required string Signature { get; init; }
}

/// <summary>
/// Result of node enrollment request.
/// </summary>
public sealed record NodeEnrollmentResult
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Enrollment status.
    /// </summary>
    public required NodeEnrollmentStatus Status { get; init; }

    /// <summary>
    /// Enrollment ID for status polling (if pending).
    /// </summary>
    public string? EnrollmentId { get; init; }

    /// <summary>
    /// Base64-encoded certificate (if approved).
    /// </summary>
    public string? Certificate { get; init; }

    /// <summary>
    /// Server's public key for verification.
    /// </summary>
    public string? ServerPublicKey { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Suggested poll interval for pending enrollments.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    public static NodeEnrollmentResult Pending(string enrollmentId) => new()
    {
        Success = true,
        Status = NodeEnrollmentStatus.Pending,
        EnrollmentId = enrollmentId
    };

    public static NodeEnrollmentResult Approved(string certificate, string serverPublicKey) => new()
    {
        Success = true,
        Status = NodeEnrollmentStatus.Approved,
        Certificate = certificate,
        ServerPublicKey = serverPublicKey
    };

    public static NodeEnrollmentResult Rejected(string? reason = null) => new()
    {
        Success = false,
        Status = NodeEnrollmentStatus.Rejected,
        Error = reason ?? "Enrollment rejected"
    };

    public static NodeEnrollmentResult Failed(string error) => new()
    {
        Success = false,
        Status = NodeEnrollmentStatus.Failed,
        Error = error
    };

    public static NodeEnrollmentResult Blocked() => new()
    {
        Success = false,
        Status = NodeEnrollmentStatus.Blocked,
        Error = "Node is blocked from enrollment"
    };
}

/// <summary>
/// Node enrollment status.
/// </summary>
public enum NodeEnrollmentStatus
{
    /// <summary>
    /// Enrollment is pending admin approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Enrollment has been approved.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Enrollment has been rejected.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Enrollment expired.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// Enrollment failed (validation error).
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Node is blocked from enrollment.
    /// </summary>
    Blocked = 5
}
