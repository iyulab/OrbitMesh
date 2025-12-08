using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Server.Services.Workflows;

/// <summary>
/// Background service that processes scheduled workflow triggers.
/// </summary>
public sealed class ScheduleTriggerHostedService : BackgroundService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly ILogger<ScheduleTriggerHostedService> _logger;
    private readonly ScheduleTriggerOptions _options;

    private readonly ConcurrentDictionary<string, ScheduleEntry> _schedules = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    public ScheduleTriggerHostedService(
        IWorkflowEngine workflowEngine,
        IWorkflowRegistry workflowRegistry,
        IOptions<ScheduleTriggerOptions> options,
        ILogger<ScheduleTriggerHostedService> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowRegistry = workflowRegistry;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Registers a schedule trigger for a workflow.
    /// </summary>
    public void RegisterSchedule(string workflowId, string workflowVersion, ScheduleTrigger trigger)
    {
        var key = $"{workflowId}:{trigger.Id}";

        var entry = new ScheduleEntry
        {
            WorkflowId = workflowId,
            WorkflowVersion = workflowVersion,
            Trigger = trigger,
            NextRun = CalculateNextRun(trigger),
            IsEnabled = trigger.IsEnabled
        };

        _schedules[key] = entry;

        _logger.LogInformation(
            "Registered schedule trigger. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}, NextRun: {NextRun}",
            workflowId, trigger.Id, entry.NextRun);
    }

    /// <summary>
    /// Unregisters all schedule triggers for a workflow.
    /// </summary>
    public void UnregisterSchedules(string workflowId)
    {
        var keysToRemove = _schedules.Keys.Where(k => k.StartsWith($"{workflowId}:", StringComparison.Ordinal)).ToList();

        foreach (var key in keysToRemove)
        {
            if (_schedules.TryRemove(key, out _))
            {
                _logger.LogInformation("Unregistered schedule trigger. Key: {Key}", key);
            }
        }
    }

    /// <summary>
    /// Enables or disables a schedule trigger.
    /// </summary>
    public void SetEnabled(string workflowId, string triggerId, bool enabled)
    {
        var key = $"{workflowId}:{triggerId}";

        if (_schedules.TryGetValue(key, out var entry))
        {
            entry.IsEnabled = enabled;
            if (enabled)
            {
                entry.NextRun = CalculateNextRun(entry.Trigger);
            }

            _logger.LogInformation(
                "Schedule trigger {Action}. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}",
                enabled ? "enabled" : "disabled", workflowId, triggerId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Schedule trigger service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled triggers");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Schedule trigger service stopped");
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dueSchedules = _schedules.Values
            .Where(s => s.IsEnabled && s.NextRun <= now)
            .ToList();

        foreach (var entry in dueSchedules)
        {
            try
            {
                // Check concurrent executions limit
                if (entry.RunningCount >= entry.Trigger.MaxConcurrentExecutions)
                {
                    _logger.LogDebug(
                        "Skipping schedule due to concurrent execution limit. WorkflowId: {WorkflowId}, RunningCount: {RunningCount}",
                        entry.WorkflowId, entry.RunningCount);
                    continue;
                }

                // Check end date
                if (entry.Trigger.EndAt.HasValue && now > entry.Trigger.EndAt.Value)
                {
                    entry.IsEnabled = false;
                    _logger.LogInformation(
                        "Schedule disabled (past end date). WorkflowId: {WorkflowId}, TriggerId: {TriggerId}",
                        entry.WorkflowId, entry.Trigger.Id);
                    continue;
                }

                // Get workflow definition
                var workflow = await _workflowRegistry.GetAsync(
                    entry.WorkflowId,
                    entry.WorkflowVersion,
                    cancellationToken);

                if (workflow is null)
                {
                    _logger.LogWarning(
                        "Workflow not found for scheduled trigger. WorkflowId: {WorkflowId}",
                        entry.WorkflowId);
                    continue;
                }

                // Track running count
                Interlocked.Increment(ref entry.RunningCount);

                // Start workflow asynchronously
                _ = ExecuteScheduledWorkflowAsync(workflow, entry, cancellationToken);

                // Update next run time
                entry.LastRun = now;
                entry.NextRun = CalculateNextRun(entry.Trigger, now);

                _logger.LogInformation(
                    "Scheduled workflow triggered. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}, NextRun: {NextRun}",
                    entry.WorkflowId, entry.Trigger.Id, entry.NextRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error triggering scheduled workflow. WorkflowId: {WorkflowId}",
                    entry.WorkflowId);
            }
        }
    }

    private async Task ExecuteScheduledWorkflowAsync(
        WorkflowDefinition workflow,
        ScheduleEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _workflowEngine.StartAsync(
                workflow,
                input: null,
                triggerId: entry.Trigger.Id,
                correlationId: null,
                cancellationToken);

            _logger.LogInformation(
                "Scheduled workflow started. WorkflowId: {WorkflowId}, InstanceId: {InstanceId}",
                workflow.Id, instance.Id);

            // Wait for completion to track running count
            await WaitForCompletionAsync(instance.Id, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref entry.RunningCount);
        }
    }

    private async Task WaitForCompletionAsync(string instanceId, CancellationToken cancellationToken)
    {
        const int pollIntervalMs = 500;
        const int maxWaitMinutes = 60;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(maxWaitMinutes);

        while (!cancellationToken.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            var instance = await _workflowEngine.GetInstanceAsync(instanceId, cancellationToken);

            if (instance is null || instance.IsTerminal)
            {
                return;
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
        }
    }

    private static DateTimeOffset? CalculateNextRun(ScheduleTrigger trigger, DateTimeOffset? from = null)
    {
        var now = from ?? DateTimeOffset.UtcNow;

        // Check if before start date
        if (trigger.StartAt.HasValue && now < trigger.StartAt.Value)
        {
            return trigger.StartAt.Value;
        }

        // Check if past end date
        if (trigger.EndAt.HasValue && now > trigger.EndAt.Value)
        {
            return null;
        }

        // Use interval if specified
        if (trigger.Interval.HasValue)
        {
            return now.Add(trigger.Interval.Value);
        }

        // Parse cron expression
        if (!string.IsNullOrEmpty(trigger.CronExpression))
        {
            return ParseCronNextRun(trigger.CronExpression, now);
        }

        return null;
    }

    private static DateTimeOffset? ParseCronNextRun(string cronExpression, DateTimeOffset from)
    {
        // Simple cron parser for common patterns
        // Format: minute hour day month dayOfWeek
        // Supports: * (any), */N (every N), specific values

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return null;
        }

        // For simplicity, implement common patterns
        // Full cron parsing would require a library like Cronos

        // Pattern: "* * * * *" - every minute
        if (parts.All(p => p == "*"))
        {
            return from.AddMinutes(1);
        }

        // Pattern: "0 * * * *" - every hour
        if (parts[0] == "0" && parts.Skip(1).All(p => p == "*"))
        {
            var next = from.AddHours(1);
            return new DateTimeOffset(next.Year, next.Month, next.Day, next.Hour, 0, 0, next.Offset);
        }

        // Pattern: "0 0 * * *" - daily at midnight
        if (parts[0] == "0" && parts[1] == "0" && parts.Skip(2).All(p => p == "*"))
        {
            var next = from.AddDays(1);
            return new DateTimeOffset(next.Year, next.Month, next.Day, 0, 0, 0, next.Offset);
        }

        // Default: add 1 hour for unsupported patterns
        return from.AddHours(1);
    }

    private sealed class ScheduleEntry
    {
        public required string WorkflowId { get; init; }
        public required string WorkflowVersion { get; init; }
        public required ScheduleTrigger Trigger { get; init; }
        public DateTimeOffset? NextRun { get; set; }
        public DateTimeOffset? LastRun { get; set; }
        public bool IsEnabled { get; set; }
        public int RunningCount;
    }
}

/// <summary>
/// Options for the schedule trigger service.
/// </summary>
public sealed class ScheduleTriggerOptions
{
    /// <summary>
    /// Whether to enable the schedule trigger service.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default timezone for cron expressions.
    /// </summary>
    public string DefaultTimezone { get; set; } = "UTC";
}
