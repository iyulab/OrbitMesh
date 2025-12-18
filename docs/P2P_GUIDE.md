# OrbitMesh P2P NAT Traversal Guide

## Overview

OrbitMesh supports direct peer-to-peer (P2P) connections between agents using NAT traversal techniques. This enables high-performance, low-latency communication without routing through the central server.

```
Without P2P                          With P2P
─────────────                        ─────────────────────────────

┌─────────┐                          ┌─────────┐
│ Agent A │ ──┐                      │ Agent A │ ─────┐
└─────────┘   │                      └─────────┘      │
              ▼                                       ▼
         ┌────────┐                              ┌────────┐
         │ Server │                              │ Server │ (signaling only)
         └────────┘                              └────────┘
              ▲                                       ▲
┌─────────┐   │                      ┌─────────┐      │
│ Agent B │ ──┘                      │ Agent B │ ─────┘
└─────────┘                          └─────────┘
                                          │
All data via server                       └────── Direct P2P ──────┘
```

## Key Benefits

| Benefit | Description |
|---------|-------------|
| **Lower Latency** | Direct connection eliminates server hop |
| **Higher Throughput** | No server bandwidth bottleneck |
| **Reduced Server Load** | Server only handles signaling |
| **Better Scalability** | P2P offloads data transfer |

## NAT Types and Connectivity

### NAT Type Matrix

| NAT Type | Description | P2P Success Rate |
|----------|-------------|------------------|
| **Open** | No NAT, direct public IP | 100% |
| **Full Cone** | Any external host can send packets | 95%+ |
| **Restricted** | Only hosts you contacted can reply | 85%+ |
| **Port Restricted** | Specific port required from contacted host | 70%+ |
| **Symmetric** | Different mapping per destination | Requires TURN relay |

### Connection Strategy Selection

```csharp
// Strategy is automatically determined based on NAT types
var strategy = (initiatorNat, responderNat) switch
{
    (Symmetric, _) or (_, Symmetric) => TurnRelay,      // TURN required
    (Open, _) or (_, Open)           => DirectConnect,  // Easy direct
    (FullCone, _) or (_, FullCone)   => DirectConnect,  // Easy direct
    _                                => UdpHolePunch    // Standard hole punch
};
```

## Quick Start

### 1. Enable P2P on Agent

```csharp
using OrbitMesh.Node;
using OrbitMesh.Node.Extensions;

var agent = new MeshAgentBuilder()
    .WithServer("https://your-server.com")
    .WithAgentId("my-agent")
    .WithP2P(options =>
    {
        options.PreferP2P = true;           // Prefer P2P over relay
        options.FallbackToRelay = true;     // Fallback if P2P fails
        options.StunServer = null;          // Use server's embedded STUN
        options.TurnServer = "turn.your-server.com"; // TURN for Symmetric NAT
        options.TurnUsername = "turnuser";
        options.TurnPassword = "turnpass";
    })
    .Build();

await agent.ConnectAsync();
```

### 2. Enable P2P on Server

```csharp
// In Program.cs or Startup.cs
services.AddOrbitMeshP2P(options =>
{
    options.StunPort = 3478;           // Embedded STUN server port
    options.EnableTurnRelay = true;     // Enable TURN for Symmetric NAT
});
```

### 3. Connect to Peer

```csharp
// Initiate P2P connection to another agent
var connectionState = await agent.ConnectToPeerAsync("target-agent-id");

if (connectionState.Status == PeerConnectionStatus.Connected)
{
    Console.WriteLine($"P2P connected via {connectionState.ActiveTransport}");
    Console.WriteLine($"Local: {connectionState.LocalCandidate}");
    Console.WriteLine($"Remote: {connectionState.RemoteCandidate}");
}
```

### 4. Send Data Over P2P

```csharp
// Data is automatically routed via P2P if connected
await agent.SendToPeerAsync("target-agent-id", myData);
```

## Configuration Options

### P2POptions (Agent-side)

```csharp
public class P2POptions
{
    // Connection behavior
    public bool PreferP2P { get; set; } = true;
    public bool FallbackToRelay { get; set; } = true;

    // STUN configuration
    public string? StunServer { get; set; }  // null = use server's STUN
    public int StunPort { get; set; } = 3478;

    // TURN configuration (for Symmetric NAT)
    public string? TurnServer { get; set; }
    public int TurnPort { get; set; } = 3478;
    public string? TurnUsername { get; set; }
    public string? TurnPassword { get; set; }

    // Timeouts
    public int ConnectionTimeoutMs { get; set; } = 10000;
    public int NatPunchRetries { get; set; } = 10;
    public int NatPunchIntervalMs { get; set; } = 100;

    // Transport settings
    public int KeepAliveIntervalMs { get; set; } = 15000;
    public int DisconnectTimeoutMs { get; set; } = 30000;
    public int MaxMtu { get; set; } = 1400;

    // Delivery modes
    public bool EnableUnreliableDelivery { get; set; } = true;
    public bool EnableReliableDelivery { get; set; } = true;
}
```

### P2PServerOptions (Server-side)

```csharp
public class P2PServerOptions
{
    public int StunPort { get; set; } = 3478;
    public bool EnableTurnRelay { get; set; } = false;
    public string? TurnRealm { get; set; }
}
```

## ICE Candidate Types

### Host Candidates
Local network addresses discovered from network interfaces.

```csharp
var hostCandidate = IceCandidate.CreateHost("192.168.1.100", 5000);
// Priority: Highest (126 type preference)
```

### Server Reflexive Candidates
Public addresses discovered via STUN server.

```csharp
var srflxCandidate = IceCandidate.CreateServerReflexive(
    publicAddress: "203.0.113.50",
    publicPort: 54321,
    localAddress: "192.168.1.100",
    localPort: 5000);
// Priority: Medium (100 type preference)
```

### Relayed Candidates
Addresses allocated from TURN server for Symmetric NAT scenarios.

```csharp
var relayCandidate = IceCandidate.CreateRelayed(
    relayedAddress: "turn.example.com",
    relayedPort: 49152,
    relayServer: "turn.example.com");
// Priority: Lowest (0 type preference) - used as fallback
```

## Connection Flow

```
Agent A                    Server                     Agent B
   │                         │                           │
   │  1. Register + Get NAT Info                         │
   ├────────────────────────►│                           │
   │         NAT Type: FullCone                          │
   │◄────────────────────────┤                           │
   │                         │                           │
   │  2. Request P2P Connection                          │
   ├────────────────────────►│                           │
   │                         │  3. Forward Request       │
   │                         ├──────────────────────────►│
   │                         │                           │
   │                         │  4. Accept + Candidates   │
   │                         │◄──────────────────────────┤
   │  5. Response + Candidates                           │
   │◄────────────────────────┤                           │
   │                         │                           │
   │  ═══════════════════════════════════════════════════│
   │       6. Direct P2P Connection (UDP Hole Punch)     │
   │◄═══════════════════════════════════════════════════►│
   │                         │                           │
```

## Symmetric NAT Handling (TURN Relay)

When either agent is behind Symmetric NAT, direct P2P is not possible. OrbitMesh automatically falls back to TURN relay:

```
Agent A                  TURN Server                 Agent B
(Symmetric NAT)             │                    (Any NAT)
   │                        │                        │
   │  1. Allocate Relay     │                        │
   ├───────────────────────►│                        │
   │  Relay: 5.6.7.8:49152  │                        │
   │◄───────────────────────┤                        │
   │                        │                        │
   │  2. Create Permission  │                        │
   ├───────────────────────►│                        │
   │                        │                        │
   │  3. Data to B via Relay│                        │
   ├───────────────────────►│────────────────────────►
   │                        │                        │
   │                        │  4. Data from B        │
   │◄───────────────────────│◄────────────────────────
   │                        │                        │
```

## Monitoring and Diagnostics

### Connection State

```csharp
var state = agent.GetPeerConnectionState("peer-id");

Console.WriteLine($"Status: {state.Status}");           // Connected, Failed, etc.
Console.WriteLine($"Transport: {state.ActiveTransport}"); // P2P, SignalR, Relay
Console.WriteLine($"Strategy: {state.Strategy}");        // DirectConnect, UdpHolePunch, etc.
Console.WriteLine($"Local NAT: {state.LocalNatInfo?.Type}");
Console.WriteLine($"Remote NAT: {state.RemoteNatInfo?.Type}");
```

### Connection Metrics

```csharp
var metrics = state.Metrics;
Console.WriteLine($"RTT: {metrics?.RoundTripTime.TotalMilliseconds}ms");
Console.WriteLine($"Bytes Sent: {metrics?.BytesSent}");
Console.WriteLine($"Bytes Received: {metrics?.BytesReceived}");
Console.WriteLine($"Packet Loss: {metrics?.PacketLossRate:P2}");
```

## Best Practices

### 1. Always Enable Fallback

```csharp
options.FallbackToRelay = true;  // Ensures connectivity in all scenarios
```

### 2. Configure TURN for Enterprise

```csharp
// Enterprise networks often use Symmetric NAT
options.TurnServer = "turn.company.com";
options.TurnUsername = Environment.GetEnvironmentVariable("TURN_USER");
options.TurnPassword = Environment.GetEnvironmentVariable("TURN_PASS");
```

### 3. Use Appropriate MTU

```csharp
// Default 1400 avoids fragmentation in most networks
options.MaxMtu = 1400;
```

### 4. Handle Connection Events

```csharp
agent.PeerConnected += (sender, e) =>
{
    Console.WriteLine($"P2P connected to {e.PeerId}");
};

agent.PeerDisconnected += (sender, e) =>
{
    Console.WriteLine($"P2P disconnected from {e.PeerId}: {e.Reason}");
};
```

## Troubleshooting

### P2P Connection Fails

1. **Check NAT Types**: Both agents behind Symmetric NAT require TURN
2. **Verify STUN/TURN**: Ensure servers are reachable
3. **Firewall Rules**: UDP traffic must be allowed
4. **Timeout Settings**: Increase `ConnectionTimeoutMs` for slow networks

### High Latency

1. **Check Route**: Verify P2P is active (`ActiveTransport == P2P`)
2. **MTU Issues**: Lower MTU if fragmentation occurs
3. **Network Conditions**: Use simulation to test

### Connection Drops

1. **Keep-Alive**: Ensure `KeepAliveIntervalMs` is appropriate
2. **NAT Timeout**: Some NATs have short UDP mapping timeouts
3. **Mobile Networks**: May require more aggressive keep-alive

## Performance Characteristics

| Metric | P2P Direct | Server Relay | TURN Relay |
|--------|------------|--------------|------------|
| Latency | Lowest | Medium | Low-Medium |
| Throughput | Highest | Limited by server | Limited by TURN |
| Server Load | None | High | TURN server load |
| Reliability | Good | Best | Good |
| NAT Compatibility | ~82% | 100% | 100% |

## Related Documentation

- [Architecture Overview](ARCHITECTURE.md)
- [Core Concepts](CONCEPTS.md)
- [Implementation Phases](IMPLEMENTATION_PHASES.md)
