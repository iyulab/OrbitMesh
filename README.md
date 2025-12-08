# OrbitMesh

> **Infrastructure SDK for Distributed Agent Systems**
>
> Connect, orchestrate, and manage distributed agents with minimal boilerplate.

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Status-Phase%202%20In%20Progress-blue)](docs/IMPLEMENTATION_PHASES.md)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)

OrbitMesh handles the complex infrastructure of distributed systems—connection management, reliable message delivery, job orchestration, and state synchronization—so you can focus on your domain logic.

## Why OrbitMesh?

Building distributed agent systems requires solving the same infrastructure problems repeatedly:

| Challenge | Without OrbitMesh | With OrbitMesh |
|-----------|-------------------|----------------|
| Agent connectivity | Custom WebSocket/gRPC implementation | Built-in SignalR with auto-reconnect |
| Message delivery | Manual ACK/retry logic | Reliable delivery with idempotency |
| Job distribution | Custom queue + dispatcher | Channel-based dispatcher with backpressure |
| Progress tracking | Ad-hoc polling/callbacks | Structured progress reporting |
| Failure handling | Manual retry/circuit breaker | Polly-based resilience patterns |
| State sync | Custom sync protocol | Twin pattern with conflict resolution |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Your Applications                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Data        │  │ GPU Cluster │  │ App         │  ...         │
│  │ Collector   │  │ Manager     │  │ Deployer    │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
├─────────────────────────────────────────────────────────────────┤
│                     O R B I T M E S H                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Connection │ Execution │ Workflow │ State │ Monitoring  │   │
│  └──────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│                     Infrastructure                              │
│            (Network, Servers, Agents, Storage)                  │
└─────────────────────────────────────────────────────────────────┘
```

## Key Features

| Capability | Description |
|------------|-------------|
| **Connection Management** | Server-agent bidirectional communication with auto-reconnect |
| **Execution Engine** | Fire & Forget, Request-Response, Streaming, Long-running Jobs |
| **Workflow Engine** | Multi-step orchestration with branching, rollback, scheduling |
| **State Management** | Agent state, Job state with Event Sourcing |
| **Resilience** | Retry policies, Circuit breakers, Dead letter queues |
| **Monitoring** | Health checks, Metrics, Distributed tracing |

## Quick Start

### Server

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOrbitMeshServer();

var app = builder.Build();
app.MapOrbitMeshHub();
app.Run();
```

### Agent

```csharp
await using var agent = await MeshAgentBuilder
    .Create("http://localhost:5000")
    .WithId("worker-1")
    .WithCapability("process-data", async ctx =>
    {
        var input = ctx.GetRequiredParameter<ProcessRequest>();
        return MessagePackSerializer.Serialize(result);
    })
    .InGroup("workers")
    .BuildAndConnectAsync();

await agent.WaitForShutdownAsync();
```

## Execution Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Fire & Forget** | Execute without waiting | Notifications, Logging |
| **Request-Response** | Immediate result | Status checks, Quick queries |
| **Streaming** | Continuous data flow | LLM inference, Log tailing |
| **Long-Running Job** | Progress + Final result | ML training, Data processing |
| **Workflow** | Multi-step orchestration | Deployments, Pipelines |

## Use Cases

### GPU Cluster Management

```csharp
var agent = await _mesh.SelectAgent(
    capability: AgentCapability.Gpu,
    selector: agents => agents.OrderBy(a => a.GpuUtilization).First());

return await _mesh.StreamAsync<InferenceResult>(
    agent, "run-inference", new { model, input });
```

### Rolling Deployment

```csharp
var workflow = new MeshWorkflow("deploy-app")
    .AddStep<HealthCheck>()
    .AddStep<StopService>()
    .AddStep<DownloadPackage>(cfg => cfg.Version = version)
    .AddStep<StartService>()
    .OnFailure<Rollback>()
    .Build();

await _mesh.ExecuteRollingAsync(workflow, batchPercent: 20);
```

### Edge Device Control

```csharp
var agents = await _mesh.GetAgentsByGroup("sensors");
await Task.WhenAll(agents.Select(agent =>
    _mesh.ExecuteAsync(agent, "update-firmware", firmware)));
```

## Installation

```bash
dotnet add package OrbitMesh.Server    # Server
dotnet add package OrbitMesh.Agent     # Agent
dotnet add package OrbitMesh.Workflows # Workflows (optional)
```

## Implementation Status

| Phase | Description | Status |
|-------|-------------|--------|
| **Phase 1** | Foundation (Core & Transport) | ✅ Complete |
| **Phase 2** | Execution Engine (Dispatcher & Reliability) | 🔄 In Progress |
| **Phase 3** | Persistence & State (Event Sourcing) | ⏳ Planned |
| **Phase 4** | Workflow Engine (Orchestration) | ⏳ Planned |
| **Phase 5** | Production Readiness (Security & Scale) | ⏳ Planned |

See [IMPLEMENTATION_PHASES.md](docs/IMPLEMENTATION_PHASES.md) for detailed roadmap.

## Package Structure

| Package | Description |
|---------|-------------|
| `OrbitMesh.Core` | Core abstractions and interfaces |
| `OrbitMesh.Server` | Server implementation |
| `OrbitMesh.Agent` | Agent implementation |
| `OrbitMesh.Client` | Client library for job submission |
| `OrbitMesh.Workflows` | Workflow engine |
| `OrbitMesh.Storage.Sqlite` | SQLite storage provider |

## Technology Stack

| Component | Technology | Rationale |
|-----------|------------|-----------|
| Runtime | .NET 10.0 | Latest, Native AOT ready |
| Communication | SignalR | Bidirectional, Auto-fallback |
| Serialization | MessagePack | Compact, Fast |
| State Machine | Stateless | Agent/Job lifecycle |
| Resilience | Polly | Retry, Circuit Breaker |
| Observability | OpenTelemetry | Vendor-neutral |

## Design Principles

**Zero Domain Knowledge** — OrbitMesh handles infrastructure, you handle business logic.

**Pluggable Everything** — Storage, serialization, middleware are all replaceable.

**Layered Abstraction** — From raw bytes to high-level workflows, choose your level.

```csharp
// Low level
await mesh.SendRawAsync(agent, bytes);

// Mid level
await mesh.ExecuteAsync(agent, "restart-service");

// High level
await mesh.ExecuteWorkflowAsync("deploy-app");
```

## License

MIT License

## Links

- [Documentation](docs/)
- [Implementation Roadmap](docs/IMPLEMENTATION_PHASES.md)
- [Issues](https://github.com/iyulab/OrbitMesh/issues)
