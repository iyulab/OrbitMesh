using System.Globalization;
using Microsoft.Extensions.FileProviders;
using OrbitMesh.Core.Platform;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Extensions;
using OrbitMesh.Host.Features;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Extensions;
using OrbitMesh.Update.Extensions;
using OrbitMesh.Update.Services;
using Serilog;

#if WINDOWS
using System.Windows.Forms;
using OrbitMesh.Products.Server.Tray;
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
    var logPath = Path.Combine(platformPaths.LogsPath, "server-.log");
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
    Application.SetHighDpiMode(HighDpiMode.SystemAware);
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    using var trayContext = new TrayApplicationContext(
        StartServerAsync,
        "http://localhost:5000");

    Application.Run(trayContext);
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
// Server startup logic
// ============================================================================

async Task StartServerAsync(CancellationToken cancellationToken)
{
    try
    {
        Log.Information("Starting OrbitMesh Server");

        var platformPaths = new PlatformPaths();
        platformPaths.EnsureDirectoriesExist();
        Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog
        builder.Host.UseSerilog();

        ConfigureServices(builder, platformPaths);

        var app = builder.Build();

        await InitializeAndConfigureApp(app, platformPaths);

        Log.Information("OrbitMesh Server listening on {Urls}", string.Join(", ", app.Urls));
        await app.RunAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Log.Information("Server shutdown requested");
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

async Task RunConsoleAsync()
{
    try
    {
        Log.Information("Starting OrbitMesh Server");

        var platformPaths = new PlatformPaths();
        platformPaths.EnsureDirectoriesExist();
        Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog
        builder.Host.UseSerilog();

        ConfigureServices(builder, platformPaths);

        var app = builder.Build();

        await InitializeAndConfigureApp(app, platformPaths);

        Log.Information("OrbitMesh Server listening on {Urls}", string.Join(", ", app.Urls));
        await app.RunAsync();
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

void ConfigureServices(WebApplicationBuilder builder, IPlatformPaths platformPaths)
{
    // Add OrbitMesh server services with workflows
    builder.Services.AddOrbitMeshServer(server =>
    {
        server.AddHealthChecks(options =>
        {
            options.PendingJobThreshold = 1000;
        });

        server.AddWorkflows();
        server.AddDeployments();

        // Add built-in features based on configuration
        server.AddBuiltInFeatures(builder.Configuration);

        server.ConfigureSignalR(hub =>
        {
            hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
            hub.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            hub.StreamBufferCapacity = 20;
        });

        server.ConfigureWorkItemProcessor(options =>
        {
            options.PollingInterval = TimeSpan.FromMilliseconds(100);
            options.MaxConcurrency = 50;
        });

        server.ConfigureJobTimeoutMonitor(options =>
        {
            options.CheckInterval = TimeSpan.FromSeconds(10);
        });
    });

    // Configure security options from configuration
    builder.Services.Configure<SecurityOptions>(
        builder.Configuration.GetSection(SecurityOptions.SectionName));

    // Register platform paths
    builder.Services.AddSingleton(platformPaths);

    // Add SQLite storage with persistent path
    var connectionString = builder.Configuration.GetConnectionString("OrbitMesh")
        ?? $"Data Source={platformPaths.DatabasePath}";
    builder.Services.AddOrbitMeshSqliteStorage(connectionString);

    // Add update service
    builder.Services.AddOrbitMeshUpdate(options =>
    {
        options.ProductName = "orbit-host";
        builder.Configuration.GetSection("OrbitMesh:Update").Bind(options);
    });

    // Add API controllers with JSON enum string serialization
    // Include controllers from OrbitMesh.Host assembly
    builder.Services.AddControllers()
        .AddApplicationPart(typeof(OrbitMesh.Host.Controllers.AgentsController).Assembly)
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // Add Swagger for development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "OrbitMesh Server API", Version = "v1" });
        });
    }

    // Add admin authentication
    builder.Services.AddAdminAuthentication(builder.Configuration);
}

async Task InitializeAndConfigureApp(WebApplication app, IPlatformPaths platformPaths)
{
    // Initialize storage
    await app.Services.InitializeOrbitMeshStorageAsync();

    // Check for updates on startup
    var updateService = app.Services.GetRequiredService<IUpdateService>();
    var updateResult = await updateService.CheckAndApplyUpdateAsync();
    if (updateResult.UpdatePending)
    {
        Log.Information("Update to v{Version} is being applied. Restarting...", updateResult.NewVersion);
        Environment.Exit(0);
    }
    else if (updateResult.UpdateAvailable)
    {
        Log.Information("Update available: v{Version} (auto-update disabled)", updateResult.NewVersion);
    }

    // Configure request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrbitMesh API v1"));
    }

    app.UseRouting();

    // Authentication and authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Serve embedded static files (SPA)
    var embeddedProvider = new ManifestEmbeddedFileProvider(
        typeof(Program).Assembly,
        "wwwroot");

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = embeddedProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = embeddedProvider
    });

    // Map endpoints
    app.MapOrbitMeshHub("/agent");
    app.MapOrbitMeshDashboardHub("/hub/dashboard");
    app.MapControllers();
    app.MapHealthChecks("/health");

    // SPA fallback
    app.MapFallback(async context =>
    {
        var file = embeddedProvider.GetFileInfo("index.html");
        if (file.Exists)
        {
            context.Response.ContentType = "text/html";
            await using var stream = file.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });
}
