using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Host.Services.Workflows;

/// <summary>
/// Default implementation of workflow trigger service.
/// </summary>
public sealed class WorkflowTriggerService : IWorkflowTriggerService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly ILogger<WorkflowTriggerService> _logger;

    private readonly ConcurrentDictionary<string, TriggerRegistration> _triggers = new();
    private readonly ConcurrentDictionary<string, List<TriggerRegistration>> _eventTriggers = new();
    private readonly ConcurrentDictionary<string, List<TriggerRegistration>> _webhookTriggers = new();

    public WorkflowTriggerService(
        IWorkflowEngine workflowEngine,
        IWorkflowRegistry workflowRegistry,
        ILogger<WorkflowTriggerService> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowRegistry = workflowRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RegisterTriggersAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        if (workflow.Triggers is null || workflow.Triggers.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var trigger in workflow.Triggers)
        {
            var registration = new TriggerRegistration
            {
                WorkflowId = workflow.Id,
                WorkflowVersion = workflow.Version,
                Trigger = trigger,
                IsEnabled = trigger.IsEnabled
            };

            var key = $"{workflow.Id}:{trigger.Id}";
            _triggers[key] = registration;

            // Index by trigger type for faster lookup
            switch (trigger)
            {
                case EventTrigger eventTrigger:
                    _eventTriggers.AddOrUpdate(
                        eventTrigger.EventType,
                        _ => [registration],
                        (_, list) => { list.Add(registration); return list; });
                    break;

                case WebhookTrigger webhookTrigger:
                    _webhookTriggers.AddOrUpdate(
                        webhookTrigger.Path.ToUpperInvariant(),
                        _ => [registration],
                        (_, list) => { list.Add(registration); return list; });
                    break;
            }

            _logger.LogInformation(
                "Registered trigger. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}, Type: {TriggerType}",
                workflow.Id, trigger.Id, trigger.GetType().Name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterTriggersAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _triggers.Keys.Where(k => k.StartsWith($"{workflowId}:", StringComparison.Ordinal)).ToList();

        foreach (var key in keysToRemove)
        {
            if (_triggers.TryRemove(key, out var registration))
            {
                // Remove from type-specific indexes
                switch (registration.Trigger)
                {
                    case EventTrigger eventTrigger:
                        if (_eventTriggers.TryGetValue(eventTrigger.EventType, out var eventList))
                        {
                            eventList.RemoveAll(r => r.WorkflowId == workflowId);
                        }
                        break;

                    case WebhookTrigger webhookTrigger:
                        if (_webhookTriggers.TryGetValue(webhookTrigger.Path.ToUpperInvariant(), out var webhookList))
                        {
                            webhookList.RemoveAll(r => r.WorkflowId == workflowId);
                        }
                        break;
                }

                _logger.LogInformation(
                    "Unregistered trigger. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}",
                    workflowId, registration.Trigger.Id);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ProcessEventAsync(
        string eventType,
        object? eventData,
        CancellationToken cancellationToken = default)
    {
        var triggeredInstances = new List<string>();

        if (!_eventTriggers.TryGetValue(eventType, out var registrations))
        {
            return triggeredInstances;
        }

        foreach (var registration in registrations.Where(r => r.IsEnabled))
        {
            try
            {
                var eventTrigger = (EventTrigger)registration.Trigger;

                // Check filter if present
                if (!string.IsNullOrEmpty(eventTrigger.Filter))
                {
                    // Simple filter evaluation - could be expanded
                    if (!EvaluateFilter(eventTrigger.Filter, eventData))
                    {
                        continue;
                    }
                }

                // Map input
                var input = MapInput(eventTrigger.InputMapping, eventData);

                // Get workflow definition
                var workflow = await _workflowRegistry.GetAsync(
                    registration.WorkflowId,
                    registration.WorkflowVersion,
                    cancellationToken);

                if (workflow is null)
                {
                    _logger.LogWarning(
                        "Workflow not found for trigger. WorkflowId: {WorkflowId}, Version: {Version}",
                        registration.WorkflowId, registration.WorkflowVersion);
                    continue;
                }

                // Start workflow
                var instance = await _workflowEngine.StartAsync(
                    workflow,
                    input,
                    triggerId: registration.Trigger.Id,
                    correlationId: null,
                    cancellationToken);

                triggeredInstances.Add(instance.Id);

                _logger.LogInformation(
                    "Event triggered workflow. EventType: {EventType}, WorkflowId: {WorkflowId}, InstanceId: {InstanceId}",
                    eventType, registration.WorkflowId, instance.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to trigger workflow from event. EventType: {EventType}, WorkflowId: {WorkflowId}",
                    eventType, registration.WorkflowId);
            }
        }

        return triggeredInstances;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ProcessWebhookAsync(
        string path,
        string method,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        var triggeredInstances = new List<string>();
        var normalizedPath = path.ToUpperInvariant();

        if (!_webhookTriggers.TryGetValue(normalizedPath, out var registrations))
        {
            return triggeredInstances;
        }

        foreach (var registration in registrations.Where(r => r.IsEnabled))
        {
            try
            {
                var webhookTrigger = (WebhookTrigger)registration.Trigger;

                // Check method
                if (!webhookTrigger.Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Validate secret if configured
                if (!string.IsNullOrEmpty(webhookTrigger.Secret))
                {
                    if (headers is null ||
                        !headers.TryGetValue("X-Webhook-Secret", out var secret) ||
                        secret != webhookTrigger.Secret)
                    {
                        _logger.LogWarning(
                            "Webhook secret validation failed. Path: {Path}, WorkflowId: {WorkflowId}",
                            path, registration.WorkflowId);
                        continue;
                    }
                }

                // Map input
                var input = MapInput(webhookTrigger.InputMapping, body);

                // Get workflow definition
                var workflow = await _workflowRegistry.GetAsync(
                    registration.WorkflowId,
                    registration.WorkflowVersion,
                    cancellationToken);

                if (workflow is null)
                {
                    continue;
                }

                // Start workflow
                var instance = await _workflowEngine.StartAsync(
                    workflow,
                    input,
                    triggerId: registration.Trigger.Id,
                    correlationId: null,
                    cancellationToken);

                triggeredInstances.Add(instance.Id);

                _logger.LogInformation(
                    "Webhook triggered workflow. Path: {Path}, WorkflowId: {WorkflowId}, InstanceId: {InstanceId}",
                    path, registration.WorkflowId, instance.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to trigger workflow from webhook. Path: {Path}, WorkflowId: {WorkflowId}",
                    path, registration.WorkflowId);
            }
        }

        return triggeredInstances;
    }

    /// <inheritdoc />
    public async Task<string> TriggerManuallyAsync(
        string workflowId,
        IReadOnlyDictionary<string, object?>? input,
        string? initiatedBy,
        CancellationToken cancellationToken = default)
    {
        // Get workflow definition
        var workflow = await _workflowRegistry.GetAsync(workflowId, version: null, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' not found");

        // Find manual trigger if exists
        var manualTrigger = workflow.Triggers?.OfType<ManualTrigger>().FirstOrDefault();

        // Validate input if trigger has schema
        if (manualTrigger?.InputSchema is not null)
        {
            ValidateInput(manualTrigger.InputSchema, input);
        }

        // Start workflow
        var instance = await _workflowEngine.StartAsync(
            workflow,
            input,
            triggerId: manualTrigger?.Id,
            correlationId: null,
            cancellationToken);

        _logger.LogInformation(
            "Manually triggered workflow. WorkflowId: {WorkflowId}, InstanceId: {InstanceId}, InitiatedBy: {InitiatedBy}",
            workflowId, instance.Id, initiatedBy ?? "unknown");

        return instance.Id;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowTrigger>>> GetRegisteredTriggersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = _triggers.Values
            .GroupBy(r => r.WorkflowId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<WorkflowTrigger>)g.Select(r => r.Trigger).ToList());

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<WorkflowTrigger>>>(result);
    }

    /// <inheritdoc />
    public Task SetTriggerEnabledAsync(
        string workflowId,
        string triggerId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var key = $"{workflowId}:{triggerId}";

        if (_triggers.TryGetValue(key, out var registration))
        {
            registration.IsEnabled = enabled;

            _logger.LogInformation(
                "Trigger {Action}. WorkflowId: {WorkflowId}, TriggerId: {TriggerId}",
                enabled ? "enabled" : "disabled", workflowId, triggerId);
        }

        return Task.CompletedTask;
    }

    private static bool EvaluateFilter(string filter, object? data)
    {
        // Simple filter implementation - evaluate basic expressions
        // In production, this could use a more sophisticated expression evaluator
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        // For now, support simple property existence checks like "$.status"
        if (filter.StartsWith("$.", StringComparison.Ordinal) && data is not null)
        {
            // Basic property check
            return true; // Simplified - always pass for now
        }

        return true;
    }

    private static Dictionary<string, object?>? MapInput(
        IReadOnlyDictionary<string, string>? mapping,
        object? sourceData)
    {
        if (mapping is null || mapping.Count == 0)
        {
            // If no mapping, pass the entire source data as "data"
            if (sourceData is null)
            {
                return null;
            }
            return new Dictionary<string, object?> { ["data"] = sourceData };
        }

        var result = new Dictionary<string, object?>();

        foreach (var (key, path) in mapping)
        {
            // Simple path resolution - in production could use JSONPath
            if (path.StartsWith("$.", StringComparison.Ordinal) && sourceData is IDictionary<string, object?> dict)
            {
                var propertyName = path[2..]; // Remove "$."
                if (dict.TryGetValue(propertyName, out var value))
                {
                    result[key] = value;
                }
            }
            else
            {
                result[key] = sourceData;
            }
        }

        return result;
    }

    private static void ValidateInput(
        IReadOnlyDictionary<string, InputParameterDefinition> schema,
        IReadOnlyDictionary<string, object?>? input)
    {
        foreach (var (name, definition) in schema)
        {
            object? value = null;
            var hasValue = input?.TryGetValue(name, out value) == true;

            if (definition.Required && (!hasValue || value is null))
            {
                throw new ArgumentException($"Required input parameter '{name}' is missing");
            }

            if (hasValue && value is not null && definition.AllowedValues is not null)
            {
                if (!definition.AllowedValues.Contains(value))
                {
                    throw new ArgumentException(
                        $"Input parameter '{name}' has invalid value. Allowed values: {string.Join(", ", definition.AllowedValues)}");
                }
            }
        }
    }

    private sealed class TriggerRegistration
    {
        public required string WorkflowId { get; init; }
        public required string WorkflowVersion { get; init; }
        public required WorkflowTrigger Trigger { get; init; }
        public bool IsEnabled { get; set; }
    }
}
