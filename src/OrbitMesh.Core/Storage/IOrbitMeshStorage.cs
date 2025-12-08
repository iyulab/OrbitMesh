namespace OrbitMesh.Core.Storage;

/// <summary>
/// Unified storage abstraction for OrbitMesh.
/// Provides access to all storage components.
/// </summary>
public interface IOrbitMeshStorage
{
    /// <summary>
    /// Storage for jobs.
    /// </summary>
    IJobStore Jobs { get; }

    /// <summary>
    /// Storage for agents.
    /// </summary>
    IAgentStore Agents { get; }

    /// <summary>
    /// Storage for workflows.
    /// </summary>
    IWorkflowStore Workflows { get; }

    /// <summary>
    /// Storage for events (event sourcing).
    /// </summary>
    IEventStore Events { get; }

    /// <summary>
    /// Ensures the storage is initialized and ready.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs any necessary cleanup.
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Marker interface for storage providers.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Provider name (e.g., "sqlite", "sqlserver", "redis").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates the storage instance.
    /// </summary>
    IOrbitMeshStorage CreateStorage();
}
