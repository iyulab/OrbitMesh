namespace OrbitMesh.Core.Enums;

/// <summary>
/// Represents the lifecycle state of a job in the mesh.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job has been created and is waiting to be assigned.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Job has been assigned to an agent but not yet acknowledged.
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// Job has been acknowledged and is being executed.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Job failed during execution.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Job was cancelled before completion.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// Job timed out during execution.
    /// </summary>
    TimedOut = 6
}
