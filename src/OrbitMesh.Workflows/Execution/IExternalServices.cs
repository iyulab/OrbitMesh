using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Execution;

/// <summary>
/// Interface for dispatching jobs to agents.
/// </summary>
public interface IJobDispatcher
{
    /// <summary>
    /// Dispatches a job to an agent.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="pattern">Agent selection pattern.</param>
    /// <param name="payload">Optional payload data.</param>
    /// <param name="priority">Job priority.</param>
    /// <param name="requiredTags">Required agent tags.</param>
    /// <param name="timeout">Optional timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job dispatch result.</returns>
    Task<JobDispatchResult> DispatchAsync(
        string command,
        string pattern,
        object? payload,
        int priority,
        IReadOnlyList<string>? requiredTags,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a job dispatch operation.
/// </summary>
public sealed record JobDispatchResult
{
    /// <summary>
    /// Whether the job completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The job ID.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// The job result data.
    /// </summary>
    public object? JobResult { get; init; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Interface for launching sub-workflows.
/// </summary>
public interface ISubWorkflowLauncher
{
    /// <summary>
    /// Launches a sub-workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID to launch.</param>
    /// <param name="version">Optional specific version.</param>
    /// <param name="input">Input variables for the sub-workflow.</param>
    /// <param name="parentInstanceId">Parent workflow instance ID.</param>
    /// <param name="parentStepId">Parent step ID.</param>
    /// <param name="waitForCompletion">Whether to wait for completion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sub-workflow launch result.</returns>
    Task<SubWorkflowResult> LaunchAsync(
        string workflowId,
        string? version,
        IReadOnlyDictionary<string, object?>? input,
        string parentInstanceId,
        string parentStepId,
        bool waitForCompletion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a sub-workflow launch operation.
/// </summary>
public sealed record SubWorkflowResult
{
    /// <summary>
    /// Whether the sub-workflow completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The sub-workflow instance ID.
    /// </summary>
    public string? SubWorkflowInstanceId { get; init; }

    /// <summary>
    /// The sub-workflow output.
    /// </summary>
    public object? Output { get; init; }

    /// <summary>
    /// Error message if the sub-workflow failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Interface for sending notifications.
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Sends a notification.
    /// </summary>
    /// <param name="channel">The notification channel.</param>
    /// <param name="target">The notification target (email, webhook URL, etc.).</param>
    /// <param name="message">The message content.</param>
    /// <param name="subject">Optional subject/title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was sent successfully.</returns>
    Task<bool> SendAsync(
        NotifyChannel channel,
        string target,
        string message,
        string? subject,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for notifying approvers.
/// </summary>
public interface IApprovalNotifier
{
    /// <summary>
    /// Notifies approvers of a pending approval.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="stepId">The approval step ID.</param>
    /// <param name="approvers">List of approvers to notify.</param>
    /// <param name="message">Optional message to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyApproversAsync(
        string workflowInstanceId,
        string stepId,
        IReadOnlyList<string> approvers,
        string? message,
        CancellationToken cancellationToken = default);
}
