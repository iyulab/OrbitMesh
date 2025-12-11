using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrbitMesh.Node.LocalScheduler;

/// <summary>
/// Extension methods for configuring local scheduler services.
/// </summary>
public static class LocalSchedulerExtensions
{
    /// <summary>
    /// Adds the local scheduler service to the service collection.
    /// The scheduler runs tasks independently of server connection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for pre-registering tasks.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalScheduler(
        this IServiceCollection services,
        Action<LocalSchedulerService>? configure = null)
    {
        services.AddSingleton<LocalSchedulerService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LocalSchedulerService>>();
            var agent = sp.GetService<IMeshAgent>();
            var scheduler = new LocalSchedulerService(logger, agent);

            configure?.Invoke(scheduler);

            return scheduler;
        });

        services.AddHostedService(sp => sp.GetRequiredService<LocalSchedulerService>());

        return services;
    }

    /// <summary>
    /// Registers a simple interval-based task.
    /// </summary>
    /// <param name="scheduler">The scheduler service.</param>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="taskName">Display name for the task.</param>
    /// <param name="interval">Execution interval.</param>
    /// <param name="handler">Task handler.</param>
    /// <param name="runOffline">Whether to run when server is disconnected.</param>
    /// <param name="runOnStartup">Whether to run immediately on startup.</param>
    public static void RegisterIntervalTask(
        this LocalSchedulerService scheduler,
        string taskId,
        string taskName,
        TimeSpan interval,
        LocalTaskHandler handler,
        bool runOffline = true,
        bool runOnStartup = false)
    {
        scheduler.RegisterTask(
            new LocalTaskConfig
            {
                Id = taskId,
                Name = taskName,
                Interval = interval,
                RunOffline = runOffline,
                RunOnStartup = runOnStartup
            },
            handler);
    }

    /// <summary>
    /// Registers a simple interval-based task with a synchronous handler.
    /// </summary>
    public static void RegisterIntervalTask(
        this LocalSchedulerService scheduler,
        string taskId,
        string taskName,
        TimeSpan interval,
        Func<LocalTaskContext, object?> handler,
        bool runOffline = true,
        bool runOnStartup = false)
    {
        scheduler.RegisterIntervalTask(
            taskId,
            taskName,
            interval,
            (ctx, ct) => Task.FromResult(handler(ctx)),
            runOffline,
            runOnStartup);
    }

    /// <summary>
    /// Registers a simple interval-based task with an action handler (no return value).
    /// </summary>
    public static void RegisterIntervalTask(
        this LocalSchedulerService scheduler,
        string taskId,
        string taskName,
        TimeSpan interval,
        Action<LocalTaskContext> handler,
        bool runOffline = true,
        bool runOnStartup = false)
    {
        scheduler.RegisterIntervalTask(
            taskId,
            taskName,
            interval,
            (ctx, ct) =>
            {
                handler(ctx);
                return Task.FromResult<object?>(null);
            },
            runOffline,
            runOnStartup);
    }

    /// <summary>
    /// Registers a simple interval-based task with an async action handler (no return value).
    /// </summary>
    public static void RegisterIntervalTask(
        this LocalSchedulerService scheduler,
        string taskId,
        string taskName,
        TimeSpan interval,
        Func<LocalTaskContext, CancellationToken, Task> handler,
        bool runOffline = true,
        bool runOnStartup = false)
    {
        scheduler.RegisterIntervalTask(
            taskId,
            taskName,
            interval,
            async (ctx, ct) =>
            {
                await handler(ctx, ct);
                return null;
            },
            runOffline,
            runOnStartup);
    }
}
