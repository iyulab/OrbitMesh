namespace OrbitMesh.Agent.BuiltIn;

/// <summary>
/// Built-in command names provided by the OrbitMesh SDK.
/// These commands are automatically available when using built-in handlers.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible (intentional design for command grouping)
public static class Commands
{
    /// <summary>
    /// File transfer commands.
    /// </summary>
    public static class File
    {
        /// <summary>
        /// Download a file from server to agent.
        /// </summary>
        public const string Download = "orbit:file:download";

        /// <summary>
        /// Upload a file from agent to server.
        /// </summary>
        public const string Upload = "orbit:file:upload";

        /// <summary>
        /// Delete a file on the agent.
        /// </summary>
        public const string Delete = "orbit:file:delete";

        /// <summary>
        /// List files in a directory.
        /// </summary>
        public const string List = "orbit:file:list";

        /// <summary>
        /// Get file metadata (size, checksum, modified time).
        /// </summary>
        public const string Info = "orbit:file:info";

        /// <summary>
        /// Check if file exists.
        /// </summary>
        public const string Exists = "orbit:file:exists";

        /// <summary>
        /// Sync a directory from server to agent.
        /// </summary>
        public const string Sync = "orbit:file:sync";
    }

    /// <summary>
    /// Service management commands.
    /// </summary>
    public static class Service
    {
        /// <summary>
        /// Start a service.
        /// </summary>
        public const string Start = "orbit:service:start";

        /// <summary>
        /// Stop a service.
        /// </summary>
        public const string Stop = "orbit:service:stop";

        /// <summary>
        /// Restart a service.
        /// </summary>
        public const string Restart = "orbit:service:restart";

        /// <summary>
        /// Get service status.
        /// </summary>
        public const string Status = "orbit:service:status";
    }

    /// <summary>
    /// Agent system health and diagnostics commands.
    /// </summary>
#pragma warning disable CA1724 // Type name conflicts with namespace (intentional)
    public static class System
    {
        /// <summary>
        /// Perform health check.
        /// </summary>
        public const string HealthCheck = "orbit:system:health";

        /// <summary>
        /// Get agent version information.
        /// </summary>
        public const string Version = "orbit:system:version";

        /// <summary>
        /// Get system metrics (CPU, memory, disk).
        /// </summary>
        public const string Metrics = "orbit:system:metrics";

        /// <summary>
        /// Execute a shell command (if enabled).
        /// </summary>
        public const string Execute = "orbit:system:exec";

        /// <summary>
        /// Ping - simple connectivity check.
        /// </summary>
        public const string Ping = "orbit:system:ping";
    }
#pragma warning restore CA1724

    /// <summary>
    /// File watch commands for monitoring file system changes.
    /// </summary>
    public static class FileWatch
    {
        /// <summary>
        /// Start watching a directory for changes.
        /// </summary>
        public const string Start = "orbit:filewatch:start";

        /// <summary>
        /// Stop watching a directory.
        /// </summary>
        public const string Stop = "orbit:filewatch:stop";

        /// <summary>
        /// List active file watches.
        /// </summary>
        public const string List = "orbit:filewatch:list";
    }

    /// <summary>
    /// Update management commands.
    /// </summary>
    public static class Update
    {
        /// <summary>
        /// Check for available updates.
        /// </summary>
        public const string Check = "orbit:update:check";

        /// <summary>
        /// Download an update package.
        /// </summary>
        public const string Download = "orbit:update:download";

        /// <summary>
        /// Apply a downloaded update.
        /// </summary>
        public const string Apply = "orbit:update:apply";

        /// <summary>
        /// Rollback to previous version.
        /// </summary>
        public const string Rollback = "orbit:update:rollback";

        /// <summary>
        /// Get current update status.
        /// </summary>
        public const string Status = "orbit:update:status";
    }
}
#pragma warning restore CA1034
