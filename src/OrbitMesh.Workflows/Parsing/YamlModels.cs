using YamlDotNet.Serialization;

#pragma warning disable CA1002 // Do not expose generic lists - YAML serialization requires mutable lists
#pragma warning disable CA2227 // Collection properties should be read only - YAML serialization requires setters

namespace OrbitMesh.Workflows.Parsing;

/// <summary>
/// YAML model for workflow definition.
/// </summary>
public sealed class YamlWorkflow
{
    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    [YamlMember(Alias = "name")]
    public required string Name { get; set; }

    [YamlMember(Alias = "version")]
    public required string Version { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; set; }

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "timeout")]
    public string? Timeout { get; set; }

    [YamlMember(Alias = "max_retries")]
    public int MaxRetries { get; set; } = 0;

    [YamlMember(Alias = "variables")]
    public Dictionary<string, object?>? Variables { get; set; }

    [YamlMember(Alias = "triggers")]
    public List<YamlTrigger>? Triggers { get; set; }

    [YamlMember(Alias = "steps")]
    public required List<YamlStep> Steps { get; set; }

    [YamlMember(Alias = "error_handling")]
    public YamlErrorHandling? ErrorHandling { get; set; }
}

/// <summary>
/// YAML model for workflow step.
/// </summary>
public sealed class YamlStep
{
    [YamlMember(Alias = "id")]
    public required string Id { get; set; }

    [YamlMember(Alias = "name")]
    public required string Name { get; set; }

    [YamlMember(Alias = "type")]
    public required string Type { get; set; }

    [YamlMember(Alias = "depends_on")]
    public List<string>? DependsOn { get; set; }

    [YamlMember(Alias = "condition")]
    public string? Condition { get; set; }

    [YamlMember(Alias = "timeout")]
    public string? Timeout { get; set; }

    [YamlMember(Alias = "max_retries")]
    public int MaxRetries { get; set; } = 0;

    [YamlMember(Alias = "retry_delay")]
    public string? RetryDelay { get; set; }

    [YamlMember(Alias = "continue_on_error")]
    public bool ContinueOnError { get; set; } = false;

    [YamlMember(Alias = "output_variable")]
    public string? OutputVariable { get; set; }

    // Job step properties
    [YamlMember(Alias = "command")]
    public string? Command { get; set; }

    [YamlMember(Alias = "pattern")]
    public string? Pattern { get; set; }

    [YamlMember(Alias = "payload")]
    public object? Payload { get; set; }

    [YamlMember(Alias = "priority")]
    public int Priority { get; set; } = 0;

    [YamlMember(Alias = "required_tags")]
    public List<string>? RequiredTags { get; set; }

    // Parallel step properties
    [YamlMember(Alias = "branches")]
    public List<YamlStep>? Branches { get; set; }

    [YamlMember(Alias = "max_concurrency")]
    public int? MaxConcurrency { get; set; }

    [YamlMember(Alias = "fail_fast")]
    public bool FailFast { get; set; } = true;

    // Conditional step properties
    [YamlMember(Alias = "expression")]
    public string? Expression { get; set; }

    [YamlMember(Alias = "then")]
    public List<YamlStep>? Then { get; set; }

    [YamlMember(Alias = "else")]
    public List<YamlStep>? Else { get; set; }

    // Delay step properties
    [YamlMember(Alias = "duration")]
    public string? Duration { get; set; }

    // WaitForEvent step properties
    [YamlMember(Alias = "event_type")]
    public string? EventType { get; set; }

    [YamlMember(Alias = "correlation_key")]
    public string? CorrelationKey { get; set; }

    // SubWorkflow step properties
    [YamlMember(Alias = "workflow_id")]
    public string? WorkflowId { get; set; }

    [YamlMember(Alias = "workflow_version")]
    public string? WorkflowVersion { get; set; }

    [YamlMember(Alias = "input")]
    public Dictionary<string, object?>? Input { get; set; }

    [YamlMember(Alias = "wait_for_completion")]
    public bool WaitForCompletion { get; set; } = true;

    // ForEach step properties
    [YamlMember(Alias = "collection")]
    public string? Collection { get; set; }

    [YamlMember(Alias = "item_variable")]
    public string ItemVariable { get; set; } = "item";

    [YamlMember(Alias = "index_variable")]
    public string IndexVariable { get; set; } = "index";

    [YamlMember(Alias = "loop_steps")]
    public List<YamlStep>? LoopSteps { get; set; }

    // Transform step properties
    [YamlMember(Alias = "source")]
    public string? Source { get; set; }

    // Notify step properties
    [YamlMember(Alias = "channel")]
    public string? Channel { get; set; }

    [YamlMember(Alias = "target")]
    public string? Target { get; set; }

    [YamlMember(Alias = "message")]
    public string? Message { get; set; }

    [YamlMember(Alias = "subject")]
    public string? Subject { get; set; }

    // Approval step properties
    [YamlMember(Alias = "approvers")]
    public List<string>? Approvers { get; set; }

    [YamlMember(Alias = "required_approvals")]
    public int RequiredApprovals { get; set; } = 1;

    [YamlMember(Alias = "timeout_action")]
    public string? TimeoutAction { get; set; }

    // Compensation
    [YamlMember(Alias = "compensation")]
    public YamlCompensation? Compensation { get; set; }
}

/// <summary>
/// YAML model for trigger definition.
/// </summary>
public sealed class YamlTrigger
{
    [YamlMember(Alias = "id")]
    public required string Id { get; set; }

    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "type")]
    public required string Type { get; set; }

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;

    // Schedule trigger properties
    [YamlMember(Alias = "cron")]
    public string? Cron { get; set; }

    [YamlMember(Alias = "interval")]
    public string? Interval { get; set; }

    [YamlMember(Alias = "timezone")]
    public string Timezone { get; set; } = "UTC";

    [YamlMember(Alias = "start_at")]
    public string? StartAt { get; set; }

    [YamlMember(Alias = "end_at")]
    public string? EndAt { get; set; }

    [YamlMember(Alias = "catch_up")]
    public bool CatchUp { get; set; } = false;

    [YamlMember(Alias = "max_concurrent")]
    public int MaxConcurrent { get; set; } = 1;

    // Event trigger properties
    [YamlMember(Alias = "event_type")]
    public string? EventType { get; set; }

    [YamlMember(Alias = "filter")]
    public string? Filter { get; set; }

    [YamlMember(Alias = "input_mapping")]
    public Dictionary<string, string>? InputMapping { get; set; }

    // Manual trigger properties
    [YamlMember(Alias = "input_schema")]
    public Dictionary<string, YamlInputParameter>? InputSchema { get; set; }

    [YamlMember(Alias = "required_roles")]
    public List<string>? RequiredRoles { get; set; }

    // Webhook trigger properties
    [YamlMember(Alias = "path")]
    public string? Path { get; set; }

    [YamlMember(Alias = "methods")]
    public List<string>? Methods { get; set; }

    [YamlMember(Alias = "secret")]
    public string? Secret { get; set; }

    // Job completion trigger properties
    [YamlMember(Alias = "command_pattern")]
    public string? CommandPattern { get; set; }

    [YamlMember(Alias = "statuses")]
    public List<string>? Statuses { get; set; }

    // File watch trigger properties
    [YamlMember(Alias = "agent_pattern")]
    public string? AgentPattern { get; set; }

    [YamlMember(Alias = "watch_path")]
    public string? WatchPath { get; set; }

    [YamlMember(Alias = "watch_filter")]
    public string? WatchFilter { get; set; }

    [YamlMember(Alias = "include_subdirectories")]
    public bool IncludeSubdirectories { get; set; } = true;

    [YamlMember(Alias = "change_types")]
    public List<string>? ChangeTypes { get; set; }

    [YamlMember(Alias = "debounce_ms")]
    public int DebounceMs { get; set; } = 1000;
}

/// <summary>
/// YAML model for input parameter definition.
/// </summary>
public sealed class YamlInputParameter
{
    [YamlMember(Alias = "type")]
    public required string Type { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "required")]
    public bool Required { get; set; } = false;

    [YamlMember(Alias = "default")]
    public object? Default { get; set; }

    [YamlMember(Alias = "allowed_values")]
    public List<object>? AllowedValues { get; set; }
}

/// <summary>
/// YAML model for error handling configuration.
/// </summary>
public sealed class YamlErrorHandling
{
    [YamlMember(Alias = "strategy")]
    public string Strategy { get; set; } = "stop_on_first_error";

    [YamlMember(Alias = "compensation_workflow_id")]
    public string? CompensationWorkflowId { get; set; }

    [YamlMember(Alias = "continue_on_error")]
    public bool ContinueOnError { get; set; } = false;
}

/// <summary>
/// YAML model for compensation configuration.
/// </summary>
public sealed class YamlCompensation
{
    [YamlMember(Alias = "command")]
    public string? Command { get; set; }

    [YamlMember(Alias = "pattern")]
    public string? Pattern { get; set; }

    [YamlMember(Alias = "payload")]
    public object? Payload { get; set; }

    [YamlMember(Alias = "timeout")]
    public string? Timeout { get; set; }

    [YamlMember(Alias = "max_retries")]
    public int MaxRetries { get; set; } = 3;
}
