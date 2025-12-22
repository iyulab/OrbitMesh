#if WINDOWS
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace OrbitMesh.Products.Server.Tray;

/// <summary>
/// WPF-based tray application for OrbitMesh Server.
/// </summary>
internal sealed class TrayApplication : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;
    private readonly string _dashboardUrl;
    private readonly MenuItem _autoStartMenuItem;
    private bool _isDisposed;

    public TrayApplication(Func<CancellationToken, Task> startServer, string dashboardUrl = "http://localhost:5000")
    {
        _cts = new CancellationTokenSource();
        _dashboardUrl = dashboardUrl;

        // Create menu items
        var openDashboardMenuItem = new MenuItem { Header = "Open Dashboard" };
        openDashboardMenuItem.Click += OnOpenDashboard;

        _autoStartMenuItem = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = AutoStartManager.IsAutoStartEnabled("OrbitMeshServer")
        };
        _autoStartMenuItem.Click += OnAutoStartClick;

        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += OnExit;

        // Create context menu
        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(openDashboardMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(_autoStartMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitMenuItem);

        // Create tray icon
        _notifyIcon = new TaskbarIcon
        {
            Icon = CreateDefaultIcon(),
            ToolTipText = "OrbitMesh Server",
            ContextMenu = contextMenu
        };
        _notifyIcon.TrayMouseDoubleClick += (_, _) => OnOpenDashboard(null, null!);

        // Start the server
        _serverTask = Task.Run(async () =>
        {
            try
            {
                await startServer(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show($"Server error: {ex.Message}", "OrbitMesh Server",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });

        // Enable auto-start on first run
        EnableAutoStartOnFirstRun();
    }

    private void EnableAutoStartOnFirstRun()
    {
        const string firstRunKey = @"Software\OrbitMesh";
        const string firstRunValue = "FirstRunCompleted";

        using var key = Registry.CurrentUser.CreateSubKey(firstRunKey);
        if (key?.GetValue(firstRunValue) == null)
        {
            // First run - enable auto-start
            AutoStartManager.SetAutoStart("OrbitMeshServer", true);
            _autoStartMenuItem.IsChecked = true;
            key?.SetValue(firstRunValue, "true");
        }
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple icon programmatically
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(0, 122, 204)); // Blue
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
        g.DrawEllipse(pen, 8, 8, 16, 16);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnOpenDashboard(object? sender, RoutedEventArgs? e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _dashboardUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open browser: {ex.Message}", "OrbitMesh Server",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        var newState = _autoStartMenuItem.IsChecked;
        AutoStartManager.SetAutoStart("OrbitMeshServer", newState);
    }

    private async void OnExit(object sender, RoutedEventArgs e)
    {
        _isDisposed = true;
        await _cts.CancelAsync();
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
#endif
