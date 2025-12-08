using System.Globalization;
using OrbitMesh.Workflows.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CA1308 // Normalize strings to uppercase - YAML uses lowercase conventions

namespace OrbitMesh.Workflows.Parsing;

/// <summary>
/// Parses YAML workflow definitions into strongly-typed models.
/// </summary>
public sealed class WorkflowParser
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the WorkflowParser.
    /// </summary>
    public WorkflowParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses a YAML string into a WorkflowDefinition.
    /// </summary>
    /// <param name="yaml">The YAML content to parse.</param>
    /// <returns>The parsed workflow definition.</returns>
    /// <exception cref="WorkflowParseException">Thrown when parsing fails.</exception>
    public WorkflowDefinition Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        try
        {
            var yamlWorkflow = _deserializer.Deserialize<YamlWorkflow>(yaml);
            return ConvertToDefinition(yamlWorkflow);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new WorkflowParseException($"Invalid YAML syntax: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a YAML string into a WorkflowDefinition asynchronously from a stream.
    /// </summary>
    /// <param name="stream">The stream containing YAML content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed workflow definition.</returns>
    public async Task<WorkflowDefinition> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var yaml = await reader.ReadToEndAsync(cancellationToken);
        return Parse(yaml);
    }

    /// <summary>
    /// Parses a YAML file into a WorkflowDefinition.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed workflow definition.</returns>
    public async Task<WorkflowDefinition> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Parse(yaml);
    }

    private WorkflowDefinition ConvertToDefinition(YamlWorkflow yaml)
    {
        return new WorkflowDefinition
        {
            Id = yaml.Id ?? Guid.NewGuid().ToString("N"),
            Name = yaml.Name,
            Version = yaml.Version,
            Description = yaml.Description,
            Tags = yaml.Tags?.AsReadOnly(),
            IsEnabled = yaml.Enabled,
            Timeout = ParseTimeSpan(yaml.Timeout),
            MaxRetries = yaml.MaxRetries,
            Variables = yaml.Variables?.AsReadOnly(),
            Triggers = yaml.Triggers?.Select(ConvertTrigger).ToList().AsReadOnly(),
            Steps = yaml.Steps.Select(ConvertStep).ToList().AsReadOnly(),
            ErrorHandling = yaml.ErrorHandling != null ? ConvertErrorHandling(yaml.ErrorHandling) : null
        };
    }

    private WorkflowStep ConvertStep(YamlStep yaml)
    {
        var stepType = ParseStepType(yaml.Type);

        return new WorkflowStep
        {
            Id = yaml.Id,
            Name = yaml.Name,
            Type = stepType,
            Config = CreateStepConfig(stepType, yaml),
            DependsOn = yaml.DependsOn?.AsReadOnly(),
            Condition = yaml.Condition,
            Timeout = ParseTimeSpan(yaml.Timeout),
            MaxRetries = yaml.MaxRetries,
            RetryDelay = ParseTimeSpan(yaml.RetryDelay),
            ContinueOnError = yaml.ContinueOnError,
            OutputVariable = yaml.OutputVariable,
            Compensation = yaml.Compensation != null ? ConvertCompensation(yaml.Compensation) : null
        };
    }

    private StepConfig CreateStepConfig(StepType stepType, YamlStep yaml) => stepType switch
    {
        StepType.Job => new JobStepConfig
        {
            Command = yaml.Command ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'job' requires 'command'"),
            Pattern = yaml.Pattern ?? "*",
            Payload = yaml.Payload,
            Priority = yaml.Priority,
            RequiredTags = yaml.RequiredTags?.AsReadOnly()
        },
        StepType.Parallel => new ParallelStepConfig
        {
            Branches = yaml.Branches?.Select(ConvertStep).ToList().AsReadOnly()
                ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'parallel' requires 'branches'"),
            MaxConcurrency = yaml.MaxConcurrency,
            FailFast = yaml.FailFast
        },
        StepType.Conditional => new ConditionalStepConfig
        {
            Expression = yaml.Expression ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'conditional' requires 'expression'"),
            ThenBranch = yaml.Then?.Select(ConvertStep).ToList().AsReadOnly()
                ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'conditional' requires 'then'"),
            ElseBranch = yaml.Else?.Select(ConvertStep).ToList().AsReadOnly()
        },
        StepType.Delay => new DelayStepConfig
        {
            Duration = ParseTimeSpan(yaml.Duration) ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'delay' requires 'duration'")
        },
        StepType.WaitForEvent => new WaitForEventStepConfig
        {
            EventType = yaml.EventType ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'wait_for_event' requires 'event_type'"),
            CorrelationKey = yaml.CorrelationKey,
            Timeout = ParseTimeSpan(yaml.Timeout)
        },
        StepType.SubWorkflow => new SubWorkflowStepConfig
        {
            WorkflowId = yaml.WorkflowId ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'sub_workflow' requires 'workflow_id'"),
            Version = yaml.WorkflowVersion,
            Input = yaml.Input?.AsReadOnly(),
            WaitForCompletion = yaml.WaitForCompletion
        },
        StepType.ForEach => new ForEachStepConfig
        {
            Collection = yaml.Collection ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'foreach' requires 'collection'"),
            ItemVariable = yaml.ItemVariable,
            IndexVariable = yaml.IndexVariable,
            Steps = yaml.LoopSteps?.Select(ConvertStep).ToList().AsReadOnly()
                ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'foreach' requires 'loop_steps'"),
            MaxConcurrency = yaml.MaxConcurrency
        },
        StepType.Transform => new TransformStepConfig
        {
            Expression = yaml.Expression ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'transform' requires 'expression'"),
            Source = yaml.Source
        },
        StepType.Notify => new NotifyStepConfig
        {
            Channel = ParseNotifyChannel(yaml.Channel ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'notify' requires 'channel'")),
            Target = yaml.Target ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'notify' requires 'target'"),
            Message = yaml.Message ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'notify' requires 'message'"),
            Subject = yaml.Subject
        },
        StepType.Approval => new ApprovalStepConfig
        {
            Approvers = yaml.Approvers?.AsReadOnly()
                ?? throw new WorkflowParseException($"Step '{yaml.Id}' of type 'approval' requires 'approvers'"),
            RequiredApprovals = yaml.RequiredApprovals,
            Message = yaml.Message,
            Timeout = ParseTimeSpan(yaml.Timeout),
            TimeoutAction = ParseApprovalTimeoutAction(yaml.TimeoutAction)
        },
        _ => throw new WorkflowParseException($"Unknown step type: {stepType}")
    };

    private WorkflowTrigger ConvertTrigger(YamlTrigger yaml)
    {
        return yaml.Type.ToLowerInvariant() switch
        {
            "schedule" => new ScheduleTrigger
            {
                Id = yaml.Id,
                Name = yaml.Name,
                IsEnabled = yaml.Enabled,
                CronExpression = yaml.Cron,
                Interval = ParseTimeSpan(yaml.Interval),
                Timezone = yaml.Timezone,
                StartAt = ParseDateTimeOffset(yaml.StartAt),
                EndAt = ParseDateTimeOffset(yaml.EndAt),
                CatchUp = yaml.CatchUp,
                MaxConcurrentExecutions = yaml.MaxConcurrent
            },
            "event" => new EventTrigger
            {
                Id = yaml.Id,
                Name = yaml.Name,
                IsEnabled = yaml.Enabled,
                EventType = yaml.EventType ?? throw new WorkflowParseException($"Trigger '{yaml.Id}' of type 'event' requires 'event_type'"),
                Filter = yaml.Filter,
                InputMapping = yaml.InputMapping?.AsReadOnly()
            },
            "manual" => new ManualTrigger
            {
                Id = yaml.Id,
                Name = yaml.Name,
                IsEnabled = yaml.Enabled,
                InputSchema = yaml.InputSchema?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertInputParameter(kvp.Value)
                ).AsReadOnly(),
                RequiredRoles = yaml.RequiredRoles?.AsReadOnly()
            },
            "webhook" => new WebhookTrigger
            {
                Id = yaml.Id,
                Name = yaml.Name,
                IsEnabled = yaml.Enabled,
                Path = yaml.Path ?? throw new WorkflowParseException($"Trigger '{yaml.Id}' of type 'webhook' requires 'path'"),
                Methods = yaml.Methods?.AsReadOnly() ?? (IReadOnlyList<string>)["POST"],
                Secret = yaml.Secret,
                InputMapping = yaml.InputMapping?.AsReadOnly()
            },
            "job_completion" => new JobCompletionTrigger
            {
                Id = yaml.Id,
                Name = yaml.Name,
                IsEnabled = yaml.Enabled,
                CommandPattern = yaml.CommandPattern ?? throw new WorkflowParseException($"Trigger '{yaml.Id}' of type 'job_completion' requires 'command_pattern'"),
                Statuses = yaml.Statuses?.AsReadOnly() ?? (IReadOnlyList<string>)["Completed", "Failed"],
                Filter = yaml.Filter
            },
            _ => throw new WorkflowParseException($"Unknown trigger type: {yaml.Type}")
        };
    }

    private static InputParameterDefinition ConvertInputParameter(YamlInputParameter yaml)
    {
        return new InputParameterDefinition
        {
            Type = ParseInputParameterType(yaml.Type),
            Description = yaml.Description,
            Required = yaml.Required,
            DefaultValue = yaml.Default,
            AllowedValues = yaml.AllowedValues?.AsReadOnly()
        };
    }

    private static CompensationConfig ConvertCompensation(YamlCompensation yaml)
    {
        return new CompensationConfig
        {
            Config = new JobStepConfig
            {
                Command = yaml.Command ?? throw new WorkflowParseException("Compensation requires 'command'"),
                Pattern = yaml.Pattern ?? "*",
                Payload = yaml.Payload
            },
            Timeout = ParseTimeSpan(yaml.Timeout),
            MaxRetries = yaml.MaxRetries
        };
    }

    private static WorkflowErrorHandling ConvertErrorHandling(YamlErrorHandling yaml)
    {
        return new WorkflowErrorHandling
        {
            Strategy = yaml.Strategy.ToLowerInvariant() switch
            {
                "stop_on_first_error" or "stop" => ErrorStrategy.StopOnFirstError,
                "continue_and_aggregate" or "continue" => ErrorStrategy.ContinueAndAggregate,
                "compensate" or "saga" => ErrorStrategy.Compensate,
                _ => ErrorStrategy.StopOnFirstError
            },
            CompensationWorkflowId = yaml.CompensationWorkflowId,
            ContinueOnError = yaml.ContinueOnError
        };
    }

    private static StepType ParseStepType(string type) => type.ToLowerInvariant() switch
    {
        "job" => StepType.Job,
        "parallel" => StepType.Parallel,
        "conditional" or "if" => StepType.Conditional,
        "delay" or "wait" => StepType.Delay,
        "wait_for_event" or "event" => StepType.WaitForEvent,
        "sub_workflow" or "workflow" => StepType.SubWorkflow,
        "foreach" or "loop" => StepType.ForEach,
        "transform" or "map" => StepType.Transform,
        "notify" => StepType.Notify,
        "approval" or "approve" => StepType.Approval,
        _ => throw new WorkflowParseException($"Unknown step type: {type}")
    };

    private static NotifyChannel ParseNotifyChannel(string channel) => channel.ToLowerInvariant() switch
    {
        "webhook" => NotifyChannel.Webhook,
        "email" => NotifyChannel.Email,
        "slack" => NotifyChannel.Slack,
        "teams" => NotifyChannel.Teams,
        _ => throw new WorkflowParseException($"Unknown notification channel: {channel}")
    };

    private static ApprovalTimeoutAction ParseApprovalTimeoutAction(string? action) => action?.ToLowerInvariant() switch
    {
        "fail" or null => ApprovalTimeoutAction.Fail,
        "approve" or "auto_approve" => ApprovalTimeoutAction.Approve,
        "reject" or "auto_reject" => ApprovalTimeoutAction.Reject,
        _ => ApprovalTimeoutAction.Fail
    };

    private static InputParameterType ParseInputParameterType(string type) => type.ToLowerInvariant() switch
    {
        "string" or "text" => InputParameterType.StringValue,
        "int" or "integer" => InputParameterType.IntegerValue,
        "number" or "float" or "double" => InputParameterType.NumberValue,
        "bool" or "boolean" => InputParameterType.BooleanValue,
        "array" or "list" => InputParameterType.ArrayValue,
        "object" or "map" => InputParameterType.ObjectValue,
        "datetime" or "date" or "timestamp" => InputParameterType.DateTimeValue,
        _ => InputParameterType.StringValue
    };

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try standard TimeSpan parsing first
        if (TimeSpan.TryParse(value, out var result))
        {
            return result;
        }

        // Parse human-readable formats like "30s", "5m", "1h", "1d"
        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.EndsWith("ms", StringComparison.Ordinal) && int.TryParse(trimmed[..^2], out var ms))
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        if (trimmed.EndsWith('s') && int.TryParse(trimmed[..^1], out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (trimmed.EndsWith('m') && int.TryParse(trimmed[..^1], out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        if (trimmed.EndsWith('h') && int.TryParse(trimmed[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        if (trimmed.EndsWith('d') && int.TryParse(trimmed[..^1], out var days))
        {
            return TimeSpan.FromDays(days);
        }

        throw new WorkflowParseException($"Invalid duration format: {value}. Use formats like '30s', '5m', '1h', '1d', or '00:05:00'");
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var result))
        {
            return result;
        }

        throw new WorkflowParseException($"Invalid datetime format: {value}");
    }
}

/// <summary>
/// Exception thrown when workflow parsing fails.
/// </summary>
public sealed class WorkflowParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of WorkflowParseException.
    /// </summary>
    public WorkflowParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of WorkflowParseException.
    /// </summary>
    public WorkflowParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of WorkflowParseException with an inner exception.
    /// </summary>
    public WorkflowParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
