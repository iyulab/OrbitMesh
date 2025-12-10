using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.BuiltIn;

/// <summary>
/// Provides built-in workflow templates for common operations.
/// </summary>
public static class WorkflowTemplates
{
    /// <summary>
    /// Creates a health check workflow that monitors agents.
    /// </summary>
    /// <param name="agentPattern">Agent pattern to check (default: *).</param>
    /// <param name="interval">Check interval (default: 5 minutes).</param>
    /// <returns>A workflow definition for health checking.</returns>
    public static WorkflowDefinition HealthCheck(string agentPattern = "*", TimeSpan? interval = null)
    {
        interval ??= TimeSpan.FromMinutes(5);

        return new WorkflowDefinition
        {
            Id = "orbit:workflow:health-check",
            Name = "Agent Health Check",
            Version = "1.0.0",
            Description = "Monitors agent health status and reports unhealthy agents",
            Tags = ["built-in", "monitoring", "health"],
            Triggers =
            [
                new ScheduleTrigger
                {
                    Id = "health-schedule",
                    Name = "Health Check Schedule",
                    Interval = interval,
                    MaxConcurrentExecutions = 1
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "check-health",
                    Name = "Check Agent Health",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "orbit:system.health",
                        Pattern = agentPattern
                    },
                    OutputVariable = "healthResults"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a file deployment workflow.
    /// </summary>
    /// <param name="sourceUrl">Source URL for file download.</param>
    /// <param name="destinationPath">Destination path on agents.</param>
    /// <param name="agentPattern">Agent pattern (default: *).</param>
    /// <param name="checksum">Optional SHA256 checksum for verification.</param>
    /// <returns>A workflow definition for file deployment.</returns>
    public static WorkflowDefinition FileDeployment(
        string sourceUrl,
        string destinationPath,
        string agentPattern = "*",
        string? checksum = null)
    {
        return new WorkflowDefinition
        {
            Id = $"orbit:workflow:file-deploy-{Guid.NewGuid():N}",
            Name = "File Deployment",
            Version = "1.0.0",
            Description = $"Deploy file from {sourceUrl} to {destinationPath}",
            Tags = ["built-in", "deployment", "file"],
            Variables = new Dictionary<string, object?>
            {
                ["sourceUrl"] = sourceUrl,
                ["destinationPath"] = destinationPath,
                ["checksum"] = checksum
            },
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-deploy",
                    Name = "Manual Deployment Trigger"
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "download-file",
                    Name = "Download File to Agents",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "orbit:file.download",
                        Pattern = agentPattern,
                        Payload = new Dictionary<string, object?>
                        {
                            ["sourcePath"] = sourceUrl,
                            ["destinationPath"] = destinationPath,
                            ["checksum"] = checksum,
                            ["overwrite"] = true,
                            ["createDirectories"] = true
                        }
                    },
                    MaxRetries = 3,
                    RetryDelay = TimeSpan.FromSeconds(5),
                    OutputVariable = "downloadResults"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a service restart workflow with health verification.
    /// </summary>
    /// <param name="serviceName">Name of the service to restart.</param>
    /// <param name="agentPattern">Agent pattern (default: *).</param>
    /// <param name="timeout">Service operation timeout.</param>
    /// <returns>A workflow definition for service restart.</returns>
    public static WorkflowDefinition ServiceRestart(
        string serviceName,
        string agentPattern = "*",
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        return new WorkflowDefinition
        {
            Id = $"orbit:workflow:service-restart-{serviceName}",
            Name = $"Restart {serviceName}",
            Version = "1.0.0",
            Description = $"Restart {serviceName} service and verify health",
            Tags = ["built-in", "service", "restart"],
            Variables = new Dictionary<string, object?>
            {
                ["serviceName"] = serviceName,
                ["timeout"] = timeout.Value.TotalSeconds
            },
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-restart",
                    Name = "Manual Restart Trigger"
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "restart-service",
                    Name = $"Restart {serviceName}",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "orbit:service.restart",
                        Pattern = agentPattern,
                        Payload = new Dictionary<string, object?>
                        {
                            ["serviceName"] = serviceName,
                            ["timeoutSeconds"] = timeout.Value.TotalSeconds
                        }
                    },
                    Timeout = timeout.Value.Add(TimeSpan.FromSeconds(30)),
                    OutputVariable = "restartResult"
                },
                new WorkflowStep
                {
                    Id = "verify-status",
                    Name = "Verify Service Status",
                    Type = StepType.Job,
                    DependsOn = ["restart-service"],
                    Config = new JobStepConfig
                    {
                        Command = "orbit:service.status",
                        Pattern = agentPattern,
                        Payload = new Dictionary<string, object?>
                        {
                            ["serviceName"] = serviceName
                        }
                    },
                    OutputVariable = "statusResult"
                }
            ]
        };
    }

    /// <summary>
    /// Creates an update workflow that downloads, applies, and verifies updates.
    /// </summary>
    /// <param name="agentPattern">Agent pattern (default: *).</param>
    /// <param name="requireApproval">Whether to require approval before applying.</param>
    /// <returns>A workflow definition for agent updates.</returns>
    public static WorkflowDefinition AgentUpdate(
        string agentPattern = "*",
        bool requireApproval = true)
    {
        var steps = new List<WorkflowStep>
        {
            new()
            {
                Id = "check-update",
                Name = "Check for Updates",
                Type = StepType.Job,
                Config = new JobStepConfig
                {
                    Command = "orbit:update.check",
                    Pattern = agentPattern
                },
                OutputVariable = "updateInfo"
            },
            new()
            {
                Id = "download-update",
                Name = "Download Update Package",
                Type = StepType.Job,
                DependsOn = ["check-update"],
                Condition = "${updateInfo.updateAvailable}",
                Config = new JobStepConfig
                {
                    Command = "orbit:update.download",
                    Pattern = agentPattern,
                    Payload = new Dictionary<string, object?>
                    {
                        ["package"] = "${updateInfo.package}"
                    }
                },
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(10),
                OutputVariable = "downloadResult"
            }
        };

        if (requireApproval)
        {
            steps.Add(new WorkflowStep
            {
                Id = "approve-update",
                Name = "Approve Update Application",
                Type = StepType.Approval,
                DependsOn = ["download-update"],
                Condition = "${downloadResult.success}",
                Config = new ApprovalStepConfig
                {
                    Approvers = ["admin", "ops-team"],
                    RequiredApprovals = 1,
                    Message = "Update downloaded successfully. Approve to apply update.",
                    Timeout = TimeSpan.FromHours(24),
                    TimeoutAction = ApprovalTimeoutAction.Reject
                }
            });
        }

        steps.Add(new WorkflowStep
        {
            Id = "apply-update",
            Name = "Apply Update",
            Type = StepType.Job,
            DependsOn = requireApproval ? ["approve-update"] : ["download-update"],
            Condition = "${downloadResult.success}",
            Config = new JobStepConfig
            {
                Command = "orbit:update.apply",
                Pattern = agentPattern,
                Payload = new Dictionary<string, object?>
                {
                    ["packagePath"] = "${downloadResult.localPath}",
                    ["targetVersion"] = "${updateInfo.package.version}",
                    ["createBackup"] = true,
                    ["restartAfterApply"] = true
                }
            },
            OutputVariable = "applyResult"
        });

        steps.Add(new WorkflowStep
        {
            Id = "verify-update",
            Name = "Verify Update Status",
            Type = StepType.Job,
            DependsOn = ["apply-update"],
            Config = new JobStepConfig
            {
                Command = "orbit:update.status",
                Pattern = agentPattern
            },
            OutputVariable = "updateStatus"
        });

        return new WorkflowDefinition
        {
            Id = "orbit:workflow:agent-update",
            Name = "Agent Update",
            Version = "1.0.0",
            Description = "Check, download, approve, and apply agent updates",
            Tags = ["built-in", "update", "maintenance"],
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-update",
                    Name = "Manual Update Trigger"
                },
                new ScheduleTrigger
                {
                    Id = "scheduled-update",
                    Name = "Scheduled Update Check",
                    CronExpression = "0 0 * * 0", // Weekly on Sunday at midnight
                    MaxConcurrentExecutions = 1
                }
            ],
            Steps = steps.AsReadOnly(),
            ErrorHandling = new WorkflowErrorHandling
            {
                Strategy = ErrorStrategy.StopOnFirstError
            }
        };
    }

    /// <summary>
    /// Creates a rolling deployment workflow.
    /// </summary>
    /// <param name="sourceUrl">Source URL for deployment package.</param>
    /// <param name="destinationPath">Destination path on agents.</param>
    /// <param name="serviceName">Service to restart after deployment.</param>
    /// <param name="maxConcurrent">Maximum concurrent deployments.</param>
    /// <returns>A workflow definition for rolling deployment.</returns>
    public static WorkflowDefinition RollingDeployment(
        string sourceUrl,
        string destinationPath,
        string serviceName,
        int maxConcurrent = 2)
    {
        return new WorkflowDefinition
        {
            Id = "orbit:workflow:rolling-deploy",
            Name = "Rolling Deployment",
            Version = "1.0.0",
            Description = "Deploy files and restart services with rolling strategy",
            Tags = ["built-in", "deployment", "rolling"],
            Variables = new Dictionary<string, object?>
            {
                ["sourceUrl"] = sourceUrl,
                ["destinationPath"] = destinationPath,
                ["serviceName"] = serviceName,
                ["maxConcurrent"] = maxConcurrent
            },
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-deploy",
                    Name = "Manual Deployment Trigger",
                    InputSchema = new Dictionary<string, InputParameterDefinition>
                    {
                        ["version"] = new() { Type = InputParameterType.StringValue, Required = true, Description = "Version to deploy" },
                        ["agents"] = new() { Type = InputParameterType.ArrayValue, Required = false, Description = "Specific agents to deploy to" }
                    }
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "download-package",
                    Name = "Download Deployment Package",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "orbit:file.download",
                        Pattern = "*",
                        Payload = new Dictionary<string, object?>
                        {
                            ["sourcePath"] = sourceUrl,
                            ["destinationPath"] = destinationPath,
                            ["overwrite"] = true,
                            ["createDirectories"] = true
                        }
                    },
                    MaxRetries = 3,
                    OutputVariable = "downloadResult"
                },
                new WorkflowStep
                {
                    Id = "stop-service",
                    Name = "Stop Service",
                    Type = StepType.Job,
                    DependsOn = ["download-package"],
                    Config = new JobStepConfig
                    {
                        Command = "orbit:service.stop",
                        Pattern = "*",
                        Payload = new Dictionary<string, object?>
                        {
                            ["serviceName"] = serviceName,
                            ["timeoutSeconds"] = 60
                        }
                    },
                    OutputVariable = "stopResult"
                },
                new WorkflowStep
                {
                    Id = "start-service",
                    Name = "Start Service",
                    Type = StepType.Job,
                    DependsOn = ["stop-service"],
                    Config = new JobStepConfig
                    {
                        Command = "orbit:service.start",
                        Pattern = "*",
                        Payload = new Dictionary<string, object?>
                        {
                            ["serviceName"] = serviceName,
                            ["timeoutSeconds"] = 60
                        }
                    },
                    OutputVariable = "startResult"
                },
                new WorkflowStep
                {
                    Id = "health-check",
                    Name = "Verify Health After Deployment",
                    Type = StepType.Job,
                    DependsOn = ["start-service"],
                    Config = new JobStepConfig
                    {
                        Command = "orbit:system.health",
                        Pattern = "*"
                    },
                    OutputVariable = "healthResult"
                }
            ],
            ErrorHandling = new WorkflowErrorHandling
            {
                Strategy = ErrorStrategy.StopOnFirstError
            }
        };
    }

    /// <summary>
    /// Creates a file sync workflow.
    /// </summary>
    /// <param name="sourceUrl">Source URL for sync.</param>
    /// <param name="destinationPath">Destination path on agents.</param>
    /// <param name="agentPattern">Agent pattern.</param>
    /// <param name="deleteOrphans">Whether to delete orphaned files.</param>
    /// <returns>A workflow definition for file synchronization.</returns>
    public static WorkflowDefinition FileSync(
        string sourceUrl,
        string destinationPath,
        string agentPattern = "*",
        bool deleteOrphans = false)
    {
        return new WorkflowDefinition
        {
            Id = "orbit:workflow:file-sync",
            Name = "File Synchronization",
            Version = "1.0.0",
            Description = $"Synchronize files from {sourceUrl} to {destinationPath}",
            Tags = ["built-in", "sync", "file"],
            Variables = new Dictionary<string, object?>
            {
                ["sourceUrl"] = sourceUrl,
                ["destinationPath"] = destinationPath,
                ["deleteOrphans"] = deleteOrphans
            },
            Triggers =
            [
                new ManualTrigger
                {
                    Id = "manual-sync",
                    Name = "Manual Sync Trigger"
                },
                new ScheduleTrigger
                {
                    Id = "scheduled-sync",
                    Name = "Scheduled Sync",
                    Interval = TimeSpan.FromHours(1),
                    MaxConcurrentExecutions = 1
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "sync-files",
                    Name = "Synchronize Files",
                    Type = StepType.Job,
                    Config = new JobStepConfig
                    {
                        Command = "orbit:file.sync",
                        Pattern = agentPattern,
                        Payload = new Dictionary<string, object?>
                        {
                            ["source"] = sourceUrl,
                            ["destination"] = destinationPath,
                            ["deleteOrphans"] = deleteOrphans
                        }
                    },
                    MaxRetries = 3,
                    RetryDelay = TimeSpan.FromSeconds(10),
                    OutputVariable = "syncResult"
                }
            ]
        };
    }

    /// <summary>
    /// Creates a bidirectional file sync workflow with file watch triggers.
    /// </summary>
    /// <param name="serverPath">Server-side directory path.</param>
    /// <param name="agentPath">Agent-side directory path.</param>
    /// <param name="agentPattern">Agent pattern for targeting.</param>
    /// <param name="mode">Sync mode: "twoway", "server-to-agents", or "agent-to-server".</param>
    /// <param name="filter">File filter pattern (default: "*.*").</param>
    /// <returns>A workflow definition for bidirectional file sync.</returns>
    public static WorkflowDefinition TwoWayFileSync(
        string serverPath,
        string agentPath,
        string agentPattern = "*",
        string mode = "twoway",
        string filter = "*.*")
    {
        var triggers = new List<WorkflowTrigger>
        {
            new ManualTrigger
            {
                Id = "manual-sync",
                Name = "Manual Sync Trigger"
            }
        };

        // Add file watch triggers based on mode
        if (mode is "twoway" or "server-to-agents")
        {
            // Server changes trigger sync to agents
            triggers.Add(new EventTrigger
            {
                Id = "server-change",
                Name = "Server File Change",
                EventType = "server:file:changed",
                Filter = $"path.startsWith('{serverPath}')",
                InputMapping = new Dictionary<string, string>
                {
                    ["changedPath"] = "$.path",
                    ["changeType"] = "$.type",
                    ["direction"] = "server-to-agents"
                }
            });
        }

        if (mode is "twoway" or "agent-to-server")
        {
            // Agent changes trigger sync to server
            triggers.Add(new FileWatchTrigger
            {
                Id = "agent-change",
                Name = "Agent File Change",
                AgentPattern = agentPattern,
                WatchPath = agentPath,
                Filter = filter,
                IncludeSubdirectories = true,
                ChangeTypes = ["Created", "Modified", "Deleted", "Renamed"],
                DebounceMs = 1000,
                InputMapping = new Dictionary<string, string>
                {
                    ["changedPath"] = "$.fullPath",
                    ["changeType"] = "$.changeType",
                    ["agentId"] = "$.agentId",
                    ["direction"] = "agent-to-server"
                }
            });
        }

        var steps = new List<WorkflowStep>();

        // Add steps based on mode
        if (mode is "twoway" or "server-to-agents")
        {
            steps.Add(new WorkflowStep
            {
                Id = "sync-to-agents",
                Name = "Sync Server to Agents",
                Type = StepType.Job,
                Condition = "${direction == 'server-to-agents' || direction == null}",
                Config = new JobStepConfig
                {
                    Command = "orbit:file:sync",
                    Pattern = agentPattern,
                    Payload = new Dictionary<string, object?>
                    {
                        ["source"] = serverPath,
                        ["destination"] = agentPath,
                        ["deleteOrphans"] = false
                    }
                },
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(5),
                OutputVariable = "syncToAgentsResult"
            });
        }

        if (mode is "twoway" or "agent-to-server")
        {
            steps.Add(new WorkflowStep
            {
                Id = "upload-to-server",
                Name = "Upload Changed Files to Server",
                Type = StepType.Job,
                Condition = "${direction == 'agent-to-server'}",
                Config = new JobStepConfig
                {
                    Command = "orbit:file:upload",
                    Pattern = "${agentId}",
                    Payload = new Dictionary<string, object?>
                    {
                        ["sourcePath"] = "${changedPath}",
                        ["destinationUrl"] = "${serverUploadUrl}",
                        ["includeChecksum"] = true,
                        ["overwrite"] = true
                    }
                },
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(5),
                OutputVariable = "uploadResult"
            });
        }

        return new WorkflowDefinition
        {
            Id = $"orbit:workflow:twoway-sync-{Guid.NewGuid():N}",
            Name = "Two-Way File Synchronization",
            Version = "1.0.0",
            Description = $"Bidirectional sync between server ({serverPath}) and agents ({agentPath})",
            Tags = ["built-in", "sync", "file", "twoway", "realtime"],
            Variables = new Dictionary<string, object?>
            {
                ["serverPath"] = serverPath,
                ["agentPath"] = agentPath,
                ["mode"] = mode,
                ["filter"] = filter,
                ["serverUploadUrl"] = $"/api/files/upload?path={Uri.EscapeDataString(serverPath)}"
            },
            Triggers = triggers.AsReadOnly(),
            Steps = steps.AsReadOnly(),
            ErrorHandling = new WorkflowErrorHandling
            {
                Strategy = ErrorStrategy.ContinueAndAggregate
            }
        };
    }

    /// <summary>
    /// Creates a file watch workflow that triggers on agent file changes.
    /// </summary>
    /// <param name="watchPath">Directory path to watch on agents.</param>
    /// <param name="agentPattern">Agent pattern for targeting.</param>
    /// <param name="filter">File filter pattern.</param>
    /// <param name="onChangeCommand">Command to execute when files change.</param>
    /// <param name="changeTypes">Types of changes to watch for.</param>
    /// <returns>A workflow definition for file watch.</returns>
    public static WorkflowDefinition FileWatch(
        string watchPath,
        string agentPattern = "*",
        string filter = "*.*",
        string onChangeCommand = "orbit:system:ping",
        IReadOnlyList<string>? changeTypes = null)
    {
        changeTypes ??= ["Created", "Modified", "Deleted"];

        return new WorkflowDefinition
        {
            Id = $"orbit:workflow:file-watch-{Guid.NewGuid():N}",
            Name = "File Watch Workflow",
            Version = "1.0.0",
            Description = $"Execute actions when files change in {watchPath}",
            Tags = ["built-in", "watch", "file", "trigger"],
            Variables = new Dictionary<string, object?>
            {
                ["watchPath"] = watchPath,
                ["filter"] = filter,
                ["onChangeCommand"] = onChangeCommand
            },
            Triggers =
            [
                new FileWatchTrigger
                {
                    Id = "file-watch",
                    Name = "File Change Trigger",
                    AgentPattern = agentPattern,
                    WatchPath = watchPath,
                    Filter = filter,
                    IncludeSubdirectories = true,
                    ChangeTypes = changeTypes.ToList(),
                    DebounceMs = 1000,
                    InputMapping = new Dictionary<string, string>
                    {
                        ["changedPath"] = "$.fullPath",
                        ["changeType"] = "$.changeType",
                        ["fileName"] = "$.name",
                        ["agentId"] = "$.agentId"
                    }
                },
                new ManualTrigger
                {
                    Id = "manual",
                    Name = "Manual Trigger",
                    InputSchema = new Dictionary<string, InputParameterDefinition>
                    {
                        ["changedPath"] = new() { Type = InputParameterType.StringValue, Required = true, Description = "Path to the changed file" },
                        ["changeType"] = new() { Type = InputParameterType.StringValue, Required = true, Description = "Type of change", AllowedValues = ["Created", "Modified", "Deleted", "Renamed"] }
                    }
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "log-change",
                    Name = "Log File Change",
                    Type = StepType.Log,
                    Config = new LogStepConfig
                    {
                        Level = "Info",
                        Message = "File ${changeType}: ${changedPath} on agent ${agentId}"
                    }
                },
                new WorkflowStep
                {
                    Id = "execute-action",
                    Name = "Execute Change Action",
                    Type = StepType.Job,
                    DependsOn = ["log-change"],
                    Config = new JobStepConfig
                    {
                        Command = onChangeCommand,
                        Pattern = "${agentId}",
                        Payload = new Dictionary<string, object?>
                        {
                            ["triggeredBy"] = "file-change",
                            ["changedPath"] = "${changedPath}",
                            ["changeType"] = "${changeType}"
                        }
                    },
                    OutputVariable = "actionResult"
                }
            ]
        };
    }

    /// <summary>
    /// Creates an alert workflow for monitoring failures.
    /// </summary>
    /// <param name="webhookUrl">Webhook URL for alerts.</param>
    /// <returns>A workflow definition for alerting.</returns>
    public static WorkflowDefinition AlertOnFailure(string webhookUrl)
    {
        return new WorkflowDefinition
        {
            Id = "orbit:workflow:alert-on-failure",
            Name = "Alert on Job Failure",
            Version = "1.0.0",
            Description = "Send alert notification when jobs fail",
            Tags = ["built-in", "alerting", "monitoring"],
            Triggers =
            [
                new JobCompletionTrigger
                {
                    Id = "job-failed",
                    Name = "Job Failure Trigger",
                    CommandPattern = "*",
                    Statuses = ["Failed"]
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    Id = "send-alert",
                    Name = "Send Alert Notification",
                    Type = StepType.Notify,
                    Config = new NotifyStepConfig
                    {
                        Channel = NotifyChannel.Webhook,
                        Target = webhookUrl,
                        Subject = "Job Failure Alert",
                        Message = "Job ${trigger.jobId} failed on agent ${trigger.agentId}. Error: ${trigger.error}"
                    }
                }
            ]
        };
    }
}
