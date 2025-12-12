# Built-in Workflows

This directory contains built-in workflow definitions for the OrbitMesh Host.

## How It Works

1. **SDK Templates**: Base templates are provided by `OrbitMesh.Workflows` SDK
2. **Product Customization**: This directory allows product-level customization
3. **Configuration**: Features are enabled/disabled via `appsettings.json`

## Available Workflows

### Data Collection
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `system-inventory.yaml` | Collects system inventory (hardware, OS, software) | `Features:DataCollection:Inventory` |
| `metrics-collection.yaml` | Periodic metrics collection with alerting | `Features:DataCollection:Metrics` |
| `log-collection.yaml` | Log file collection from agents | `Features:DataCollection:Logs` |

### Script Execution
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `remote-exec.yaml` | Execute commands on remote agents | `Features:ScriptExecution` |
| `script-deploy-run.yaml` | Deploy and execute scripts (bash, PowerShell, Python) | `Features:ScriptExecution` |
| `batch-command.yaml` | Execute multiple commands in sequence | `Features:ScriptExecution` |

### File Management
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `file-sync-twoway.yaml` | Two-way file synchronization | `Features:FileSync` |
| `config-deploy.yaml` | Deploy configuration files with backup/rollback | `Features:FileSync` |
| `backup-collect.yaml` | Collect backup files from agents | `Features:FileSync` |

### Service Management
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `service-status-check.yaml` | Check service status across agents | `Features:ServiceManagement` |
| `rolling-restart.yaml` | Rolling service restart with health checks | `Features:ServiceManagement` |

### Monitoring
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `health-monitor.yaml` | Agent health monitoring with alerting | `Features:HealthMonitor` |
| `disk-space-alert.yaml` | Disk space monitoring with thresholds | `Features:Monitoring:DiskSpace` |
| `process-monitor.yaml` | Process monitoring and alerting | `Features:Monitoring:Process` |
| `connectivity-check.yaml` | Network connectivity testing | `Features:Monitoring:Connectivity` |

### Deployment
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `blue-green-deploy.yaml` | Blue-green deployment strategy | `Features:Deployment` |
| `canary-deploy.yaml` | Canary deployment with gradual rollout | `Features:Deployment` |
| `app-update.yaml` | Application update with pre/post scripts | `Features:Deployment` |

### Maintenance
| Workflow | Description | Configuration |
|----------|-------------|---------------|
| `cleanup-temp.yaml` | Cleanup temporary files and directories | `Features:Maintenance:Cleanup` |
| `log-rotate.yaml` | Log rotation and compression | `Features:Maintenance:LogRotation` |
| `scheduled-reboot.yaml` | Scheduled agent reboots with coordination | `Features:Maintenance` |

## Configuration Example

```json
{
  "OrbitMesh": {
    "Features": {
      "FileSync": {
        "Enabled": true,
        "RootPath": "./files",
        "WatchEnabled": true,
        "SyncMode": "TwoWay"
      },
      "HealthMonitor": {
        "Enabled": true,
        "Interval": "5m",
        "AgentPattern": "*",
        "AutoRestart": false
      },
      "DataCollection": {
        "Enabled": true,
        "Inventory": { "Enabled": true, "Interval": "24h" },
        "Metrics": { "Enabled": true, "Interval": "1m" }
      },
      "Monitoring": {
        "Enabled": true,
        "DiskSpace": { "Enabled": true, "WarningThreshold": 80, "CriticalThreshold": 90 },
        "Process": { "Enabled": true, "Processes": ["nginx", "app"] }
      },
      "Deployment": {
        "Enabled": true,
        "DefaultStrategy": "rolling",
        "RollbackOnFailure": true
      },
      "Maintenance": {
        "Enabled": true,
        "Cleanup": { "Enabled": true, "Interval": "24h", "OlderThanDays": 7 },
        "LogRotation": { "Enabled": true, "MaxSizeMb": 100, "KeepRotations": 5 }
      }
    }
  }
}
```

## Customization

To customize a built-in workflow:

1. Copy the SDK template from `OrbitMesh.Workflows/BuiltIn/Templates/`
2. Place it in this directory with the same filename
3. Modify as needed - the product version will override the SDK template

## Creating Custom Workflows

Add any `.yaml` file to this directory following the workflow schema:

```yaml
id: custom:workflow:my-workflow
name: My Custom Workflow
version: "1.0.0"
description: Description of what this workflow does
enabled: true

triggers:
  - id: manual
    type: manual
    name: Manual Trigger
    input_schema:
      param1:
        type: string
        required: true
        description: Parameter description

steps:
  - id: step-1
    name: First Step
    type: job
    command: orbit:system.health
    pattern: "*"
    timeout: 30s
    output_variable: healthResults

  - id: step-2
    name: Process Results
    type: script
    language: javascript
    script: |
      const results = context.healthResults || [];
      return { processed: results.length };
```

## Workflow Structure

- **id**: Unique workflow identifier (format: `namespace:category:name`)
- **name**: Human-readable name
- **version**: Semantic version
- **description**: Workflow description
- **enabled**: Whether workflow is active
- **tags**: Categorization tags
- **triggers**: How workflow can be started (schedule, manual, event)
- **steps**: Sequential execution steps

## Step Types

- **job**: Execute agent commands (`orbit:*`)
- **script**: Run JavaScript for data processing
- **notification**: Send alerts/notifications
- **condition**: Conditional execution based on expressions
