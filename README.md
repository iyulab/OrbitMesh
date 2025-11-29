# OrbitMesh

> **Foundation Layer for Distributed Systems**
>
> Build distributed applications the way you build web apps with ASP.NET Core.

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Status-Phase%201%20Complete-green)](docs/IMPLEMENTATION_PHASES.md)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)

OrbitMesh is **not** an end-user application. It's an **infrastructure SDK** that provides building blocks for constructing distributed systems - just like ASP.NET Core does for web applications.

## Positioning

```
Web Development          Distributed Systems
─────────────────        ─────────────────────
ASP.NET Core       ≈     OrbitMesh
     │                        │
     ▼                        ▼
E-commerce Site          GPU Cluster Manager
Blog Platform            Data Collection System
API Server               App Deployment Tool
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Consumer Applications                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Data        │  │ GPU Cluster │  │ App         │  ...         │
│  │ Collector   │  │ Manager     │  │ Deployer    │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
├─────────────────────────────────────────────────────────────────┤
│                     O R B I T M E S H                            │
│              Foundation Layer for Distributed Systems            │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Connection │ Execution │ Workflow │ State │ Monitoring  │   │
│  └──────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│                     Infrastructure                               │
│            (Network, Servers, Agents, Storage)                   │
└─────────────────────────────────────────────────────────────────┘
```

## Implementation Status

| Phase | Description | Status |
|-------|-------------|--------|
| **Phase 1** | Foundation (Core & Transport) | ✅ Complete |
| **Phase 2** | Execution Engine (Dispatcher & Reliability) | 🔜 Next |
| **Phase 3** | Persistence & State (Event Sourcing) | ⏳ Planned |
| **Phase 4** | Workflow Engine (Orchestration) | ⏳ Planned |
| **Phase 5** | Production Readiness (Security & Scale) | ⏳ Planned |
| **Phase 6** | Advanced Features (Optional) | ⏳ Planned |

See [IMPLEMENTATION_PHASES.md](docs/IMPLEMENTATION_PHASES.md) for detailed roadmap.

## What OrbitMesh Provides

| Capability | Description |
|------------|-------------|
| **Connection Management** | Server-agent bidirectional communication via SignalR |
| **Execution Engine** | Fire & Forget, Request-Response, Streaming, Long-running Jobs |
| **Workflow Engine** | Multi-step orchestration with branching, rollback, scheduling |
| **State Management** | Agent state, Job state, Workflow state with Event Sourcing |
| **Monitoring** | Health checks, Metrics, Distributed tracing |
| **Extension Points** | Custom handlers, Middleware, Plugins |

## What Your Application Adds

| OrbitMesh Provides | Your Application Adds |
|--------------------|----------------------|
| Agent connection & auth | Domain-specific agent capabilities |
| Message routing | Business logic |
| Execution patterns | Custom handlers |
| Workflow engine | Domain workflows |
| State management | Domain state |

## Use Cases

### GPU Cluster Manager

```csharp
public class GpuClusterManager
{
    private readonly IMeshClient _mesh;

    public async Task<InferenceResult> RunInference(Model model, Input input)
    {
        // Select GPU-capable agent with lowest utilization
        var agent = await _mesh.SelectAgent(
            capability: AgentCapability.Gpu,
            selector: agents => agents.OrderBy(a => a.GpuUtilization).First());

        // Execute inference with streaming response
        return await _mesh.StreamAsync<InferenceResult>(
            agent, "run-inference", new { model, input });
    }
}
```

### App Deployment System

```csharp
public class AppDeployer
{
    private readonly IMeshClient _mesh;

    public async Task RollingDeploy(string appName, string version)
    {
        var workflow = new MeshWorkflow("deploy-app")
            .AddStep<HealthCheck>()
            .AddStep<StopService>()
            .AddStep<DownloadPackage>(cfg => cfg.Version = version)
            .AddStep<ExtractAndInstall>()
            .AddStep<StartService>()
            .AddStep<VerifyHealth>()
            .OnFailure<Rollback>()
            .Build();

        // Rolling deploy: 20% at a time
        await _mesh.ExecuteRollingAsync(workflow, batchPercent: 20);
    }
}
```

### Data Collection System

```csharp
public class DataCollector
{
    private readonly IMeshClient _mesh;

    public async Task CollectFromAllAgents()
    {
        var workflow = new MeshWorkflow("collect-metrics")
            .AddStep<GatherSystemMetrics>()
            .AddStep<GatherApplicationLogs>()
            .AddStep<AggregateResults>()
            .Build();

        // Execute on all agents in parallel
        await _mesh.ExecuteOnAllAsync(workflow);
    }
}
```

### Edge Device Controller

```csharp
public class EdgeController
{
    private readonly IMeshClient _mesh;

    public async Task UpdateFirmware(string deviceGroup, byte[] firmware)
    {
        var agents = await _mesh.GetAgentsByGroup(deviceGroup);

        foreach (var batch in agents.Chunk(10))
        {
            await Task.WhenAll(batch.Select(agent =>
                _mesh.ExecuteAsync(agent, "update-firmware", firmware)));
        }
    }
}
```

## Installation

```bash
# Server
dotnet add package OrbitMesh.Server

# Agent
dotnet add package OrbitMesh.Agent

# Workflows (optional)
dotnet add package OrbitMesh.Workflows
```

## Quick Start

### Server Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add OrbitMesh server services with MessagePack
builder.Services.AddOrbitMeshServer();

var app = builder.Build();

// Map the agent hub endpoint
app.MapOrbitMeshHub();  // Default: /agent

app.Run();
```

### Agent Setup (Standalone)

```csharp
await using var agent = await MeshAgentBuilder
    .Create("http://localhost:5000")
    .WithId("worker-1")
    .WithName("Data Processor")
    .WithCapability("process-data", async ctx =>
    {
        var input = ctx.GetRequiredParameter<ProcessRequest>();
        // Process data...
        return MessagePackSerializer.Serialize(result);
    })
    .InGroup("workers")
    .WithTag("environment:production")
    .BuildAndConnectAsync();

await agent.WaitForShutdownAsync();
```

### Agent Setup (Hosted Service)

```csharp
builder.Services.AddOrbitMeshAgentHostedService(
    "http://localhost:5000",
    agent => agent
        .WithId("worker-1")
        .WithName("Background Worker")
        .WithCapability("process", handler));
```

### Custom Handler

```csharp
public class GpuInferenceHandler : IRequestResponseHandler<InferenceResult>
{
    public string Command => "run-inference";

    public async Task<InferenceResult> HandleAsync(
        CommandContext context,
        CancellationToken ct)
    {
        var model = context.GetRequiredParameter<Model>();
        var input = context.GetParameter<Input>("input");

        // Your domain logic here
        return await RunGpuInference(model, input, ct);
    }
}
```

## Core Principles

### 1. Zero Domain Knowledge

OrbitMesh core knows nothing about:
- What GPU inference is
- What deployment means
- What data collection involves

OrbitMesh core knows how to:
- Connect agents to servers
- Send and receive messages
- Execute and track jobs
- Orchestrate workflows

### 2. Minimal Opinions, Maximum Flexibility

```csharp
// OrbitMesh: provides "how" to execute
await mesh.ExecuteAsync(agent, command, parameters);

// Your App: defines "what" to execute
public class YourHandler : IMeshHandler { ... }
```

### 3. Layered Abstraction

```
High Level   │  mesh.ExecuteWorkflowAsync("deploy-app")
             │
             │  mesh.ExecuteAsync(agent, "restart-service")
             │
             │  mesh.InvokeAsync<T>(agent, "get-metrics")
             │
Low Level    │  mesh.SendRawAsync(agent, bytes)
```

### 4. Pluggable Everything

```csharp
services.AddOrbitMesh(options =>
{
    options.UseStorage<RedisJobStorage>();
    options.UseSerializer<MessagePackSerializer>();
    options.UseMiddleware<LoggingMiddleware>();
    options.UseMiddleware<MetricsMiddleware>();
});
```

## Execution Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Fire & Forget** | Execute without waiting | Notifications, Logging |
| **Request-Response** | Immediate result | Status checks, Quick queries |
| **Streaming** | Continuous data flow | LLM inference, Log tailing |
| **Long-Running Job** | Progress + Final result | ML training, Data processing |
| **Workflow** | Multi-step orchestration | Deployments, Pipelines |

## Package Structure

| Package | Description |
|---------|-------------|
| `OrbitMesh.Core` | Core abstractions and interfaces |
| `OrbitMesh.Server` | Server implementation |
| `OrbitMesh.Agent` | Agent implementation |
| `OrbitMesh.Workflows` | Workflow engine |
| `OrbitMesh.Storage.Sqlite` | SQLite storage provider |
| `OrbitMesh.Storage.SqlServer` | SQL Server storage provider |
| `OrbitMesh.Storage.Redis` | Redis storage provider |

## Technology Stack

| Component | Technology | Version | Rationale |
|-----------|------------|---------|-----------|
| Runtime | .NET | 10.0 | Latest, Native AOT ready |
| Language | C# | preview (14) | Latest language features |
| Communication | SignalR | 10.0.0 | Bidirectional, Auto-fallback |
| Serialization | MessagePack | 3.1.3 | Compact, Fast |
| State Machine | Stateless | 5.16.0 | Agent/Job lifecycle |
| Resilience | Polly | 8.5.2 | Retry, Circuit Breaker |
| Storage | SQLite (EF Core) | 10.0.0 | Embedded, WAL mode |
| Observability | OpenTelemetry | 1.11.2 | Vendor-neutral |
| Logging | Serilog | 4.3.0 | Structured logging |
| Testing | xUnit + FluentAssertions | 2.9.3 / 8.0.1 | Comprehensive testing |

## Requirements

- .NET 10.0 SDK
- ASP.NET Core (Server)

## Summary

```
Without OrbitMesh:
  "To build a GPU cluster management system...
   I need to implement connection management, authentication,
   message protocols, reconnection, state sync, job queues,
   scheduling, monitoring... all from scratch"

With OrbitMesh:
  "To build a GPU cluster management system...
   I just implement GPU-specific handlers on top of OrbitMesh"
```

## License

MIT License

## Links

- [Documentation](https://github.com/iyulab/OrbitMesh/docs)
- [NuGet](https://www.nuget.org/packages/OrbitMesh)
- [Issues](https://github.com/iyulab/OrbitMesh/issues)
