using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents a capability that an agent can declare.
/// Capabilities are used for agent selection and job routing.
/// </summary>
[MessagePackObject]
public sealed record AgentCapability
{
    /// <summary>
    /// The unique name of the capability.
    /// </summary>
    [Key(0)]
    public required string Name { get; init; }

    /// <summary>
    /// Optional version of the capability.
    /// </summary>
    [Key(1)]
    public string? Version { get; init; }

    /// <summary>
    /// Optional metadata associated with the capability.
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Creates a new capability with the specified name.
    /// </summary>
    public static AgentCapability Create(string name) => new() { Name = name };

    /// <summary>
    /// Creates a new capability with the specified name and version.
    /// </summary>
    public static AgentCapability Create(string name, string version) =>
        new() { Name = name, Version = version };

    public override string ToString() =>
        Version is null ? Name : $"{Name}@{Version}";
}

/// <summary>
/// Common built-in capabilities.
/// </summary>
public static class BuiltInCapabilities
{
    public static AgentCapability Gpu => AgentCapability.Create("gpu");
    public static AgentCapability HighMemory => AgentCapability.Create("high-memory");
    public static AgentCapability HighCpu => AgentCapability.Create("high-cpu");
    public static AgentCapability Storage => AgentCapability.Create("storage");
    public static AgentCapability Network => AgentCapability.Create("network");
}
