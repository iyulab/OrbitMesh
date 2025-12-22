#if WINDOWS
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// WPF-based tray application for OrbitMesh Agent.
/// </summary>
internal sealed class TrayApplication : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly Func<AgentSettings, CancellationToken, Task> _startAgent;
    private readonly MenuItem _startMenuItem;
    private readonly MenuItem _stopMenuItem;
    private readonly MenuItem _restartMenuItem;
    private readonly MenuItem _autoStartMenuItem;
    private readonly MenuItem _serverSettingsMenuItem;

    private CancellationTokenSource? _cts;
    private Task? _agentTask;
    private AgentSettings _settings;
    private AgentState _state = AgentState.Stopped;
    private bool _isDisposed;

    private enum AgentState
    {
        Stopped,
        Starting,
        Running,
        Stopping
    }

    public TrayApplication(Func<AgentSettings, CancellationToken, Task> startAgent)
    {
        _startAgent = startAgent;
        _settings = AgentSettings.Load();

        // Create menu items
        _startMenuItem = new MenuItem { Header = "Start" };
        _startMenuItem.Click += OnStartClick;

        _stopMenuItem = new MenuItem { Header = "Stop", IsEnabled = false };
        _stopMenuItem.Click += OnStopClick;

        _restartMenuItem = new MenuItem { Header = "Restart", IsEnabled = false };
        _restartMenuItem.Click += OnRestartClick;

        _serverSettingsMenuItem = new MenuItem { Header = "Server Settings..." };
        _serverSettingsMenuItem.Click += OnServerSettingsClick;

        _autoStartMenuItem = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = AutoStartManager.IsAutoStartEnabled("OrbitMeshAgent")
        };
        _autoStartMenuItem.Click += OnAutoStartClick;

        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += OnExit;

        // Create context menu
        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(_startMenuItem);
        contextMenu.Items.Add(_stopMenuItem);
        contextMenu.Items.Add(_restartMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(_serverSettingsMenuItem);
        contextMenu.Items.Add(_autoStartMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitMenuItem);

        // Create tray icon
        _notifyIcon = new TaskbarIcon
        {
            Icon = CreateIcon(AgentState.Stopped),
            ToolTipText = "OrbitMesh Agent - Stopped",
            ContextMenu = contextMenu
        };
        _notifyIcon.TrayMouseDoubleClick += (_, _) => OnServerSettingsClick(null, null!);

        // Enable auto-start on first run
        EnableAutoStartOnFirstRun();

        // Auto-start if configured
        if (_settings.IsConfigured)
        {
            _ = StartAgentAsync();
        }
        else
        {
            // Show settings dialog on first run if not configured
            _ = Application.Current.Dispatcher.BeginInvoke(() => OnServerSettingsClick(null, null!));
        }
    }

    private void EnableAutoStartOnFirstRun()
    {
        const string firstRunKey = @"Software\OrbitMesh";
        const string firstRunValue = "AgentFirstRunCompleted";

        using var key = Registry.CurrentUser.CreateSubKey(firstRunKey);
        if (key?.GetValue(firstRunValue) == null)
        {
            AutoStartManager.SetAutoStart("OrbitMeshAgent", true);
            _autoStartMenuItem.IsChecked = true;
            key?.SetValue(firstRunValue, "true");
        }
    }

    private static Icon CreateIcon(AgentState state)
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Transparent);

        var color = state switch
        {
            AgentState.Running => System.Drawing.Color.FromArgb(0, 153, 76),     // Green
            AgentState.Starting or AgentState.Stopping => System.Drawing.Color.FromArgb(255, 165, 0), // Orange
            _ => System.Drawing.Color.FromArgb(128, 128, 128)                     // Gray
        };

        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
        g.DrawEllipse(pen, 8, 8, 16, 16);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void UpdateState(AgentState newState)
    {
        _state = newState;

        var stateText = newState switch
        {
            AgentState.Running => "Running",
            AgentState.Starting => "Starting...",
            AgentState.Stopping => "Stopping...",
            _ => "Stopped"
        };

        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _notifyIcon.Icon = CreateIcon(newState);
            _notifyIcon.ToolTipText = $"OrbitMesh Agent - {stateText}";

            _startMenuItem.IsEnabled = newState == AgentState.Stopped;
            _stopMenuItem.IsEnabled = newState == AgentState.Running;
            _restartMenuItem.IsEnabled = newState == AgentState.Running;
            _serverSettingsMenuItem.IsEnabled = newState == AgentState.Stopped;
        });
    }

    private async Task StartAgentAsync()
    {
        if (_state != AgentState.Stopped)
            return;

        if (!_settings.IsConfigured)
        {
            MessageBox.Show("Please configure server settings first.", "OrbitMesh Agent",
                MessageBoxButton.OK, MessageBoxImage.Information);
            OnServerSettingsClick(null, null!);
            return;
        }

        UpdateState(AgentState.Starting);
        _cts = new CancellationTokenSource();

        _agentTask = Task.Run(async () =>
        {
            try
            {
                UpdateState(AgentState.Running);
                await _startAgent(_settings, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show($"Agent error: {ex.Message}", "OrbitMesh Agent",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                UpdateState(AgentState.Stopped);
            }
        });
    }

    private async Task StopAgentAsync()
    {
        if (_state != AgentState.Running || _cts == null)
            return;

        UpdateState(AgentState.Stopping);
        await _cts.CancelAsync();

        if (_agentTask != null)
        {
            try
            {
                await _agentTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                // Force stop after timeout
            }
        }

        _cts.Dispose();
        _cts = null;
        _agentTask = null;
        UpdateState(AgentState.Stopped);
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        _ = StartAgentAsync();
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        await StopAgentAsync();
    }

    private async void OnRestartClick(object sender, RoutedEventArgs e)
    {
        await StopAgentAsync();
        await Task.Delay(500);
        await StartAgentAsync();
    }

    private async void OnServerSettingsClick(object? sender, RoutedEventArgs e)
    {
        var wasRunning = _state == AgentState.Running;
        if (wasRunning)
        {
            await StopAgentAsync();
        }

        var window = new ServerSettingsWindow(_settings);
        var result = window.ShowDialog();

        if (result == true)
        {
            _settings = AgentSettings.Load();

            if (window.SettingsChanged || wasRunning)
            {
                if (_settings.IsConfigured)
                {
                    await StartAgentAsync();
                }
            }
        }
        else if (wasRunning && _settings.IsConfigured)
        {
            await StartAgentAsync();
        }
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        var newState = _autoStartMenuItem.IsChecked;
        AutoStartManager.SetAutoStart("OrbitMeshAgent", newState);
    }

    private async void OnExit(object sender, RoutedEventArgs e)
    {
        _isDisposed = true;
        await StopAgentAsync();
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
#endif
