#if WINDOWS
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// Windows tray application context for OrbitMesh Agent.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Func<AgentSettings, CancellationToken, Task> _startAgent;
    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private readonly ToolStripMenuItem _restartMenuItem;
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private readonly ToolStripMenuItem _serverSettingsMenuItem;

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

    public TrayApplicationContext(Func<AgentSettings, CancellationToken, Task> startAgent)
    {
        _startAgent = startAgent;
        _settings = AgentSettings.Load();

        // Create tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(AgentState.Stopped),
            Text = "OrbitMesh Agent - Stopped",
            Visible = true
        };

        // Create menu items
        _startMenuItem = new ToolStripMenuItem("Start", null, OnStartClick) { Enabled = true };
        _stopMenuItem = new ToolStripMenuItem("Stop", null, OnStopClick) { Enabled = false };
        _restartMenuItem = new ToolStripMenuItem("Restart", null, OnRestartClick) { Enabled = false };
        _serverSettingsMenuItem = new ToolStripMenuItem("Server Settings...", null, OnServerSettingsClick);
        _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", null, OnAutoStartClick)
        {
            Checked = AutoStartManager.IsAutoStartEnabled("OrbitMeshAgent")
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_startMenuItem);
        contextMenu.Items.Add(_stopMenuItem);
        contextMenu.Items.Add(_restartMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_serverSettingsMenuItem);
        contextMenu.Items.Add(_autoStartMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += OnServerSettingsClick;

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
            BeginInvoke(() => OnServerSettingsClick(null, EventArgs.Empty));
        }
    }

    private void BeginInvoke(Action action)
    {
        if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _notifyIcon.ContextMenuStrip.BeginInvoke(action);
        }
        else
        {
            action();
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
            _autoStartMenuItem.Checked = true;
            key?.SetValue(firstRunValue, "true");
        }
    }

    private static Icon CreateIcon(AgentState state)
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);

        var color = state switch
        {
            AgentState.Running => Color.FromArgb(0, 153, 76),   // Green
            AgentState.Starting or AgentState.Stopping => Color.FromArgb(255, 165, 0), // Orange
            _ => Color.FromArgb(128, 128, 128)                   // Gray
        };

        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new Pen(Color.White, 2);
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

        BeginInvoke(() =>
        {
            _notifyIcon.Icon = CreateIcon(newState);
            _notifyIcon.Text = $"OrbitMesh Agent - {stateText}";

            _startMenuItem.Enabled = newState == AgentState.Stopped;
            _stopMenuItem.Enabled = newState == AgentState.Running;
            _restartMenuItem.Enabled = newState == AgentState.Running;
            _serverSettingsMenuItem.Enabled = newState == AgentState.Stopped;
        });
    }

    private async Task StartAgentAsync()
    {
        if (_state != AgentState.Stopped)
            return;

        if (!_settings.IsConfigured)
        {
            MessageBox.Show("Please configure server settings first.", "OrbitMesh Agent",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            OnServerSettingsClick(null, EventArgs.Empty);
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
                BeginInvoke(() =>
                {
                    MessageBox.Show($"Agent error: {ex.Message}", "OrbitMesh Agent",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void OnStartClick(object? sender, EventArgs e)
    {
        _ = StartAgentAsync();
    }

    private async void OnStopClick(object? sender, EventArgs e)
    {
        await StopAgentAsync();
    }

    private async void OnRestartClick(object? sender, EventArgs e)
    {
        await StopAgentAsync();
        await Task.Delay(500); // Brief pause
        await StartAgentAsync();
    }

    private async void OnServerSettingsClick(object? sender, EventArgs e)
    {
        // Stop agent before editing settings
        var wasRunning = _state == AgentState.Running;
        if (wasRunning)
        {
            await StopAgentAsync();
        }

        using var form = new ServerSettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = AgentSettings.Load(); // Reload settings

            if (form.SettingsChanged || wasRunning)
            {
                // Start agent with new settings
                if (_settings.IsConfigured)
                {
                    await StartAgentAsync();
                }
            }
        }
        else if (wasRunning && _settings.IsConfigured)
        {
            // Restart with original settings if cancelled
            await StartAgentAsync();
        }
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        var newState = !_autoStartMenuItem.Checked;
        AutoStartManager.SetAutoStart("OrbitMeshAgent", newState);
        _autoStartMenuItem.Checked = newState;
    }

    private async void OnExit(object? sender, EventArgs e)
    {
        _isDisposed = true;

        await StopAgentAsync();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();

            _startMenuItem.Dispose();
            _stopMenuItem.Dispose();
            _restartMenuItem.Dispose();
            _serverSettingsMenuItem.Dispose();
            _autoStartMenuItem.Dispose();

            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
#endif
