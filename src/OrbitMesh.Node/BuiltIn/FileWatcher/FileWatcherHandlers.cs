using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Node.BuiltIn.FileWatcher;

/// <summary>
/// Handler for starting a file watch.
/// </summary>
public sealed class StartFileWatchHandler : IRequestResponseHandler<StartFileWatchResult>
{
    private readonly FileWatcherService _watcherService;
    private readonly ILogger<StartFileWatchHandler> _logger;

    /// <inheritdoc />
    public string Command => Commands.FileWatch.Start;

    /// <summary>
    /// Creates a new handler.
    /// </summary>
    public StartFileWatchHandler(FileWatcherService watcherService, ILogger<StartFileWatchHandler> logger)
    {
        _watcherService = watcherService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<StartFileWatchResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<StartFileWatchRequest>();

        _logger.LogInformation("Starting file watch {WatchId} for path {Path}", request.WatchId, request.Path);

        var config = new FileWatchConfig
        {
            Id = request.WatchId,
            Path = request.Path,
            Filter = request.Filter,
            IncludeSubdirectories = request.IncludeSubdirectories,
            DebounceMs = request.DebounceMs
        };

        var success = _watcherService.StartWatch(config);

        return Task.FromResult(new StartFileWatchResult
        {
            Success = success,
            WatchId = success ? request.WatchId : null,
            Error = success ? null : "Failed to start file watch. Directory may not exist or watch ID already in use."
        });
    }
}

/// <summary>
/// Handler for stopping a file watch.
/// </summary>
public sealed class StopFileWatchHandler : IRequestResponseHandler<StopFileWatchResult>
{
    private readonly FileWatcherService _watcherService;
    private readonly ILogger<StopFileWatchHandler> _logger;

    /// <inheritdoc />
    public string Command => Commands.FileWatch.Stop;

    /// <summary>
    /// Creates a new handler.
    /// </summary>
    public StopFileWatchHandler(FileWatcherService watcherService, ILogger<StopFileWatchHandler> logger)
    {
        _watcherService = watcherService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<StopFileWatchResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<StopFileWatchRequest>();

        _logger.LogInformation("Stopping file watch {WatchId}", request.WatchId);

        var success = _watcherService.StopWatch(request.WatchId);

        return Task.FromResult(new StopFileWatchResult
        {
            Success = success,
            Error = success ? null : "Watch not found"
        });
    }
}

/// <summary>
/// Handler for listing active file watches.
/// </summary>
public sealed class ListFileWatchesHandler : IRequestResponseHandler<ListFileWatchesResult>
{
    private readonly FileWatcherService _watcherService;
    private readonly ILogger<ListFileWatchesHandler> _logger;

    /// <inheritdoc />
    public string Command => Commands.FileWatch.List;

    /// <summary>
    /// Creates a new handler.
    /// </summary>
    public ListFileWatchesHandler(FileWatcherService watcherService, ILogger<ListFileWatchesHandler> logger)
    {
        _watcherService = watcherService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ListFileWatchesResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing active file watches");

        var watches = _watcherService.GetActiveWatches()
            .Select(id =>
            {
                var config = _watcherService.GetWatchConfig(id);
                return new FileWatchInfo
                {
                    WatchId = id,
                    Path = config?.Path ?? "",
                    Filter = config?.Filter ?? "*.*",
                    IncludeSubdirectories = config?.IncludeSubdirectories ?? false
                };
            })
            .ToList();

        return Task.FromResult(new ListFileWatchesResult
        {
            Success = true,
            Watches = watches
        });
    }
}
