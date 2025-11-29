# OrbitMesh Core Concepts

## Identity: Foundation Layer

OrbitMesh is **NOT** an end-user application. It's a **Foundation Layer** for building distributed systems.

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
├─────────────────────────────────────────────────────────────────┤
│                     Infrastructure                               │
│            (Network, Servers, Agents, Storage)                   │
└─────────────────────────────────────────────────────────────────┘
```

## What OrbitMesh IS

| Role | Description |
|------|-------------|
| **Infrastructure SDK** | Building blocks for distributed systems |
| **Communication Framework** | Bidirectional real-time server-agent channel |
| **Execution Platform** | Abstraction for various execution patterns |
| **Workflow Engine** | Multi-step orchestration capabilities |
| **Extensible Foundation** | Plugin architecture for domain extensions |

## What OrbitMesh IS NOT

| Not This | Explanation |
|----------|-------------|
| ~~End-user Application~~ | Not a product users interact with directly |
| ~~Complete Solution~~ | Doesn't solve domain problems out-of-box |
| ~~Opinionated Framework~~ | Doesn't enforce specific usage patterns |

---

## Analogy: ASP.NET Core for Distributed Systems

Just as ASP.NET Core is the foundation for web applications, OrbitMesh is the foundation for distributed applications.

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

---

## Design Principles

### 1. Zero Domain Knowledge

OrbitMesh core **knows nothing about**:
- What GPU inference is
- What deployment means
- What data collection involves
- What ML training is

OrbitMesh core **knows how to**:
- Connect agents to servers
- Send and receive messages
- Execute and track jobs
- Orchestrate workflows

### 2. Minimal Opinions, Maximum Flexibility

OrbitMesh provides **"how"**, your application defines **"what"**.

```csharp
// OrbitMesh: provides "how" to execute
await mesh.ExecuteAsync(agent, command, parameters);

// Your App: defines "what" to execute
public class GpuInferenceHandler : IMeshHandler
{
    public async Task<object> HandleAsync(MeshCommand command)
    {
        // Your domain logic - GPU inference
    }
}
```

### 3. Layered Abstraction

Use the level that fits your needs:

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

Every component can be replaced:

```csharp
services.AddOrbitMesh(options =>
{
    // Storage - swap implementations
    options.UseStorage<RedisJobStorage>();

    // Serializer - swap implementations
    options.UseSerializer<MessagePackSerializer>();

    // Custom middleware
    options.UseMiddleware<LoggingMiddleware>();
    options.UseMiddleware<MetricsMiddleware>();
});
```

---

## Responsibility Separation

| OrbitMesh Provides | Your Application Adds |
|--------------------|----------------------|
| Agent connection & auth | Domain-specific agent capabilities |
| Message routing | Business logic |
| Execution patterns | Custom handlers |
| Workflow engine | Domain workflows |
| State management | Domain state |
| Base monitoring | Domain metrics |

---

## Example Applications

### 1. GPU Cluster Manager (like GPUStack)

Manage distributed GPU nodes and distribute ML tasks.

```csharp
public class GpuClusterManager
{
    private readonly IMeshClient _mesh;

    public async Task<InferenceResult> RunInference(Model model, Input input)
    {
        // Select GPU agent with lowest utilization
        var agent = await _mesh.SelectAgent(
            capability: AgentCapability.Gpu,
            selector: agents => agents.OrderBy(a => a.GpuUtilization).First());

        // Execute inference with streaming
        return await _mesh.StreamAsync<InferenceResult>(
            agent, "run-inference", new { model, input });
    }
}
```

### 2. Data Collection System

Collect data from distributed sources to central aggregation.

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

        await _mesh.ExecuteOnAllAsync(workflow);
    }
}
```

### 3. App Deployment System

Deploy and update applications across distributed servers.

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

        await _mesh.ExecuteRollingAsync(workflow, batchPercent: 20);
    }
}
```

### 4. Edge Device Controller

Monitor and control edge devices remotely.

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

### 5. Distributed Task Scheduler

Schedule and execute tasks across distributed workers.

```csharp
public class DistributedScheduler
{
    private readonly IMeshClient _mesh;

    public async Task ScheduleRecurring(string cronExpr, IWorkflow workflow)
    {
        _mesh.Schedule(cronExpr, async () =>
        {
            var agent = await _mesh.SelectLeastLoadedAgent();
            await _mesh.ExecuteAsync(agent, workflow);
        });
    }
}
```

---

## Package Structure

```
OrbitMesh (Foundation)
├── OrbitMesh.Core              # Core abstractions, interfaces
├── OrbitMesh.Server            # Server implementation
├── OrbitMesh.Agent             # Agent implementation
├── OrbitMesh.Workflows         # Workflow engine
├── OrbitMesh.Storage.Sqlite    # Storage implementation
├── OrbitMesh.Storage.Redis     # Storage implementation
└── OrbitMesh.Metrics           # Metrics/monitoring

Applications (Built on OrbitMesh)
├── YourCompany.GpuCluster      # GPU cluster management app
├── YourCompany.DataCollector   # Data collection app
├── YourCompany.AppDeployer     # App deployment app
└── YourCompany.EdgeManager     # Edge device management app
```

---

## Comparison with Similar Foundations

| Foundation | Domain | OrbitMesh Equivalent |
|------------|--------|---------------------|
| ASP.NET Core | Web Applications | Distributed Applications |
| Entity Framework | Database Access | Agent Communication |
| MediatR | In-process Messaging | Distributed Messaging |
| SignalR | Real-time Web | (Used internally) |
| Orleans | Virtual Actors | Agent-based Execution |

---

## Summary

**OrbitMesh = ASP.NET Core for Distributed Systems**

- Doesn't solve problems directly
- Provides **tools** to solve problems
- Knows nothing about domains
- Enables **rapid development** of domain applications

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
