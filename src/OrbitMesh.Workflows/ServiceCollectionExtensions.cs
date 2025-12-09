using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Execution;
using OrbitMesh.Workflows.Parsing;

namespace OrbitMesh.Workflows;

/// <summary>
/// Extension methods for registering workflow services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds workflow engine services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrbitMeshWorkflows(this IServiceCollection services)
    {
        // Core engine
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
        services.AddSingleton<IWorkflowInstanceStore, InMemoryWorkflowInstanceStore>();
        services.AddSingleton<IWorkflowRegistry, InMemoryWorkflowRegistry>();

        // Execution infrastructure
        services.AddSingleton<IStepExecutorFactory, StepExecutorFactory>();
        services.AddSingleton<IExpressionEvaluator, SimpleExpressionEvaluator>();

        // Step executors
        services.AddSingleton<JobStepExecutor>();
        services.AddSingleton<DelayStepExecutor>();
        services.AddSingleton<TransformStepExecutor>();
        services.AddSingleton<WaitForEventStepExecutor>();
        services.AddSingleton<ApprovalStepExecutor>();
        services.AddSingleton<ParallelStepExecutor>();
        services.AddSingleton<ConditionalStepExecutor>();
        services.AddSingleton<ForEachStepExecutor>();
        services.AddSingleton<SubWorkflowStepExecutor>();
        services.AddSingleton<NotifyStepExecutor>();

        // Default no-op external services (can be overridden by AddNotificationSender, etc.)
        services.AddSingleton<INotificationSender, NoOpNotificationSender>();
        services.AddSingleton<IApprovalNotifier, NoOpApprovalNotifier>();
        services.AddSingleton<ISubWorkflowLauncher, NoOpSubWorkflowLauncher>();

        // Parsing
        services.AddSingleton<WorkflowParser>();
        services.AddSingleton<WorkflowSerializer>();

        return services;
    }

    /// <summary>
    /// Adds a custom workflow instance store implementation.
    /// </summary>
    /// <typeparam name="TStore">The store implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowInstanceStore<TStore>(this IServiceCollection services)
        where TStore : class, IWorkflowInstanceStore
    {
        services.AddSingleton<IWorkflowInstanceStore, TStore>();
        return services;
    }

    /// <summary>
    /// Adds a custom workflow registry implementation.
    /// </summary>
    /// <typeparam name="TRegistry">The registry implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowRegistry<TRegistry>(this IServiceCollection services)
        where TRegistry : class, IWorkflowRegistry
    {
        services.AddSingleton<IWorkflowRegistry, TRegistry>();
        return services;
    }

    /// <summary>
    /// Adds a job dispatcher for workflow job steps.
    /// </summary>
    /// <typeparam name="TDispatcher">The dispatcher implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobDispatcher<TDispatcher>(this IServiceCollection services)
        where TDispatcher : class, IJobDispatcher
    {
        services.AddSingleton<IJobDispatcher, TDispatcher>();
        return services;
    }

    /// <summary>
    /// Adds a notification sender for workflow notify steps.
    /// </summary>
    /// <typeparam name="TSender">The notification sender implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotificationSender<TSender>(this IServiceCollection services)
        where TSender : class, INotificationSender
    {
        services.AddSingleton<INotificationSender, TSender>();
        return services;
    }

    /// <summary>
    /// Adds a sub-workflow launcher for workflow sub-workflow steps.
    /// </summary>
    /// <typeparam name="TLauncher">The launcher implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSubWorkflowLauncher<TLauncher>(this IServiceCollection services)
        where TLauncher : class, ISubWorkflowLauncher
    {
        services.AddSingleton<ISubWorkflowLauncher, TLauncher>();
        return services;
    }

    /// <summary>
    /// Adds an approval notifier for workflow approval steps.
    /// </summary>
    /// <typeparam name="TNotifier">The notifier implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApprovalNotifier<TNotifier>(this IServiceCollection services)
        where TNotifier : class, IApprovalNotifier
    {
        services.AddSingleton<IApprovalNotifier, TNotifier>();
        return services;
    }
}
