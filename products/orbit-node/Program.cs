using System.Globalization;
using System.IO;
using OrbitMesh.Core.Platform;
using OrbitMesh.Node;
using OrbitMesh.Node.BuiltIn;
using OrbitMesh.Node.Extensions;
using OrbitMesh.Update.Extensions;
using OrbitMesh.Update.Services;
using Serilog;

#if WINDOWS
using System.Windows;
using OrbitMesh.Products.Agent.Tray;
#endif

// Configure Serilog - file logging for tray mode, console for others
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information();

#if WINDOWS
if (!Environment.GetCommandLineArgs().Contains("--console"))
{
    // Tray mode: log to file only
    var platformPaths = new PlatformPaths();
    platformPaths.EnsureDirectoriesExist();
    var logPath = Path.Combine(platformPaths.LogsPath, "agent-.log");
    logConfig.WriteTo.File(logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture);
}
else
#endif
{
    // Console mode
    logConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture);
}

Log.Logger = logConfig.CreateLogger();

#if WINDOWS
// Windows: Run as tray app unless --console flag is passed
if (!Environment.GetCommandLineArgs().Contains("--console"))
{
    // WPF requires STA thread - create one explicitly
    var staThread = new Thread(() =>
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        TrayApplication? trayApp = null;
        app.Startup += (_, _) =>
        {
            trayApp = new TrayApplication(StartAgentWithSettingsAsync);
        };
        app.Exit += (_, _) =>
        {
            trayApp?.Dispose();
        };
        app.Run();
    });
    staThread.SetApartmentState(ApartmentState.STA);
    staThread.Start();
    staThread.Join();
}
else
{
    // Console mode for debugging
    await RunConsoleAsync();
}
#else
// Linux/macOS: Run as console app
await RunConsoleAsync();
#endif

return;

// ============================================================================
// Agent startup logic
// ============================================================================

#if WINDOWS
async Task StartAgentWithSettingsAsync(AgentSettings settings, CancellationToken cancellationToken)
{
    try
    {
        Log.Information("Starting OrbitMesh Agent");

        var platformPaths = new PlatformPaths();
        platformPaths.EnsureDirectoriesExist();
        Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

        var builder = Host.CreateApplicationBuilder(args);

        ConfigureServicesWithSettings(builder, platformPaths, settings);

        var host = builder.Build();

        await InitializeAndRunAgent(host, cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Log.Information("Agent shutdown requested");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
        throw;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

void ConfigureServicesWithSettings(HostApplicationBuilder builder, IPlatformPaths platformPaths, AgentSettings settings)
{
    // Register platform paths
    builder.Services.AddSingleton(platformPaths);

    // Add update service
    builder.Services.AddOrbitMeshUpdate(options =>
    {
        options.ProductName = "orbit-node";
        builder.Configuration.GetSection("OrbitMesh:Update").Bind(options);
    });

    // Configure Serilog
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    // Use settings from tray dialog (priority) or fall back to configuration
    var serverUrl = settings.NormalizedServerUrl;
    var bootstrapToken = settings.BootstrapToken;
    var agentName = !string.IsNullOrEmpty(settings.AgentName)
        ? settings.AgentName
        : Environment.MachineName;

    // Get additional config from appsettings
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
    if (!string.IsNullOrEmpty(bootstrapToken))
    {
        Log.Information("Using bootstrap token for enrollment");
    }
    if (!string.IsNullOrEmpty(tags))
    {
        Log.Information("Agent tags: {Tags}", tags);
    }

    // Add OrbitMesh agent as hosted service
    builder.Services.AddOrbitMeshAgentHostedService(serverUrl, agent =>
    {
        agent.WithName(agentName);

        // Configure authentication with bootstrap token
        if (!string.IsNullOrEmpty(bootstrapToken))
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
}
#endif

async Task RunConsoleAsync()
{
    try
    {
        Log.Information("Starting OrbitMesh Agent");

        var platformPaths = new PlatformPaths();
        platformPaths.EnsureDirectoriesExist();
        Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

        var builder = Host.CreateApplicationBuilder(args);

        ConfigureServices(builder, platformPaths);

        var host = builder.Build();

        await InitializeAndRunAgent(host, CancellationToken.None);
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
}

void ConfigureServices(HostApplicationBuilder builder, IPlatformPaths platformPaths)
{
    // Register platform paths
    builder.Services.AddSingleton(platformPaths);

    // Add update service
    builder.Services.AddOrbitMeshUpdate(options =>
    {
        options.ProductName = "orbit-node";
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
}

async Task InitializeAndRunAgent(IHost host, CancellationToken cancellationToken)
{
    // Check for updates on startup
    var updateService = host.Services.GetRequiredService<IUpdateService>();
    var updateResult = await updateService.CheckAndApplyUpdateAsync();
    if (updateResult.UpdatePending)
    {
        Log.Information("Update to v{Version} is being applied. Restarting...", updateResult.NewVersion);
        return;
    }
    else if (updateResult.UpdateAvailable)
    {
        Log.Information("Update available: v{Version} (auto-update disabled)", updateResult.NewVersion);
    }

    Log.Information("OrbitMesh Agent starting...");

    if (cancellationToken == CancellationToken.None)
    {
        await host.RunAsync();
    }
    else
    {
        await host.RunAsync(cancellationToken);
    }
}
