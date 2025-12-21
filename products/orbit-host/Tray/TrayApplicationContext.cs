#if WINDOWS
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OrbitMesh.Products.Server.Tray;

/// <summary>
/// Windows tray application context for OrbitMesh Server.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;
    private readonly string _dashboardUrl;
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private bool _isRunning = true;

    public TrayApplicationContext(Func<CancellationToken, Task> startServer, string dashboardUrl = "http://localhost:5000")
    {
        _cts = new CancellationTokenSource();
        _dashboardUrl = dashboardUrl;

        // Create tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "OrbitMesh Server",
            Visible = true
        };

        // Create context menu
        _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", null, OnAutoStartClick)
        {
            Checked = AutoStartManager.IsAutoStartEnabled("OrbitMeshServer")
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Dashboard", null, OnOpenDashboard);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_autoStartMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += OnOpenDashboard;

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
                MessageBox.Show($"Server error: {ex.Message}", "OrbitMesh Server",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _autoStartMenuItem.Checked = true;
            key?.SetValue(firstRunValue, "true");
        }
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple icon programmatically
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(0, 122, 204)); // Blue
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new Pen(Color.White, 2);
        g.DrawEllipse(pen, 8, 8, 16, 16);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
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
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        var newState = !_autoStartMenuItem.Checked;
        AutoStartManager.SetAutoStart("OrbitMeshServer", newState);
        _autoStartMenuItem.Checked = newState;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _isRunning = false;
        _cts.Cancel();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isRunning)
            {
                _cts.Cancel();
            }
            _autoStartMenuItem.Dispose();
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
#endif
