using System.Globalization;
using OrbitMesh.Core.Platform;
using OrbitMesh.Node;
using OrbitMesh.Node.BuiltIn;
using OrbitMesh.Node.Extensions;
using OrbitMesh.Update.Extensions;
using OrbitMesh.Update.Services;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting OrbitMesh Agent");

    // Initialize platform paths
    var platformPaths = new PlatformPaths();
    platformPaths.EnsureDirectoriesExist();
    Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

    var builder = Host.CreateApplicationBuilder(args);

    // Register platform paths
    builder.Services.AddSingleton<IPlatformPaths>(platformPaths);

    // Add update service
    builder.Services.AddOrbitMeshUpdate(options =>
    {
        options.ProductName = "orbit-node";
        // Configure from appsettings if available
        builder.Configuration.GetSection("OrbitMesh:Update").Bind(options);
    });

    // Configure Serilog
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    // Get configuration
    var serverUrl = builder.Configuration["OrbitMesh:ServerUrl"]
        ?? builder.Configuration["SERVER_URL"]
        ?? "http://localhost:5000/agent";

    var agentName = builder.Configuration["OrbitMesh:AgentName"]
        ?? builder.Configuration["AGENT_NAME"]
        ?? Environment.MachineName;

    var accessToken = builder.Configuration["OrbitMesh:AccessToken"]
        ?? builder.Configuration["ORBITMESH_TOKEN"]
        ?? string.Empty;

    var bootstrapToken = builder.Configuration["OrbitMesh:BootstrapToken"]
        ?? builder.Configuration["ORBITMESH_BOOTSTRAP_TOKEN"]
        ?? string.Empty;

    var tags = builder.Configuration["OrbitMesh:Tags"]
        ?? builder.Configuration["AGENT_TAGS"]
        ?? string.Empty;

    var enableShellExecution = bool.TryParse(
        builder.Configuration["OrbitMesh:EnableShellExecution"]
            ?? builder.Configuration["ENABLE_SHELL_EXECUTION"],
        out var shellEnabled) && shellEnabled;

    var highAvailability = bool.TryParse(
        builder.Configuration["OrbitMesh:HighAvailability"]
            ?? builder.Configuration["HIGH_AVAILABILITY"],
        out var haEnabled) && haEnabled;

    Log.Information("Connecting to server at {ServerUrl}", serverUrl);
    Log.Information("Agent name: {AgentName}", agentName);
    if (!string.IsNullOrEmpty(accessToken))
    {
        Log.Information("Using access token for authentication");
    }
    else if (!string.IsNullOrEmpty(bootstrapToken))
    {
        Log.Information("Using bootstrap token for enrollment");
    }
    else
    {
        Log.Warning("No access token or bootstrap token configured. Set OrbitMesh:BootstrapToken for enrollment or OrbitMesh:AccessToken for production.");
    }
    if (!string.IsNullOrEmpty(tags))
    {
        Log.Information("Agent tags: {Tags}", tags);
    }

    // Add OrbitMesh agent as hosted service
    builder.Services.AddOrbitMeshAgentHostedService(serverUrl, agent =>
    {
        agent.WithName(agentName);

        // Configure authentication
        if (!string.IsNullOrEmpty(accessToken))
        {
            agent.WithAccessToken(accessToken);
        }
        else if (!string.IsNullOrEmpty(bootstrapToken))
        {
            agent.WithBootstrapToken(bootstrapToken);
        }

        // Add tags from configuration
        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            agent.WithTags(tagList);
        }

        // Configure built-in handlers
        agent.WithBuiltInHandlers(new BuiltInHandlerOptions
        {
            LoggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>(),
            AppVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            EnableShellExecution = enableShellExecution
        });

        // Configure connection timeout
        agent.WithConnectionTimeout(TimeSpan.FromSeconds(30));

        // Configure resilience options
        if (highAvailability)
        {
            agent.WithHighAvailability();
            Log.Information("High availability resilience mode enabled");
        }
    });

    var host = builder.Build();

    // Check for updates on startup
    var updateService = host.Services.GetRequiredService<IUpdateService>();
    var updateResult = await updateService.CheckAndApplyUpdateAsync();
    if (updateResult.UpdatePending)
    {
        Log.Information("Update to v{Version} is being applied. Restarting...", updateResult.NewVersion);
        return; // Exit for update script to apply
    }
    else if (updateResult.UpdateAvailable)
    {
        Log.Information("Update available: v{Version} (auto-update disabled)", updateResult.NewVersion);
    }

    Log.Information("OrbitMesh Agent starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
