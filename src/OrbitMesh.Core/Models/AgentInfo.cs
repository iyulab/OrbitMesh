using MessagePack;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents information about an agent in the mesh.
/// </summary>
[MessagePackObject]
public sealed record AgentInfo
{
    /// <summary>
    /// Unique identifier for the agent.
    /// </summary>
    [Key(0)]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name for the agent.
    /// </summary>
    [Key(1)]
    public required string Name { get; init; }

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    [Key(2)]
    public AgentStatus Status { get; init; } = AgentStatus.Created;

    /// <summary>
    /// Tags for grouping and filtering agents.
    /// </summary>
    [Key(3)]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Capabilities declared by the agent.
    /// </summary>
    [Key(4)]
    public IReadOnlyList<AgentCapability> Capabilities { get; init; } = [];

    /// <summary>
    /// Optional group the agent belongs to.
    /// </summary>
    [Key(5)]
    public string? Group { get; init; }

    /// <summary>
    /// Hostname or IP address of the agent machine.
    /// </summary>
    [Key(6)]
    public string? Hostname { get; init; }

    /// <summary>
    /// Agent version.
    /// </summary>
    [Key(7)]
    public string? Version { get; init; }

    /// <summary>
    /// Timestamp when the agent was registered.
    /// </summary>
    [Key(8)]
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp of the last heartbeat received.
    /// </summary>
    [Key(9)]
    public DateTimeOffset? LastHeartbeat { get; init; }

    /// <summary>
    /// SignalR connection ID (server-side only).
    /// </summary>
    [Key(10)]
    public string? ConnectionId { get; init; }

    /// <summary>
    /// Custom metadata for domain-specific information.
    /// </summary>
    [Key(11)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Checks if the agent has a specific capability.
    /// </summary>
    public bool HasCapability(string capabilityName) =>
        Capabilities.Any(c => c.Name.Equals(capabilityName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks if the agent has a specific tag.
    /// </summary>
    public bool HasTag(string tag) =>
        Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
}
