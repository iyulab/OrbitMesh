# OrbitMesh Architecture

## Foundation Layer Philosophy

OrbitMesh is a **Foundation Layer** for distributed systems - not an end-user application.

```
┌─────────────────────────────────────────────────────────────────┐
│                     YOUR APPLICATION                             │
│         (Data Collector, GPU Manager, Deployer, etc.)           │
└────────────────────────────┬────────────────────────────────────┘
                             │ uses
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ORBITMESH                                  │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Connection  │  │  Execution  │  │  Workflow   │              │
│  │ Management  │  │   Engine    │  │   Engine    │              │
│  │             │  │             │  │             │              │
│  │ • Connect   │  │ • Fire&Forget│ │ • Steps     │              │
│  │ • Auth      │  │ • Request   │  │ • Branching │              │
│  │ • Reconnect │  │ • Streaming │  │ • Rollback  │              │
│  │ • Groups    │  │ • Jobs      │  │ • Scheduling│              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │    State    │  │  Monitoring │  │  Extension  │              │
│  │  Management │  │  & Logging  │  │   Points    │              │
│  │             │  │             │  │             │              │
│  │ • Agent State│ │ • Health    │  │ • Handlers  │              │
│  │ • Job State │  │ • Metrics   │  │ • Middleware│              │
│  │ • Workflow  │  │ • Tracing   │  │ • Plugins   │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
└─────────────────────────────────────────────────────────────────┘
```

### Design Principles

| Principle | Description |
|-----------|-------------|
| **Zero Domain Knowledge** | OrbitMesh knows nothing about GPU, deployment, or data collection |
| **Minimal Opinions** | Provides "how" to execute, your app defines "what" to execute |
| **Pluggable Everything** | Storage, Serializer, Transport, Middleware - all replaceable |
| **Layered Abstraction** | From high-level workflows to low-level raw messaging |

### Responsibility Separation

| OrbitMesh Provides | Your Application Adds |
|--------------------|----------------------|
| Agent connection & auth | Domain-specific agent capabilities |
| Message routing | Business logic |
| Execution patterns | Custom handlers |
| Workflow engine | Domain workflows |
| State management | Domain state |
| Base monitoring | Domain metrics |

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CLIENT APPLICATIONS                                │
│                    (Dashboard, CLI, SDK Consumers)                          │
└─────────────────────────────────────────────┬───────────────────────────────┘
                                              │
                                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MESH SERVER                                     │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │   SignalR Hub   │  │  REST API       │  │   Admin Dashboard           │  │
│  │  (AgentHub)     │  │  (Management)   │  │   (Blazor/React)            │  │
│  └────────┬────────┘  └────────┬────────┘  └─────────────────────────────┘  │
│           │                    │                                             │
│           ▼                    ▼                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      ORCHESTRATION ENGINE                            │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │  Dispatcher  │  │  Scheduler   │  │  Job Manager │              │    │
│  │  │  (Channel)   │  │  (DAG)       │  │  (State)     │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      WORKFLOW ENGINE                                 │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │  Definition  │  │  Execution   │  │  Compensation│              │    │
│  │  │  (Builder)   │  │  (Runtime)   │  │  (Saga)      │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      PERSISTENCE LAYER                               │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │ Event Store  │  │ Agent Store  │  │ Job Store    │              │    │
│  │  │ (Sourcing)   │  │ (Registry)   │  │ (State)      │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  │           │                                                         │    │
│  │           ▼                                                         │    │
│  │  ┌─────────────────────────────────────────────────────────────┐   │    │
│  │  │  Storage Provider (SQLite / SQL Server / PostgreSQL / Redis) │   │    │
│  │  └─────────────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      INFRASTRUCTURE                                  │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │ Telemetry    │  │ Security     │  │ Resilience   │              │    │
│  │  │ (OTel)       │  │ (JWT/mTLS)   │  │ (Polly)      │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────┬───────────────────────────────┘
                                              │
                        SignalR (WebSocket + MessagePack)
                                              │
          ┌───────────────────────────────────┼───────────────────────────────┐
          │                                   │                               │
          ▼                                   ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐         ┌─────────────────────┐
│     MESH AGENT      │         │     MESH AGENT      │         │     MESH AGENT      │
│  ┌───────────────┐  │         │  ┌───────────────┐  │         │  ┌───────────────┐  │
│  │  Connection   │  │         │  │  Connection   │  │         │  │  Connection   │  │
│  │  Manager      │  │         │  │  Manager      │  │         │  │  Manager      │  │
│  └───────┬───────┘  │         │  └───────┬───────┘  │         │  └───────┬───────┘  │
│          │          │         │          │          │         │          │          │
│  ┌───────▼───────┐  │         │  ┌───────▼───────┐  │         │  ┌───────▼───────┐  │
│  │  Task Runner  │  │         │  │  Task Runner  │  │         │  │  Task Runner  │  │
│  │  (Executor)   │  │         │  │  (Executor)   │  │         │  │  (Executor)   │  │
│  └───────┬───────┘  │         │  └───────┬───────┘  │         │  └───────┬───────┘  │
│          │          │         │          │          │         │          │          │
│  ┌───────▼───────┐  │         │  ┌───────▼───────┐  │         │  ┌───────▼───────┐  │
│  │  State Twin   │  │         │  │  State Twin   │  │         │  │  State Twin   │  │
│  │  (Sync)       │  │         │  │  (Sync)       │  │         │  │  (Sync)       │  │
│  └───────────────┘  │         │  └───────────────┘  │         │  └───────────────┘  │
└─────────────────────┘         └─────────────────────┘         └─────────────────────┘
      Node A                          Node B                          Node C
```

## Layer Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                              │
│  • Workflow Definition (Fluent Builder)                           │
│  • Job Scheduling APIs                                            │
│  • Admin/Monitoring Interfaces                                    │
├────────────────────────────────────────────────────────────────────┤
│                     ORCHESTRATION LAYER                           │
│  • Central Dispatcher (Push-based)                                │
│  • DAG-based Workflow Execution                                   │
│  • Saga Pattern for Compensation                                  │
│  • State Machine (Agent/Job Lifecycle)                           │
├────────────────────────────────────────────────────────────────────┤
│                     COMMUNICATION LAYER                           │
│  • SignalR Hub (Strongly-typed)                                   │
│  • MessagePack Serialization                                      │
│  • ACK/NACK Protocol                                              │
│  • Streaming (IAsyncEnumerable)                                   │
├────────────────────────────────────────────────────────────────────┤
│                     PERSISTENCE LAYER                             │
│  • Event Sourcing (Checkpoint/Replay)                             │
│  • Storage Provider Abstraction                                   │
│  • Agent Twin State Sync                                          │
├────────────────────────────────────────────────────────────────────┤
│                     INFRASTRUCTURE LAYER                          │
│  • Security (JWT + mTLS)                                          │
│  • Resilience (Polly: Retry, Circuit Breaker)                     │
│  • Observability (OpenTelemetry)                                  │
│  • Scale-out (Redis Backplane)                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Core Components

### Server Components

| Component | Responsibility | Key Patterns |
|-----------|---------------|--------------|
| **AgentHub** | SignalR endpoint for agent communication | Strongly-typed Hub, Groups |
| **Dispatcher** | Push-based task distribution | Channel<T>, Backpressure |
| **Scheduler** | DAG-based workflow scheduling | Topological Sort, Parallel Execution |
| **JobManager** | Job lifecycle and state tracking | State Machine (Stateless lib) |
| **EventStore** | Workflow state persistence | Event Sourcing, Checkpoint/Replay |
| **AgentRegistry** | Connected agent management | Twin Pattern, Heartbeat |

### Agent Components

| Component | Responsibility | Key Patterns |
|-----------|---------------|--------------|
| **ConnectionManager** | Server connection lifecycle | Auto-reconnect, Jitter |
| **TaskRunner** | Task execution engine | Streaming, Cancellation |
| **StateTwin** | Local state sync with server | Desired/Reported Properties |
| **CapabilityProvider** | Agent capability declaration | Tag-based Matching |

## Communication Protocol

### Message Flow

```
┌──────────┐                                           ┌──────────┐
│  Server  │                                           │  Agent   │
└────┬─────┘                                           └────┬─────┘
     │                                                      │
     │  1. JobRequest (with IdempotencyKey)                │
     │ ─────────────────────────────────────────────────► │
     │                                                      │
     │  2. JobAccepted (ACK)                               │
     │ ◄───────────────────────────────────────────────── │
     │                                                      │
     │  3. Progress Stream (IAsyncEnumerable)              │
     │ ◄───────────────────────────────────────────────── │
     │                                                      │
     │  4. JobCompleted / JobFailed                        │
     │ ◄───────────────────────────────────────────────── │
     │                                                      │
```

### Execution Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Fire & Forget** | No response expected | Log collection, Notifications |
| **Request-Response** | Synchronous result | Quick queries, Status checks |
| **Streaming** | Continuous data flow | LLM inference, Log tailing |
| **Long-Running Job** | Progress + Final result | ML training, Data processing |
| **Workflow** | Multi-step orchestration | Deployment pipelines |

## State Management

### Agent State Machine

```
                    ┌─────────────┐
                    │   Created   │
                    └──────┬──────┘
                           │ Initialize
                           ▼
                    ┌─────────────┐
          ┌────────│ Initializing│────────┐
          │        └──────┬──────┘        │
          │ Fault         │ Start         │ Fault
          ▼               ▼               ▼
   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
   │   Faulted   │ │    Ready    │ │   Faulted   │
   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘
          │               │ Execute       │
          │ Recover       ▼               │ Recover
          │        ┌─────────────┐        │
          │        │   Running   │────────┤
          │        └──────┬──────┘        │
          │               │               │
          │      ┌────────┼────────┐      │
          │      │ Pause  │ Stop   │      │
          │      ▼        ▼        ▼      │
          │ ┌────────┐ ┌────────┐        │
          └─│ Paused │ │Stopping│────────┘
            └───┬────┘ └───┬────┘
                │ Resume   │
                │          ▼
                │    ┌─────────┐
                └────│ Stopped │
                     └─────────┘
```

### Job State Machine

```
     ┌──────────┐
     │ Pending  │
     └────┬─────┘
          │ Assign
          ▼
     ┌──────────┐
     │ Assigned │──────┐
     └────┬─────┘      │ Timeout (No ACK)
          │ ACK        │
          ▼            │
     ┌──────────┐      │
     │ Running  │◄─────┘ Reassign
     └────┬─────┘
          │
    ┌─────┴─────┐
    ▼           ▼
┌────────┐ ┌────────┐
│Complete│ │ Failed │
└────────┘ └───┬────┘
               │ Retry (< MaxAttempts)
               └──────► Pending
```

## Technology Stack

### Core Dependencies

| Category | Technology | Rationale |
|----------|------------|-----------|
| **Runtime** | .NET 8.0+ | LTS, Performance, Native AOT |
| **Communication** | SignalR | Bidirectional, Auto-fallback |
| **Serialization** | MessagePack | 37% smaller, 2-5x faster |
| **State Machine** | Stateless | Explicit transitions, Async |
| **Resilience** | Polly v8 | Retry, Circuit Breaker, Timeout |
| **Storage** | SQLite (default) | Embedded, WAL mode |
| **Telemetry** | OpenTelemetry | Vendor-neutral, Standards |
| **Logging** | Serilog | Structured, Rich sinks |

### Package Structure

```
OrbitMesh/
├── src/
│   ├── OrbitMesh.Core/              # Shared models, interfaces, abstractions
│   ├── OrbitMesh.Server/            # Server implementation
│   ├── OrbitMesh.Agent/             # Agent implementation
│   ├── OrbitMesh.Workflows/         # Workflow engine
│   ├── OrbitMesh.Storage.Sqlite/    # SQLite storage provider
│   ├── OrbitMesh.Storage.SqlServer/ # SQL Server storage provider
│   └── OrbitMesh.Storage.Redis/     # Redis storage provider
├── tests/
│   ├── OrbitMesh.Core.Tests/
│   ├── OrbitMesh.Server.Tests/
│   ├── OrbitMesh.Agent.Tests/
│   └── OrbitMesh.Integration.Tests/
├── samples/
│   ├── BasicServer/
│   ├── BasicAgent/
│   └── WorkflowDemo/
└── docs/
```

## Security Model

### TOFU Authentication (Trust On First Use)

OrbitMesh uses a certificate-based authentication system with admin approval workflow.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    INITIAL ENROLLMENT FLOW                          │
├─────────────────────────────────────────────────────────────────────┤
│   Admin              Server                     Node                │
│     │                  │                         │                  │
│     │ 1. Create Bootstrap Token                 │                  │
│     │ ─────────────────>│                        │                  │
│     │<─────────────────│ (one-time token)       │                  │
│     │                  │                         │                  │
│     │ 2. Provide to Node (out-of-band)          │                  │
│     │ ───────────────────────────────────────────>                  │
│     │                  │                         │                  │
│     │                  │ 3. Connect + Enroll    │                  │
│     │                  │<────────────────────────│                  │
│     │                  │                         │                  │
│     │ 4. Approve/Reject│                        │                  │
│     │ ─────────────────>│                        │                  │
│     │                  │ 5. Issue Certificate   │                  │
│     │                  │─────────────────────────>                  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    SUBSEQUENT CONNECTIONS                           │
├─────────────────────────────────────────────────────────────────────┤
│   Node                                          Server              │
│     │  1. Connect with Certificate               │                  │
│     │ ──────────────────────────────────────────>│                  │
│     │                                            │ 2. Verify cert   │
│     │                                            │    + challenge   │
│     │  3. Sign challenge response                │                  │
│     │ <──────────────────────────────────────────│                  │
│     │ ──────────────────────────────────────────>│                  │
│     │  4. Connection established                 │                  │
│     │ <──────────────────────────────────────────│                  │
└─────────────────────────────────────────────────────────────────────┘
```

### Security Components

| Component | Interface | Description |
|-----------|-----------|-------------|
| **Bootstrap Tokens** | `IBootstrapTokenService` | One-time enrollment tokens |
| **Enrollment** | `INodeEnrollmentService` | Admin approval workflow |
| **Credentials** | `INodeCredentialService` | Certificate issuance/validation |

### Security Layers

1. **Transport**: TLS 1.3 (HTTPS/WSS)
2. **Authentication**: Certificate-based (TOFU model)
3. **Authorization**: Capability-based (granted at approval)
4. **Network**: Challenge-response for connection verification

## Scalability

### Scale-out Architecture

```
                         ┌─────────────┐
                         │   Redis     │
                         │  Backplane  │
                         └──────┬──────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
┌───────────────┐       ┌───────────────┐       ┌───────────────┐
│ OrbitMesh     │       │ OrbitMesh     │       │ OrbitMesh     │
│ Server #1     │       │ Server #2     │       │ Server #3     │
└───────┬───────┘       └───────┬───────┘       └───────┬───────┘
        │                       │                       │
        └───────────────────────┼───────────────────────┘
                                │
                    ┌───────────┴───────────┐
                    │    Load Balancer      │
                    │  (Sticky Sessions)    │
                    └───────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
    Agent Pool              Agent Pool              Agent Pool
    (Region A)              (Region B)              (Region C)
```

### Performance Targets

| Metric | Target | Strategy |
|--------|--------|----------|
| Concurrent Connections | 100K+ | Redis Backplane, Kestrel tuning |
| Message Latency (P99) | < 100ms | MessagePack, Channel<T> |
| Throughput | 50K msg/sec | Parallel dispatch, Batching |
| Reconnection Storm | Mitigated | Decorrelated Jitter |

## Extension Points

OrbitMesh is designed to be extended, not modified. All core components are pluggable.

### Handler Registration

```csharp
// Agent-side: Register domain-specific handlers
agent.RegisterHandler<GpuInferenceHandler>("run-inference");
agent.RegisterHandler<MetricsHandler>("collect-metrics");
agent.RegisterHandler<DeployHandler>("deploy-app");

public class GpuInferenceHandler : IMeshHandler
{
    public async Task<object> HandleAsync(MeshCommand command, CancellationToken ct)
    {
        // Your domain logic here
        var model = command.GetParameter<Model>("model");
        return await RunInference(model, ct);
    }
}
```

### Middleware Pipeline

```csharp
services.AddOrbitMesh(options =>
{
    // Request/Response middleware
    options.UseMiddleware<LoggingMiddleware>();
    options.UseMiddleware<MetricsMiddleware>();
    options.UseMiddleware<ValidationMiddleware>();
    options.UseMiddleware<AuthorizationMiddleware>();
});

public class LoggingMiddleware : IMeshMiddleware
{
    public async Task InvokeAsync(MeshContext context, MeshDelegate next)
    {
        _logger.LogInformation("Command: {Command}", context.Command);
        await next(context);
        _logger.LogInformation("Result: {Status}", context.Result.Status);
    }
}
```

### Storage Providers

```csharp
// Built-in providers
options.UseStorage<SqliteJobStorage>();      // Default, embedded
options.UseStorage<SqlServerJobStorage>();   // Enterprise
options.UseStorage<RedisJobStorage>();       // High performance

// Custom provider
public class MongoDbJobStorage : IJobStorage
{
    public Task SaveJobAsync(Job job) { ... }
    public Task<Job> GetJobAsync(string jobId) { ... }
}
```

### Custom Serializers

```csharp
// Built-in serializers
options.UseSerializer<MessagePackSerializer>(); // Default, binary
options.UseSerializer<JsonSerializer>();        // Human-readable

// Custom serializer
public class ProtobufSerializer : IMeshSerializer
{
    public byte[] Serialize<T>(T obj) { ... }
    public T Deserialize<T>(byte[] data) { ... }
}
```

### Workflow Step Types

```csharp
// Create domain-specific workflow steps
public class DownloadModelStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(StepContext context)
    {
        var modelUrl = context.GetInput<string>("modelUrl");
        var localPath = await DownloadAsync(modelUrl);
        return StepResult.Success(new { localPath });
    }
}

public class RollbackModelStep : ICompensationStep
{
    public async Task CompensateAsync(StepContext context)
    {
        var localPath = context.GetInput<string>("localPath");
        File.Delete(localPath);
    }
}
```

### Event Hooks

```csharp
services.AddOrbitMesh(options =>
{
    options.OnAgentConnected += (sender, agent) =>
        _logger.LogInformation("Agent {Id} connected", agent.Id);

    options.OnAgentDisconnected += (sender, agent) =>
        _logger.LogWarning("Agent {Id} disconnected", agent.Id);

    options.OnJobCompleted += (sender, job) =>
        _metrics.RecordJobDuration(job.Duration);

    options.OnJobFailed += (sender, job) =>
        _alerting.SendAlert(job.Error);
});
```

### Capability-based Agent Selection

```csharp
// Define custom capabilities
public static class CustomCapabilities
{
    public static readonly AgentCapability Gpu = new("gpu");
    public static readonly AgentCapability HighMemory = new("high-memory");
    public static readonly AgentCapability Nvme = new("nvme-storage");
}

// Agent declares capabilities
var agent = new MeshAgentBuilder()
    .WithCapabilities(CustomCapabilities.Gpu, CustomCapabilities.HighMemory)
    .Build();

// Server selects agents by capability
var gpuAgents = await mesh.GetAgentsByCapability(CustomCapabilities.Gpu);
var bestAgent = gpuAgents.OrderBy(a => a.Load).First();
```

## API Abstraction Levels

```
┌─────────────────────────────────────────────────────────────────────┐
│ HIGH LEVEL - Workflow API                                           │
│                                                                     │
│   await mesh.ExecuteWorkflowAsync("deploy-app", parameters);        │
│   await mesh.ExecuteRollingAsync(workflow, batchPercent: 20);       │
├─────────────────────────────────────────────────────────────────────┤
│ MID LEVEL - Execution API                                           │
│                                                                     │
│   await mesh.ExecuteAsync(agent, "restart-service", params);        │
│   await mesh.StreamAsync<T>(agent, "run-inference", params);        │
│   await mesh.ExecuteOnAllAsync("collect-metrics");                  │
├─────────────────────────────────────────────────────────────────────┤
│ LOW LEVEL - Communication API                                       │
│                                                                     │
│   var result = await mesh.InvokeAsync<T>(agent, "method", args);    │
│   await mesh.SendAsync(agent, message);                             │
│   await mesh.SendRawAsync(agent, bytes);                            │
└─────────────────────────────────────────────────────────────────────┘
```

Choose the abstraction level that fits your use case:
- **Workflow API**: Complex multi-step processes with rollback
- **Execution API**: Domain commands with structured patterns
- **Communication API**: Direct messaging for custom protocols
