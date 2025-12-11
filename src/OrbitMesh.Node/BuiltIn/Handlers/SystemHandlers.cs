using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OrbitMesh.Node.BuiltIn.Models;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Node.BuiltIn.Handlers;

/// <summary>
/// Handler for health check command.
/// </summary>
public sealed class HealthCheckHandler : IRequestResponseHandler<HealthCheckResult>
{
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly IEnumerable<IHealthCheck> _healthChecks;
    private readonly ILogger<HealthCheckHandler> _logger;

    public string Command => Commands.System.HealthCheck;

    public HealthCheckHandler(
        IEnumerable<IHealthCheck> healthChecks,
        ILogger<HealthCheckHandler> logger)
    {
        _healthChecks = healthChecks;
        _logger = logger;
    }

    public async Task<HealthCheckResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, HealthCheckEntry>();
        var isHealthy = true;
        string? error = null;

        foreach (var check in _healthChecks)
        {
            try
            {
                var entry = await check.CheckAsync(cancellationToken);
                checks[check.Name] = entry;
                if (!entry.IsHealthy)
                {
                    isHealthy = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check '{Name}' failed", check.Name);
                checks[check.Name] = new HealthCheckEntry
                {
                    IsHealthy = false,
                    Description = ex.Message
                };
                isHealthy = false;
                error ??= ex.Message;
            }
        }

        return new HealthCheckResult
        {
            IsHealthy = isHealthy,
            Uptime = DateTimeOffset.UtcNow - _startTime,
            Checks = checks,
            Error = error
        };
    }
}

/// <summary>
/// Handler for version info command.
/// </summary>
public sealed class VersionHandler : IRequestResponseHandler<VersionInfo>
{
    private readonly string _appVersion;

    public string Command => Commands.System.Version;

    public VersionHandler(string appVersion = "1.0.0")
    {
        _appVersion = appVersion;
    }

    public Task<VersionInfo> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var result = new VersionInfo
        {
            Version = _appVersion,
            SdkVersion = typeof(VersionHandler).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Platform = RuntimeInformation.RuntimeIdentifier,
            Hostname = Environment.MachineName
        };

        return Task.FromResult(result);
    }
}

/// <summary>
/// Handler for system metrics command.
/// </summary>
public sealed class MetricsHandler : IRequestResponseHandler<SystemMetrics>
{
    public string Command => Commands.System.Metrics;

    public Task<SystemMetrics> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();

        var disks = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new DiskInfo
            {
                Name = d.Name,
                TotalBytes = d.TotalSize,
                AvailableBytes = d.AvailableFreeSpace,
                UsagePercent = 100.0 * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize
            })
            .ToList();

        var gcInfo = GC.GetGCMemoryInfo();

        var result = new SystemMetrics
        {
            CpuUsagePercent = 0, // Would need PerformanceCounter for accurate CPU
            TotalMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
            AvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes - process.WorkingSet64,
            MemoryUsagePercent = 100.0 * process.WorkingSet64 / gcInfo.TotalAvailableMemoryBytes,
            ProcessMemoryBytes = process.WorkingSet64,
            Disks = disks,
            Timestamp = DateTimeOffset.UtcNow
        };

        return Task.FromResult(result);
    }
}

/// <summary>
/// Handler for ping command.
/// </summary>
public sealed class PingHandler : IRequestResponseHandler<PingResult>
{
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    public string Command => Commands.System.Ping;

    public Task<PingResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PingResult
        {
            AgentId = context.AgentId,
            Timestamp = DateTimeOffset.UtcNow,
            Uptime = DateTimeOffset.UtcNow - _startTime
        });
    }
}

/// <summary>
/// Handler for execute command (shell execution).
/// </summary>
public sealed class ExecuteHandler : IRequestResponseHandler<ExecuteResult>
{
    private readonly bool _enabled;
    private readonly ILogger<ExecuteHandler> _logger;

    public string Command => Commands.System.Execute;

    public ExecuteHandler(bool enabled, ILogger<ExecuteHandler> logger)
    {
        _enabled = enabled;
        _logger = logger;
    }

    public async Task<ExecuteResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return new ExecuteResult
            {
                Success = false,
                ExitCode = -1,
                Error = "Shell execution is disabled on this agent"
            };
        }

        var request = context.GetRequiredParameter<ExecuteRequest>();

        _logger.LogInformation("Executing command: {Command} {Args}",
            request.Command,
            request.Arguments != null ? string.Join(" ", request.Arguments) : "");

        var startInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (request.Arguments != null)
        {
            foreach (var arg in request.Arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        if (!string.IsNullOrEmpty(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        if (request.Environment != null)
        {
            foreach (var (key, value) in request.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var process = new Process { StartInfo = startInfo };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            stopwatch.Stop();

            return new ExecuteResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = await stdoutTask,
                StandardError = await stderrTask,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecuteResult
            {
                Success = false,
                ExitCode = -1,
                Error = "Command execution timed out",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command}", request.Command);
            return new ExecuteResult
            {
                Success = false,
                ExitCode = -1,
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }
}

/// <summary>
/// Interface for health check implementations.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Health check name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Perform the health check.
    /// </summary>
    Task<HealthCheckEntry> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default health check that always passes.
/// </summary>
public sealed class DefaultHealthCheck : IHealthCheck
{
    public string Name => "default";

    public Task<HealthCheckEntry> CheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthCheckEntry
        {
            IsHealthy = true,
            Description = "Agent is running"
        });
    }
}
