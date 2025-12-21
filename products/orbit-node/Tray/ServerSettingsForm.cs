#if WINDOWS
using System.Drawing;
using System.Windows.Forms;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// Dialog for configuring server connection settings.
/// </summary>
internal sealed class ServerSettingsForm : Form
{
    private readonly TextBox _serverUrlTextBox;
    private readonly TextBox _bootstrapTokenTextBox;
    private readonly TextBox _agentNameTextBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly AgentSettings _settings;
    private bool _disposed;

    public bool SettingsChanged { get; private set; }

    public ServerSettingsForm(AgentSettings settings)
    {
        _settings = settings;

        Text = "OrbitMesh Agent - Server Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 240);
        Font = new Font("Segoe UI", 9F);

        // Server URL Label
        var serverUrlLabel = new Label
        {
            Text = "Server URL:",
            Location = new Point(20, 25),
            Size = new Size(100, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(serverUrlLabel);

        // Server URL TextBox
        _serverUrlTextBox = new TextBox
        {
            Location = new Point(130, 22),
            Size = new Size(270, 23),
            Text = settings.ServerUrl
        };
        Controls.Add(_serverUrlTextBox);

        // Server URL Hint
        var serverUrlHint = new Label
        {
            Text = "예: http://192.168.1.100:5000/agent",
            Location = new Point(130, 47),
            Size = new Size(270, 16),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F)
        };
        Controls.Add(serverUrlHint);

        // Bootstrap Token Label
        var tokenLabel = new Label
        {
            Text = "Bootstrap Token:",
            Location = new Point(20, 75),
            Size = new Size(100, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(tokenLabel);

        // Bootstrap Token TextBox
        _bootstrapTokenTextBox = new TextBox
        {
            Location = new Point(130, 72),
            Size = new Size(270, 23),
            Text = settings.BootstrapToken,
            UseSystemPasswordChar = true
        };
        Controls.Add(_bootstrapTokenTextBox);

        // Bootstrap Token Hint
        var tokenHint = new Label
        {
            Text = "서버 대시보드에서 토큰 복사",
            Location = new Point(130, 97),
            Size = new Size(270, 16),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F)
        };
        Controls.Add(tokenHint);

        // Agent Name Label
        var agentNameLabel = new Label
        {
            Text = "Agent Name:",
            Location = new Point(20, 125),
            Size = new Size(100, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(agentNameLabel);

        // Agent Name TextBox
        _agentNameTextBox = new TextBox
        {
            Location = new Point(130, 122),
            Size = new Size(270, 23),
            Text = settings.AgentName
        };
        Controls.Add(_agentNameTextBox);

        // Agent Name Hint
        var agentNameHint = new Label
        {
            Text = $"비워두면 '{Environment.MachineName}' 사용",
            Location = new Point(130, 147),
            Size = new Size(270, 16),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F)
        };
        Controls.Add(agentNameHint);

        // Save Button
        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(220, 190),
            Size = new Size(85, 30),
            DialogResult = DialogResult.OK
        };
        _saveButton.Click += OnSaveClick;
        Controls.Add(_saveButton);

        // Cancel Button
        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(315, 190),
            Size = new Size(85, 30),
            DialogResult = DialogResult.Cancel
        };
        _cancelButton.Click += (_, _) => Close();
        Controls.Add(_cancelButton);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        var serverUrl = _serverUrlTextBox.Text.Trim();
        var bootstrapToken = _bootstrapTokenTextBox.Text.Trim();
        var agentName = _agentNameTextBox.Text.Trim();

        // Validate server URL
        if (string.IsNullOrEmpty(serverUrl))
        {
            MessageBox.Show("Server URL is required.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _serverUrlTextBox.Focus();
            DialogResult = DialogResult.None;
            return;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show("Please enter a valid HTTP or HTTPS URL.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _serverUrlTextBox.Focus();
            DialogResult = DialogResult.None;
            return;
        }

        // Ensure URL ends with /agent
        if (!serverUrl.EndsWith("/agent", StringComparison.OrdinalIgnoreCase))
        {
            if (!serverUrl.EndsWith('/'))
            {
                serverUrl += "/";
            }
            serverUrl += "agent";
            _serverUrlTextBox.Text = serverUrl;
        }

        // Check if settings changed
        SettingsChanged = serverUrl != _settings.ServerUrl ||
                          bootstrapToken != _settings.BootstrapToken ||
                          agentName != _settings.AgentName;

        // Update settings
        _settings.ServerUrl = serverUrl;
        _settings.BootstrapToken = bootstrapToken;
        _settings.AgentName = agentName;
        _settings.Save();

        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _serverUrlTextBox.Dispose();
            _bootstrapTokenTextBox.Dispose();
            _agentNameTextBox.Dispose();
            _saveButton.Dispose();
            _cancelButton.Dispose();
        }
        base.Dispose(disposing);
    }
}
#endif
