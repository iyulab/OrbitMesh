# OrbitMesh Implementation Phases

## Phase Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PHASE 1: Foundation (Core & Transport)                    [COMPLETED ✅]   │
│  ► Core models, SignalR Hub, MessagePack, Basic Agent connection           │
├─────────────────────────────────────────────────────────────────────────────┤
│  PHASE 2: Execution Engine (Dispatcher & Reliability)      [COMPLETED ✅]   │
│  ► Channel-based dispatcher, ACK/NACK protocol, Execution patterns         │
├─────────────────────────────────────────────────────────────────────────────┤
│  PHASE 3: Persistence & State (SQLite Storage)             [COMPLETED ✅]   │
│  ► SQLite storage, Event sourcing, State machines, Domain events           │
├─────────────────────────────────────────────────────────────────────────────┤
│  PHASE 4: Workflow Engine (Orchestration)                  [COMPLETED ✅]   │
│  ► Workflow definition, Step executors, Expression evaluation, YAML/JSON   │
├─────────────────────────────────────────────────────────────────────────────┤
│  PHASE 5: Server-Workflow Integration                      [COMPLETED ✅]   │
│  ► Trigger system, Server adapters, E2E integration tests                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  PHASE 6: Advanced Features (Optional)                     [PLANNED]        │
│  ► JWT/mTLS security, Redis backplane, Dashboard, Additional providers     │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Current Test Coverage

| Package | Tests | Status |
|---------|-------|--------|
| OrbitMesh.Core.Tests | 6 | ✅ Passing |
| OrbitMesh.Client.Tests | 24 | ✅ Passing |
| OrbitMesh.Agent.Tests | 24 | ✅ Passing |
| OrbitMesh.Server.Tests | 226 | ✅ Passing |
| OrbitMesh.Workflows.Tests | 77 | ✅ Passing |
| OrbitMesh.Integration.Tests | 24 | ✅ Passing |
| **Total** | **381** | ✅ **All Passing** |

---

## Phase 1: Foundation (Core & Transport)

### Objective
Establish the fundamental building blocks: core models, communication layer, and basic connectivity.

### Tasks

#### 1.1 Project Structure Setup
- [ ] Create solution structure with all package projects
- [ ] Configure Directory.Build.props for shared settings
- [ ] Setup .editorconfig and code style rules
- [ ] Configure GitHub Actions for CI/CD
- [ ] Setup test projects with xUnit

#### 1.2 Core Models (OrbitMesh.Core)
- [ ] Define `AgentInfo` model (Id, Name, Tags, Capabilities, Status)
- [ ] Define `JobRequest` / `JobResult` models
- [ ] Define `JobStatus` enum (Pending, Assigned, Running, Completed, Failed)
- [ ] Define `AgentStatus` enum (Created, Initializing, Ready, Running, Paused, Stopped, Faulted)
- [ ] Create `IAgentClient` interface (strongly-typed SignalR client)
- [ ] Create `IServerHub` interface (server-side methods)
- [ ] Define execution pattern contracts (`IFireAndForget`, `IRequestResponse`, `IStreaming`)

#### 1.3 SignalR Hub Implementation (OrbitMesh.Server)
- [ ] Implement `AgentHub : Hub<IAgentClient>`
- [ ] Implement `OnConnectedAsync` with agent registration
- [ ] Implement `OnDisconnectedAsync` with cleanup
- [ ] Add `Context.Items` for connection metadata
- [ ] Implement Groups management (AllAgents, ByCapability)
- [ ] Add heartbeat mechanism

#### 1.4 MessagePack Integration
- [ ] Add MessagePack protocol to SignalR
- [ ] Define MessagePack formatters for custom types
- [ ] Configure compression options
- [ ] Benchmark JSON vs MessagePack performance

#### 1.5 Agent Connection (OrbitMesh.Agent)
- [ ] Implement `MeshAgentBuilder` with fluent configuration
- [ ] Implement `SignalRAgentConnection`
- [ ] Add automatic reconnection with exponential backoff + jitter
- [ ] Implement `Reconnected` event handler for state sync
- [ ] Add heartbeat response handling

#### 1.6 DI Extensions
- [ ] Create `AddOrbitMesh()` server extension
- [ ] Create `AddOrbitMeshAgent()` agent extension
- [ ] Implement `OrbitMeshOptions` with validation
- [ ] Implement `AgentOptions` with validation

### Deliverables
- ✅ Working SignalR connection between server and agent
- ✅ MessagePack serialization enabled
- ✅ Auto-reconnection with jitter
- ✅ Basic heartbeat mechanism
- ✅ Unit tests for core models

### Success Criteria
```csharp
// Server can accept agent connections
var builder = WebApplication.CreateBuilder();
builder.Services.AddOrbitMesh();
var app = builder.Build();
app.MapHub<AgentHub>("/mesh");

// Agent can connect and stay connected
var agent = new MeshAgentBuilder()
    .WithServer("https://localhost:5000/mesh")
    .WithToken("test-token")
    .Build();
await agent.ConnectAsync();
// Connection persists through network interruptions
```

---

## Phase 2: Execution Engine (Dispatcher & Reliability)

### Objective
Build the push-based task distribution system with reliable message delivery.

### Tasks

#### 2.1 Channel-based Dispatcher
- [ ] Implement `WorkItemChannel` using `Channel<WorkItem>`
- [ ] Configure bounded channel with backpressure
- [ ] Implement `WorkItemProcessor` as `BackgroundService`
- [ ] Add concurrent processing with configurable parallelism
- [ ] Implement priority queuing

#### 2.2 ACK/NACK Protocol
- [ ] Define `JobAssignment` message with IdempotencyKey
- [ ] Implement `PendingAck` state tracking with timeout
- [ ] Add `JobAccepted` ACK handling
- [ ] Implement timeout-based reassignment
- [ ] Add duplicate detection using IdempotencyKey

#### 2.3 Idempotency Service
- [ ] Implement `IdempotencyService` with distributed cache
- [ ] Add `ExecuteOnceAsync<T>` method
- [ ] Configure TTL for idempotency keys
- [ ] Support both in-memory and Redis cache

#### 2.4 Execution Patterns Implementation
- [ ] **Fire & Forget**: One-way message, no response tracking
- [ ] **Request-Response**: `InvokeAsync` with timeout
- [ ] **Streaming**: `IAsyncEnumerable` server-to-agent and agent-to-server
- [ ] **Long-Running Job**: Progress reporting + final result
- [ ] Add cancellation token propagation for all patterns

#### 2.5 Agent Task Runner
- [ ] Implement `ITaskHandler` interface
- [ ] Create `TaskHandlerRegistry` for handler discovery
- [ ] Implement task execution with timeout
- [ ] Add structured result reporting
- [ ] Implement graceful cancellation

#### 2.6 Client Results Pattern
- [ ] Enable bidirectional RPC (server calls agent method and waits)
- [ ] Implement `Clients.Client(id).InvokeAsync<T>()` usage
- [ ] Add timeout handling for client invocations

### Deliverables
- ✅ Push-based job distribution
- ✅ Reliable ACK/NACK protocol
- ✅ All 5 execution patterns working
- ✅ Streaming support for LLM-style workloads
- ✅ Cancellation propagation

### Success Criteria
```csharp
// Server pushes job to specific agent
await _dispatcher.EnqueueAsync(new WorkItem {
    AgentId = "node-a",
    Job = jobRequest
});

// Agent receives, ACKs, executes, and streams result
agent.On<JobRequest>("ExecuteJob", async (job, ct) => {
    await ReportAccepted(job.Id);
    await foreach (var chunk in ProcessAsync(job, ct))
        yield return chunk;
});
```

---

## Phase 3: Persistence & State (Event Sourcing)

### Objective
Implement durable state management with event sourcing for fault tolerance.

### Tasks

#### 3.1 Storage Provider Abstraction
- [ ] Define `IOrbitMeshStorage` interface
- [ ] Define `IEventStore` interface
- [ ] Define `IAgentStore` interface
- [ ] Define `IJobStore` interface
- [ ] Create storage provider registration pattern

#### 3.2 SQLite Storage Provider
- [ ] Implement `SqliteOrbitMeshStorage`
- [ ] Configure WAL mode for better concurrency
- [ ] Design schema for events, agents, jobs
- [ ] Implement migrations system
- [ ] Add connection pooling

#### 3.3 Event Sourcing Implementation
- [ ] Define base `DomainEvent` class with timestamp, sequence
- [ ] Implement `EventStream` for aggregate events
- [ ] Implement `EventStore.AppendAsync()` with optimistic concurrency
- [ ] Implement `EventStore.LoadAsync()` for stream replay
- [ ] Add snapshot support for large event streams

#### 3.4 State Machine Implementation (Stateless)
- [ ] Implement `AgentStateMachine` using Stateless library
- [ ] Define all states and triggers from architecture
- [ ] Add async entry/exit actions
- [ ] Implement `JobStateMachine`
- [ ] Add state persistence hooks

#### 3.5 Twin Pattern for Agent State
- [ ] Define `AgentTwin` with Desired/Reported properties
- [ ] Implement server-side desired state management
- [ ] Implement agent-side reported state sync
- [ ] Add version-based conflict resolution
- [ ] Implement eventual consistency sync loop

#### 3.6 Checkpoint/Replay Mechanism
- [ ] Implement periodic checkpoint creation
- [ ] Store checkpoint with associated event sequence
- [ ] Implement state reconstruction from checkpoint + events
- [ ] Add recovery on server restart
- [ ] Test replay determinism

### Deliverables
- ✅ SQLite-based persistent storage
- ✅ Event sourcing with replay capability
- ✅ State machines for Agent and Job lifecycle
- ✅ Twin pattern for agent-server sync
- ✅ Server restart recovery

### Success Criteria
```csharp
// Events are persisted
await _eventStore.AppendAsync(streamId, new JobStartedEvent { ... });

// State can be reconstructed after restart
var events = await _eventStore.LoadAsync(streamId);
var state = events.Aggregate(new JobState(), (s, e) => s.Apply(e));

// Agent twin syncs state
await agent.Twin.ReportAsync(new { CpuUsage = 45, MemoryFree = 2048 });
```

---

## Phase 4: Workflow Engine (Orchestration)

### Objective
Build the declarative workflow system with DAG execution and compensation.

### Tasks

#### 4.1 Workflow Definition (Fluent Builder)
- [ ] Implement `WorkflowBuilder` class
- [ ] Add `AddStep<TStep>()` method
- [ ] Add `WithRetry(attempts, delay)` configuration
- [ ] Add `WithTimeout(duration)` configuration
- [ ] Add `WithCompensation<TCompensation>()` for saga
- [ ] Add `OnAgent(capability)` for targeting

#### 4.2 DAG Construction
- [ ] Implement `WorkflowGraph` as DAG structure
- [ ] Add `Then()` for sequential steps
- [ ] Add `Parallel()` for concurrent branches
- [ ] Add `WaitAll()` for fan-in
- [ ] Implement topological sort for execution order
- [ ] Add cycle detection

#### 4.3 Workflow Step Abstraction
- [ ] Define `IWorkflowStep` interface
- [ ] Define `ICompensationStep` interface
- [ ] Create `StepContext` with input/output
- [ ] Add step result types (Success, Failed, Skipped)
- [ ] Implement step dependency injection

#### 4.4 Workflow Execution Runtime
- [ ] Implement `WorkflowEngine`
- [ ] Add step execution with state tracking
- [ ] Implement parallel step execution
- [ ] Add progress tracking and events
- [ ] Implement workflow pause/resume

#### 4.5 Saga Pattern (Compensation)
- [ ] Track executed steps for rollback
- [ ] Implement compensation chain execution (reverse order)
- [ ] Add partial failure handling
- [ ] Implement compensation timeout
- [ ] Add compensation event logging

#### 4.6 Workflow Persistence
- [ ] Store workflow definition
- [ ] Store workflow instance state
- [ ] Implement workflow replay on failure
- [ ] Add workflow versioning support

### Deliverables
- ✅ Fluent workflow builder API
- ✅ DAG-based step execution
- ✅ Parallel and sequential execution
- ✅ Saga compensation on failure
- ✅ Workflow persistence and replay

### Success Criteria
```csharp
var workflow = new WorkflowBuilder("deploy-app")
    .AddStep<ValidateStep>()
    .AddStep<BuildStep>()
        .WithRetry(3, TimeSpan.FromSeconds(10))
    .AddParallel(
        branch => branch.AddStep<DeployToNodeA>(),
        branch => branch.AddStep<DeployToNodeB>()
    )
    .AddStep<HealthCheckStep>()
        .WithCompensation<RollbackStep>()
    .Build();

await _workflowEngine.ExecuteAsync(workflow, context);
```

---

## Phase 5: Production Readiness (Security & Scale)

### Objective
Add enterprise-grade security, resilience, and scalability features.

### Tasks

#### 5.1 JWT Authentication
- [ ] Implement `TokenService` with access/refresh tokens
- [ ] Configure JWT bearer authentication
- [ ] Handle SignalR query string token
- [ ] Implement refresh token rotation
- [ ] Add token revocation support

#### 5.2 mTLS Support (Optional)
- [ ] Configure Kestrel for client certificates
- [ ] Implement certificate validation
- [ ] Add certificate rotation support

#### 5.3 Polly Resilience
- [ ] Implement `ResiliencePipeline` for agent communication
- [ ] Add retry with exponential backoff + jitter
- [ ] Add circuit breaker for failing agents
- [ ] Add timeout policies
- [ ] Create resilience metrics

#### 5.4 Redis Backplane
- [ ] Add SignalR Redis backplane support
- [ ] Configure Redis connection
- [ ] Test multi-server scenarios
- [ ] Implement Redis-based distributed cache

#### 5.5 OpenTelemetry Integration
- [ ] Add tracing instrumentation
- [ ] Create custom `ActivitySource` for OrbitMesh operations
- [ ] Add metrics (jobs processed, duration, connected agents)
- [ ] Configure OTLP exporter
- [ ] Add Prometheus metrics endpoint

#### 5.6 Performance Tuning
- [ ] Tune Kestrel settings for high connections
- [ ] Configure thread pool settings
- [ ] Implement connection limiting
- [ ] Add Thundering Herd mitigation (decorrelated jitter)
- [ ] Load test with k6 (target: 10K+ connections)

### Deliverables
- ✅ JWT-based authentication
- ✅ Polly resilience patterns
- ✅ Redis backplane for scale-out
- ✅ Full observability with OTel
- ✅ Load tested to 10K+ connections

### Success Criteria
```csharp
// Secure connection
var agent = new MeshAgentBuilder()
    .WithServer("https://mesh.example.com/hub")
    .WithToken(await GetAccessTokenAsync())
    .WithClientCertificate(cert) // Optional mTLS
    .Build();

// Resilient operations
await _resilience.ExecuteAsync(async ct =>
    await SendToAgentAsync(agentId, job, ct));

// Metrics exposed
// GET /metrics -> orbitmesh_connected_agents, orbitmesh_jobs_total
```

---

## Phase 6: Advanced Features (Optional)

### Objective
Add advanced capabilities for specific use cases.

### Tasks

#### 6.1 WASM Sandbox (Security Isolation)
- [ ] Integrate Wasmtime.NET
- [ ] Implement WASM module loader
- [ ] Define host functions (I/O, Network boundaries)
- [ ] Add resource limits (memory, CPU time)
- [ ] Create .NET to WASM compilation pipeline

#### 6.2 Admin Dashboard
- [ ] Create Blazor WebAssembly dashboard
- [ ] Real-time agent status display
- [ ] Job monitoring and management
- [ ] Workflow visualization (DAG view)
- [ ] System metrics and logs

#### 6.3 Additional Storage Providers
- [ ] SQL Server provider
- [ ] PostgreSQL provider
- [ ] MongoDB provider
- [ ] Azure Table Storage provider

#### 6.4 Advanced Scheduling
- [ ] Cron-based scheduled workflows
- [ ] Agent capability matching algorithm
- [ ] Load balancing strategies
- [ ] Priority-based scheduling

#### 6.5 CLI Tool
- [ ] Create `orbitmesh` CLI
- [ ] Add server management commands
- [ ] Add agent management commands
- [ ] Add workflow deployment commands

### Deliverables
- ✅ WASM-based secure code execution
- ✅ Web-based admin dashboard
- ✅ Multiple storage provider options
- ✅ Advanced scheduling features
- ✅ CLI management tool

---

## Task Summary by Package

### OrbitMesh.Core
| Phase | Tasks |
|-------|-------|
| 1 | Models, Interfaces, Contracts |
| 2 | Execution pattern interfaces |
| 3 | Event base classes, Storage interfaces |
| 4 | Workflow abstractions |

### OrbitMesh.Server
| Phase | Tasks |
|-------|-------|
| 1 | SignalR Hub, DI extensions |
| 2 | Dispatcher, ACK protocol |
| 3 | State machines, Twin management |
| 4 | Workflow engine |
| 5 | Auth, Resilience, Telemetry |

### OrbitMesh.Agent
| Phase | Tasks |
|-------|-------|
| 1 | Connection, Reconnection |
| 2 | Task runner, Execution patterns |
| 3 | State twin, Local persistence |
| 5 | mTLS, Client resilience |

### OrbitMesh.Workflows
| Phase | Tasks |
|-------|-------|
| 4 | Builder, DAG, Saga, Runtime |

### OrbitMesh.Storage.*
| Phase | Tasks |
|-------|-------|
| 3 | SQLite provider |
| 5 | Redis provider |
| 6 | SQL Server, PostgreSQL providers |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| SignalR scalability limits | Redis backplane, Azure SignalR Service option |
| Event store growth | Snapshots, Event compaction |
| Message loss | ACK/NACK protocol, Idempotency |
| Reconnection storms | Decorrelated jitter algorithm |
| State sync conflicts | Version-based optimistic concurrency |
| Long workflow failures | Checkpoints, Compensation saga |

## Dependencies

```
Phase 1 ─────► Phase 2 ─────► Phase 3 ─────► Phase 4
                                    │              │
                                    └──────────────┼────► Phase 5
                                                   │
                                                   └────► Phase 6
```

- Phase 2 depends on Phase 1 (transport layer)
- Phase 3 depends on Phase 2 (needs dispatcher for state events)
- Phase 4 depends on Phase 3 (needs persistence for workflows)
- Phase 5 can start partially in parallel with Phase 4
- Phase 6 can start after Phase 4
