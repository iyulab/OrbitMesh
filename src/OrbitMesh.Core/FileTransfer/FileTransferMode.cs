namespace OrbitMesh.Core.FileTransfer;

// Note: FileTransferMode enum is defined in OrbitMesh.Core.Models.DeploymentProfile
// Use OrbitMesh.Core.Models.FileTransferMode for the transfer mode options.

/// <summary>
/// Status of a file transfer operation.
/// </summary>
public enum FileTransferStatus
{
    /// <summary>Transfer is queued and waiting to start.</summary>
    Pending = 0,

    /// <summary>Transfer is in progress.</summary>
    InProgress = 1,

    /// <summary>Transfer completed successfully.</summary>
    Completed = 2,

    /// <summary>Transfer failed.</summary>
    Failed = 3,

    /// <summary>Transfer was cancelled.</summary>
    Cancelled = 4
}

/// <summary>
/// The actual method used for a file transfer.
/// </summary>
public enum FileTransferMethod
{
    /// <summary>Transfer was performed via P2P direct connection.</summary>
    P2P = 0,

    /// <summary>Transfer was performed via HTTP through the server.</summary>
    Http = 1,

    /// <summary>Transfer method is not yet determined.</summary>
    Unknown = 2
}
