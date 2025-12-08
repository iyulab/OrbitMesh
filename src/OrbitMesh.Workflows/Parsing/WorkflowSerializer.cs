using OrbitMesh.Workflows.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CA1308 // Normalize strings to uppercase - YAML uses lowercase conventions

namespace OrbitMesh.Workflows.Parsing;

/// <summary>
/// Serializes workflow definitions to YAML format.
/// </summary>
public sealed class WorkflowSerializer
{
    private readonly ISerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the WorkflowSerializer.
    /// </summary>
    public WorkflowSerializer()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
            .DisableAliases()
            .Build();
    }

    /// <summary>
    /// Serializes a WorkflowDefinition to YAML.
    /// </summary>
    /// <param name="workflow">The workflow definition to serialize.</param>
    /// <returns>The YAML representation of the workflow.</returns>
    public string Serialize(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var yamlModel = ConvertToYaml(workflow);
        return _serializer.Serialize(yamlModel);
    }

    /// <summary>
    /// Serializes a WorkflowDefinition to a file.
    /// </summary>
    /// <param name="workflow">The workflow definition to serialize.</param>
    /// <param name="filePath">Path to write the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SerializeToFileAsync(WorkflowDefinition workflow, string filePath, CancellationToken cancellationToken = default)
    {
        var yaml = Serialize(workflow);
        await File.WriteAllTextAsync(filePath, yaml, cancellationToken);
    }

    private static YamlWorkflow ConvertToYaml(WorkflowDefinition workflow)
    {
        return new YamlWorkflow
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Version = workflow.Version,
            Description = workflow.Description,
            Tags = workflow.Tags?.ToList(),
            Enabled = workflow.IsEnabled,
            Timeout = FormatTimeSpan(workflow.Timeout),
            MaxRetries = workflow.MaxRetries,
            Variables = workflow.Variables?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Triggers = workflow.Triggers?.Select(ConvertTrigger).ToList(),
            Steps = workflow.Steps.Select(ConvertStep).ToList(),
            ErrorHandling = workflow.ErrorHandling != null ? ConvertErrorHandling(workflow.ErrorHandling) : null
        };
    }

    private static YamlStep ConvertStep(WorkflowStep step)
    {
        var yamlStep = new YamlStep
        {
            Id = step.Id,
            Name = step.Name,
            Type = step.Type.ToString().ToLowerInvariant(),
            DependsOn = step.DependsOn?.ToList(),
            Condition = step.Condition,
            Timeout = FormatTimeSpan(step.Timeout),
            MaxRetries = step.MaxRetries,
            RetryDelay = FormatTimeSpan(step.RetryDelay),
            ContinueOnError = step.ContinueOnError,
            OutputVariable = step.OutputVariable
        };

        ApplyStepConfig(yamlStep, step.Config);

        if (step.Compensation != null)
        {
            yamlStep.Compensation = ConvertCompensation(step.Compensation);
        }

        return yamlStep;
    }

    private static void ApplyStepConfig(YamlStep yaml, StepConfig config)
    {
        switch (config)
        {
            case JobStepConfig job:
                yaml.Command = job.Command;
                yaml.Pattern = job.Pattern;
                yaml.Payload = job.Payload;
                yaml.Priority = job.Priority;
                yaml.RequiredTags = job.RequiredTags?.ToList();
                break;

            case ParallelStepConfig parallel:
                yaml.Branches = parallel.Branches.Select(ConvertStep).ToList();
                yaml.MaxConcurrency = parallel.MaxConcurrency;
                yaml.FailFast = parallel.FailFast;
                break;

            case ConditionalStepConfig conditional:
                yaml.Expression = conditional.Expression;
                yaml.Then = conditional.ThenBranch.Select(ConvertStep).ToList();
                yaml.Else = conditional.ElseBranch?.Select(ConvertStep).ToList();
                break;

            case DelayStepConfig delay:
                yaml.Duration = FormatTimeSpan(delay.Duration);
                break;

            case WaitForEventStepConfig waitEvent:
                yaml.EventType = waitEvent.EventType;
                yaml.CorrelationKey = waitEvent.CorrelationKey;
                yaml.Timeout = FormatTimeSpan(waitEvent.Timeout);
                break;

            case SubWorkflowStepConfig subWorkflow:
                yaml.WorkflowId = subWorkflow.WorkflowId;
                yaml.WorkflowVersion = subWorkflow.Version;
                yaml.Input = subWorkflow.Input?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                yaml.WaitForCompletion = subWorkflow.WaitForCompletion;
                break;

            case ForEachStepConfig forEach:
                yaml.Collection = forEach.Collection;
                yaml.ItemVariable = forEach.ItemVariable;
                yaml.IndexVariable = forEach.IndexVariable;
                yaml.LoopSteps = forEach.Steps.Select(ConvertStep).ToList();
                yaml.MaxConcurrency = forEach.MaxConcurrency;
                break;

            case TransformStepConfig transform:
                yaml.Expression = transform.Expression;
                yaml.Source = transform.Source;
                break;

            case NotifyStepConfig notify:
                yaml.Channel = notify.Channel.ToString().ToLowerInvariant();
                yaml.Target = notify.Target;
                yaml.Message = notify.Message;
                yaml.Subject = notify.Subject;
                break;

            case ApprovalStepConfig approval:
                yaml.Approvers = approval.Approvers.ToList();
                yaml.RequiredApprovals = approval.RequiredApprovals;
                yaml.Message = approval.Message;
                yaml.Timeout = FormatTimeSpan(approval.Timeout);
                yaml.TimeoutAction = approval.TimeoutAction.ToString().ToLowerInvariant();
                break;
        }
    }

    private static YamlTrigger ConvertTrigger(WorkflowTrigger trigger)
    {
        var triggerType = trigger switch
        {
            ScheduleTrigger => "schedule",
            EventTrigger => "event",
            ManualTrigger => "manual",
            WebhookTrigger => "webhook",
            JobCompletionTrigger => "job_completion",
            _ => "unknown"
        };

        var yaml = new YamlTrigger
        {
            Id = trigger.Id,
            Type = triggerType,
            Name = trigger.Name,
            Enabled = trigger.IsEnabled
        };

        switch (trigger)
        {
            case ScheduleTrigger schedule:
                yaml.Cron = schedule.CronExpression;
                yaml.Interval = FormatTimeSpan(schedule.Interval);
                yaml.Timezone = schedule.Timezone;
                yaml.StartAt = schedule.StartAt?.ToString("o");
                yaml.EndAt = schedule.EndAt?.ToString("o");
                yaml.CatchUp = schedule.CatchUp;
                yaml.MaxConcurrent = schedule.MaxConcurrentExecutions;
                break;

            case EventTrigger eventTrigger:
                yaml.EventType = eventTrigger.EventType;
                yaml.Filter = eventTrigger.Filter;
                yaml.InputMapping = eventTrigger.InputMapping?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                break;

            case ManualTrigger manual:
                yaml.InputSchema = manual.InputSchema?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertInputParameter(kvp.Value)
                );
                yaml.RequiredRoles = manual.RequiredRoles?.ToList();
                break;

            case WebhookTrigger webhook:
                yaml.Path = webhook.Path;
                yaml.Methods = webhook.Methods.ToList();
                yaml.Secret = webhook.Secret;
                yaml.InputMapping = webhook.InputMapping?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                break;

            case JobCompletionTrigger jobCompletion:
                yaml.CommandPattern = jobCompletion.CommandPattern;
                yaml.Statuses = jobCompletion.Statuses.ToList();
                yaml.Filter = jobCompletion.Filter;
                break;
        }

        return yaml;
    }

    private static YamlInputParameter ConvertInputParameter(InputParameterDefinition param)
    {
        return new YamlInputParameter
        {
            Type = param.Type switch
            {
                InputParameterType.StringValue => "string",
                InputParameterType.IntegerValue => "integer",
                InputParameterType.NumberValue => "number",
                InputParameterType.BooleanValue => "boolean",
                InputParameterType.ArrayValue => "array",
                InputParameterType.ObjectValue => "object",
                InputParameterType.DateTimeValue => "datetime",
                _ => "string"
            },
            Description = param.Description,
            Required = param.Required,
            Default = param.DefaultValue,
            AllowedValues = param.AllowedValues?.ToList()
        };
    }

    private static YamlCompensation ConvertCompensation(CompensationConfig compensation)
    {
        var jobConfig = compensation.Config as JobStepConfig
            ?? throw new InvalidOperationException("Compensation config must be a JobStepConfig");

        return new YamlCompensation
        {
            Command = jobConfig.Command,
            Pattern = jobConfig.Pattern,
            Payload = jobConfig.Payload,
            Timeout = FormatTimeSpan(compensation.Timeout),
            MaxRetries = compensation.MaxRetries
        };
    }

    private static YamlErrorHandling ConvertErrorHandling(WorkflowErrorHandling errorHandling)
    {
        return new YamlErrorHandling
        {
            Strategy = errorHandling.Strategy switch
            {
                ErrorStrategy.StopOnFirstError => "stop_on_first_error",
                ErrorStrategy.ContinueAndAggregate => "continue_and_aggregate",
                ErrorStrategy.Compensate => "compensate",
                _ => "stop_on_first_error"
            },
            CompensationWorkflowId = errorHandling.CompensationWorkflowId,
            ContinueOnError = errorHandling.ContinueOnError
        };
    }

    private static string? FormatTimeSpan(TimeSpan? timeSpan)
    {
        if (timeSpan == null)
        {
            return null;
        }

        var ts = timeSpan.Value;

        // Format as human-readable
        if (ts.TotalDays >= 1 && ts.TotalDays == Math.Floor(ts.TotalDays))
        {
            return $"{(int)ts.TotalDays}d";
        }

        if (ts.TotalHours >= 1 && ts.TotalHours == Math.Floor(ts.TotalHours))
        {
            return $"{(int)ts.TotalHours}h";
        }

        if (ts.TotalMinutes >= 1 && ts.TotalMinutes == Math.Floor(ts.TotalMinutes))
        {
            return $"{(int)ts.TotalMinutes}m";
        }

        if (ts.TotalSeconds >= 1 && ts.TotalSeconds == Math.Floor(ts.TotalSeconds))
        {
            return $"{(int)ts.TotalSeconds}s";
        }

        return ts.ToString();
    }
}
