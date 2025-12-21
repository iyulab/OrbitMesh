using System.Globalization;
using OrbitMesh.Core.Platform;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Extensions;
using OrbitMesh.Host.Features;
using OrbitMesh.Host.Services.Security;
using OrbitMesh.Storage.Sqlite.Extensions;
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
    Log.Information("Starting OrbitMesh Server");

    // Initialize platform paths
    var platformPaths = new PlatformPaths();
    platformPaths.EnsureDirectoriesExist();
    Log.Information("Data directory: {DataPath}", platformPaths.BasePath);

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

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
        // Reads from OrbitMesh:Features section in appsettings.json
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
    builder.Services.AddSingleton<IPlatformPaths>(platformPaths);

    // Add SQLite storage with persistent path
    var connectionString = builder.Configuration.GetConnectionString("OrbitMesh")
        ?? $"Data Source={platformPaths.DatabasePath}";
    builder.Services.AddOrbitMeshSqliteStorage(connectionString);

    // Add update service
    builder.Services.AddOrbitMeshUpdate(options =>
    {
        options.ProductName = "orbit-host";
        // Configure from appsettings if available
        builder.Configuration.GetSection("OrbitMesh:Update").Bind(options);
    });

    // Add API controllers with JSON enum string serialization
    builder.Services.AddControllers()
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

    // Add admin authentication (reads from env var or appsettings.json)
    builder.Services.AddAdminAuthentication(builder.Configuration);

    // Build the app
    var app = builder.Build();

    // Initialize storage (creates database and tables if needed)
    await app.Services.InitializeOrbitMeshStorageAsync();

    // Check for updates on startup
    var updateService = app.Services.GetRequiredService<IUpdateService>();
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

    // Serve static files (SPA)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Map endpoints
    app.MapOrbitMeshHub("/agent");
    app.MapOrbitMeshDashboardHub("/hub/dashboard");
    app.MapControllers();
    app.MapHealthChecks("/health");

    // SPA fallback - serve index.html for client-side routes
    app.MapFallbackToFile("index.html");

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
