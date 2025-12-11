using System.Globalization;
using OrbitMesh.Host.Extensions;
using OrbitMesh.Storage.Sqlite.Extensions;
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

    // Add SQLite storage (for production persistence)
    var connectionString = builder.Configuration.GetConnectionString("OrbitMesh")
        ?? "Data Source=orbitmesh.db";
    builder.Services.AddOrbitMeshSqliteStorage(connectionString);

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

    // Build the app
    var app = builder.Build();

    // Configure request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrbitMesh API v1"));
    }

    app.UseRouting();

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
