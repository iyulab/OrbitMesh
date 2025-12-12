using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrbitMesh.Host.Extensions;
using OrbitMesh.Workflows.BuiltIn;
using OrbitMesh.Workflows.Engine;
using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Host.Features;

/// <summary>
/// Extension methods for registering built-in features.
/// </summary>
internal static class BuiltInFeatureExtensions
{
    /// <summary>
    /// Adds built-in features based on configuration.
    /// Reads from OrbitMesh:Features section in configuration.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddBuiltInFeatures(
        this OrbitMeshServerBuilder builder,
        IConfiguration configuration)
    {
        var options = new BuiltInFeatureOptions();
        configuration.GetSection("OrbitMesh:Features").Bind(options);

        return builder.AddBuiltInFeatures(options);
    }

    /// <summary>
    /// Adds built-in features with explicit options.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="options">The feature options.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddBuiltInFeatures(
        this OrbitMeshServerBuilder builder,
        BuiltInFeatureOptions options)
    {
        // Register the feature options for DI
        builder.Services.AddSingleton(options);

        // Register built-in workflow loader
        builder.Services.AddSingleton<BuiltInWorkflowProvider>();
        builder.Services.AddSingleton<IBuiltInFeatureService, BuiltInFeatureService>();

        // Register hosted service to initialize features on startup
        builder.Services.AddHostedService<BuiltInFeatureInitializer>();

        // Configure individual features
        if (options.FileSync?.Enabled == true)
        {
            builder.AddFileStorage(options.FileSync.RootPath);
        }

        return builder;
    }

    /// <summary>
    /// Adds built-in features with configuration action.
    /// </summary>
    /// <param name="builder">The OrbitMesh server builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static OrbitMeshServerBuilder AddBuiltInFeatures(
        this OrbitMeshServerBuilder builder,
        Action<BuiltInFeatureOptions> configure)
    {
        var options = new BuiltInFeatureOptions();
        configure(options);

        return builder.AddBuiltInFeatures(options);
    }
}

/// <summary>
/// Service interface for managing built-in features.
/// </summary>
internal interface IBuiltInFeatureService
{
    /// <summary>
    /// Gets the list of registered built-in workflows.
    /// </summary>
    IReadOnlyList<WorkflowDefinition> RegisteredWorkflows { get; }

    /// <summary>
    /// Registers built-in workflows based on enabled features.
    /// </summary>
    Task RegisterWorkflowsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing built-in features and workflows.
/// </summary>
internal sealed class BuiltInFeatureService : IBuiltInFeatureService
{
    private readonly BuiltInFeatureOptions _options;
    private readonly BuiltInWorkflowProvider _workflowProvider;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly ILogger<BuiltInFeatureService> _logger;
    private readonly List<WorkflowDefinition> _registeredWorkflows = [];

    public BuiltInFeatureService(
        BuiltInFeatureOptions options,
        BuiltInWorkflowProvider workflowProvider,
        IWorkflowRegistry workflowRegistry,
        ILogger<BuiltInFeatureService> logger)
    {
        _options = options;
        _workflowProvider = workflowProvider;
        _workflowRegistry = workflowRegistry;
        _logger = logger;
    }

    public IReadOnlyList<WorkflowDefinition> RegisteredWorkflows => _registeredWorkflows.AsReadOnly();

    public async Task RegisterWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering built-in workflows based on feature configuration");

        // FileSync feature
        if (_options.FileSync?.Enabled == true)
        {
            await RegisterFileSyncWorkflowsAsync(cancellationToken);
        }

        // HealthMonitor feature
        if (_options.HealthMonitor?.Enabled == true)
        {
            await RegisterHealthMonitorWorkflowsAsync(cancellationToken);
        }

        // ServiceManagement feature
        if (_options.ServiceManagement?.Enabled == true)
        {
            await RegisterServiceManagementWorkflowsAsync(cancellationToken);
        }

        _logger.LogInformation("Registered {Count} built-in workflows", _registeredWorkflows.Count);
    }

    private async Task RegisterFileSyncWorkflowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _workflowProvider.LoadTemplateAsync("file-sync", cancellationToken);

            // Customize workflow based on options
            var customized = CustomizeFileSyncWorkflow(workflow, _options.FileSync!);

            await _workflowRegistry.RegisterAsync(customized, cancellationToken);
            _registeredWorkflows.Add(customized);

            _logger.LogInformation("Registered file-sync workflow: {Id}", customized.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register file-sync workflow");
        }
    }

    private async Task RegisterHealthMonitorWorkflowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _workflowProvider.LoadTemplateAsync("health-check", cancellationToken);

            // Customize workflow based on options
            var customized = CustomizeHealthCheckWorkflow(workflow, _options.HealthMonitor!);

            await _workflowRegistry.RegisterAsync(customized, cancellationToken);
            _registeredWorkflows.Add(customized);

            _logger.LogInformation("Registered health-check workflow: {Id}", customized.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register health-check workflow");
        }
    }

    private async Task RegisterServiceManagementWorkflowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _workflowProvider.LoadTemplateAsync("service-restart", cancellationToken);

            await _workflowRegistry.RegisterAsync(workflow, cancellationToken);
            _registeredWorkflows.Add(workflow);

            _logger.LogInformation("Registered service-restart workflow: {Id}", workflow.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register service-restart workflow");
        }
    }

    private static WorkflowDefinition CustomizeFileSyncWorkflow(
        WorkflowDefinition template,
        FileSyncFeatureOptions options)
    {
        // Create customized workflow with feature-specific settings
        // The template's input_schema allows runtime customization,
        // but we can set default values based on configuration
        return template with
        {
            Description = $"File Synchronization (Mode: {options.SyncMode}, Path: {options.RootPath})"
        };
    }

    private static WorkflowDefinition CustomizeHealthCheckWorkflow(
        WorkflowDefinition template,
        HealthMonitorFeatureOptions options)
    {
        // Parse interval string (e.g., "5m", "30s", "1h") to TimeSpan
        var interval = ParseInterval(options.Interval);

        // Customize trigger interval based on options
        var triggers = (template.Triggers ?? []).Select(t =>
        {
            if (t is ScheduleTrigger scheduleTrigger)
            {
                return scheduleTrigger with { Interval = interval };
            }
            return t;
        }).ToList();

        return template with
        {
            Triggers = triggers,
            Description = $"Health Monitor (Interval: {options.Interval}, Pattern: {options.AgentPattern})"
        };
    }

    private static TimeSpan? ParseInterval(string? intervalString)
    {
        if (string.IsNullOrWhiteSpace(intervalString))
            return null;

        var span = intervalString.AsSpan().Trim();
        if (span.Length < 2)
            return null;

        var unit = span[^1];
        if (!int.TryParse(span[..^1], out var value))
            return null;

        return unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => null
        };
    }
}

/// <summary>
/// Background service that initializes built-in features on startup.
/// </summary>
internal sealed class BuiltInFeatureInitializer : BackgroundService
{
    private readonly IBuiltInFeatureService _featureService;
    private readonly ILogger<BuiltInFeatureInitializer> _logger;

    public BuiltInFeatureInitializer(
        IBuiltInFeatureService featureService,
        ILogger<BuiltInFeatureInitializer> logger)
    {
        _featureService = featureService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for other services to initialize
        await Task.Delay(1000, stoppingToken);

        try
        {
            await _featureService.RegisterWorkflowsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize built-in features");
        }
    }
}
