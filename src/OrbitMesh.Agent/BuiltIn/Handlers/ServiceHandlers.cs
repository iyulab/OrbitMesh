using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using OrbitMesh.Agent.BuiltIn.Models;
using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Agent.BuiltIn.Handlers;

/// <summary>
/// Handler for service start command.
/// </summary>
public sealed class ServiceStartHandler : IRequestResponseHandler<ServiceControlResult>
{
    private readonly ILogger<ServiceStartHandler> _logger;

    public string Command => Commands.Service.Start;

    public ServiceStartHandler(ILogger<ServiceStartHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceControlResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<ServiceControlRequest>();

        _logger.LogInformation("Starting service: {ServiceName}", request.ServiceName);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await StartWindowsServiceAsync(request, cancellationToken);
            }
            else
            {
                return await StartSystemdServiceAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service: {ServiceName}", request.ServiceName);
            return new ServiceControlResult
            {
                Success = false,
                ServiceName = request.ServiceName,
                State = ServiceState.Unknown,
                Error = ex.Message
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<ServiceControlResult> StartWindowsServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        using var controller = new ServiceController(request.ServiceName);

        if (controller.Status == ServiceControllerStatus.Running)
        {
            return new ServiceControlResult
            {
                Success = true,
                ServiceName = request.ServiceName,
                State = ServiceState.Running
            };
        }

        controller.Start();
        await Task.Run(() => controller.WaitForStatus(
            ServiceControllerStatus.Running,
            TimeSpan.FromSeconds(request.TimeoutSeconds)), cancellationToken);

        return new ServiceControlResult
        {
            Success = controller.Status == ServiceControllerStatus.Running,
            ServiceName = request.ServiceName,
            State = MapWindowsStatus(controller.Status)
        };
    }

    private static async Task<ServiceControlResult> StartSystemdServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        var result = await RunSystemctlAsync("start", request.ServiceName, request.TimeoutSeconds, cancellationToken);

        if (result.Success)
        {
            var status = await GetSystemdStatusAsync(request.ServiceName, cancellationToken);
            return new ServiceControlResult
            {
                Success = true,
                ServiceName = request.ServiceName,
                State = status
            };
        }

        return new ServiceControlResult
        {
            Success = false,
            ServiceName = request.ServiceName,
            State = ServiceState.Unknown,
            Error = result.Error
        };
    }

    [SupportedOSPlatform("windows")]
    private static ServiceState MapWindowsStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceState.Stopped,
            ServiceControllerStatus.StartPending => ServiceState.Starting,
            ServiceControllerStatus.Running => ServiceState.Running,
            ServiceControllerStatus.StopPending => ServiceState.Stopping,
            ServiceControllerStatus.Paused => ServiceState.Paused,
            _ => ServiceState.Unknown
        };
    }

    private static async Task<(bool Success, string? Error)> RunSystemctlAsync(
        string action, string serviceName, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { action, serviceName },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        process.Start();
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode == 0)
        {
            return (true, null);
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        return (false, stderr);
    }

    private static async Task<ServiceState> GetSystemdStatusAsync(string serviceName, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { "is-active", serviceName },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return output.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => ServiceState.Running,
            "INACTIVE" => ServiceState.Stopped,
            "ACTIVATING" => ServiceState.Starting,
            "DEACTIVATING" => ServiceState.Stopping,
            _ => ServiceState.Unknown
        };
    }
}

/// <summary>
/// Handler for service stop command.
/// </summary>
public sealed class ServiceStopHandler : IRequestResponseHandler<ServiceControlResult>
{
    private readonly ILogger<ServiceStopHandler> _logger;

    public string Command => Commands.Service.Stop;

    public ServiceStopHandler(ILogger<ServiceStopHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceControlResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<ServiceControlRequest>();

        _logger.LogInformation("Stopping service: {ServiceName}", request.ServiceName);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await StopWindowsServiceAsync(request, cancellationToken);
            }
            else
            {
                return await StopSystemdServiceAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service: {ServiceName}", request.ServiceName);
            return new ServiceControlResult
            {
                Success = false,
                ServiceName = request.ServiceName,
                State = ServiceState.Unknown,
                Error = ex.Message
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<ServiceControlResult> StopWindowsServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        using var controller = new ServiceController(request.ServiceName);

        if (controller.Status == ServiceControllerStatus.Stopped)
        {
            return new ServiceControlResult
            {
                Success = true,
                ServiceName = request.ServiceName,
                State = ServiceState.Stopped
            };
        }

        controller.Stop();
        await Task.Run(() => controller.WaitForStatus(
            ServiceControllerStatus.Stopped,
            TimeSpan.FromSeconds(request.TimeoutSeconds)), cancellationToken);

        return new ServiceControlResult
        {
            Success = controller.Status == ServiceControllerStatus.Stopped,
            ServiceName = request.ServiceName,
            State = MapWindowsStatus(controller.Status)
        };
    }

    private static async Task<ServiceControlResult> StopSystemdServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { "stop", request.ServiceName },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        process.Start();
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode == 0)
        {
            return new ServiceControlResult
            {
                Success = true,
                ServiceName = request.ServiceName,
                State = ServiceState.Stopped
            };
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        return new ServiceControlResult
        {
            Success = false,
            ServiceName = request.ServiceName,
            State = ServiceState.Unknown,
            Error = stderr
        };
    }

    [SupportedOSPlatform("windows")]
    private static ServiceState MapWindowsStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceState.Stopped,
            ServiceControllerStatus.StartPending => ServiceState.Starting,
            ServiceControllerStatus.Running => ServiceState.Running,
            ServiceControllerStatus.StopPending => ServiceState.Stopping,
            ServiceControllerStatus.Paused => ServiceState.Paused,
            _ => ServiceState.Unknown
        };
    }
}

/// <summary>
/// Handler for service restart command.
/// </summary>
public sealed class ServiceRestartHandler : IRequestResponseHandler<ServiceControlResult>
{
    private readonly ILogger<ServiceRestartHandler> _logger;

    public string Command => Commands.Service.Restart;

    public ServiceRestartHandler(ILogger<ServiceRestartHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceControlResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<ServiceControlRequest>();

        _logger.LogInformation("Restarting service: {ServiceName}", request.ServiceName);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await RestartWindowsServiceAsync(request, cancellationToken);
            }
            else
            {
                return await RestartSystemdServiceAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service: {ServiceName}", request.ServiceName);
            return new ServiceControlResult
            {
                Success = false,
                ServiceName = request.ServiceName,
                State = ServiceState.Unknown,
                Error = ex.Message
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<ServiceControlResult> RestartWindowsServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        using var controller = new ServiceController(request.ServiceName);
        var halfTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds / 2);

        if (controller.Status == ServiceControllerStatus.Running)
        {
            controller.Stop();
            await Task.Run(() => controller.WaitForStatus(
                ServiceControllerStatus.Stopped, halfTimeout), cancellationToken);
        }

        controller.Start();
        await Task.Run(() => controller.WaitForStatus(
            ServiceControllerStatus.Running, halfTimeout), cancellationToken);

        return new ServiceControlResult
        {
            Success = controller.Status == ServiceControllerStatus.Running,
            ServiceName = request.ServiceName,
            State = MapWindowsStatus(controller.Status)
        };
    }

    private static async Task<ServiceControlResult> RestartSystemdServiceAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { "restart", request.ServiceName },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        process.Start();
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode == 0)
        {
            return new ServiceControlResult
            {
                Success = true,
                ServiceName = request.ServiceName,
                State = ServiceState.Running
            };
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        return new ServiceControlResult
        {
            Success = false,
            ServiceName = request.ServiceName,
            State = ServiceState.Unknown,
            Error = stderr
        };
    }

    [SupportedOSPlatform("windows")]
    private static ServiceState MapWindowsStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceState.Stopped,
            ServiceControllerStatus.StartPending => ServiceState.Starting,
            ServiceControllerStatus.Running => ServiceState.Running,
            ServiceControllerStatus.StopPending => ServiceState.Stopping,
            ServiceControllerStatus.Paused => ServiceState.Paused,
            _ => ServiceState.Unknown
        };
    }
}

/// <summary>
/// Handler for service status command.
/// </summary>
public sealed class ServiceStatusHandler : IRequestResponseHandler<ServiceControlResult>
{
    public string Command => Commands.Service.Status;

    public async Task<ServiceControlResult> HandleAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var request = context.GetRequiredParameter<ServiceControlRequest>();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsServiceStatus(request);
            }
            else
            {
                return await GetSystemdServiceStatusAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return new ServiceControlResult
            {
                Success = false,
                ServiceName = request.ServiceName,
                State = ServiceState.Unknown,
                Error = ex.Message
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private static ServiceControlResult GetWindowsServiceStatus(ServiceControlRequest request)
    {
        using var controller = new ServiceController(request.ServiceName);

        return new ServiceControlResult
        {
            Success = true,
            ServiceName = request.ServiceName,
            State = controller.Status switch
            {
                ServiceControllerStatus.Stopped => ServiceState.Stopped,
                ServiceControllerStatus.StartPending => ServiceState.Starting,
                ServiceControllerStatus.Running => ServiceState.Running,
                ServiceControllerStatus.StopPending => ServiceState.Stopping,
                ServiceControllerStatus.Paused => ServiceState.Paused,
                _ => ServiceState.Unknown
            }
        };
    }

    private static async Task<ServiceControlResult> GetSystemdServiceStatusAsync(ServiceControlRequest request, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { "is-active", request.ServiceName },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var state = output.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => ServiceState.Running,
            "INACTIVE" => ServiceState.Stopped,
            "ACTIVATING" => ServiceState.Starting,
            "DEACTIVATING" => ServiceState.Stopping,
            _ => ServiceState.Unknown
        };

        return new ServiceControlResult
        {
            Success = true,
            ServiceName = request.ServiceName,
            State = state
        };
    }
}
