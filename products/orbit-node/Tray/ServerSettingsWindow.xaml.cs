#if WINDOWS
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace OrbitMesh.Products.Agent.Tray;

/// <summary>
/// WPF dialog for configuring server connection settings.
/// </summary>
internal sealed partial class ServerSettingsWindow : Window
{
    private readonly AgentSettings _settings;

    public bool SettingsChanged { get; private set; }

    public ServerSettingsWindow(AgentSettings settings)
    {
        InitializeComponent();

        _settings = settings;

        // Load current values
        ServerUrlTextBox.Text = settings.ServerUrl ?? string.Empty;
        BootstrapTokenBox.Password = settings.BootstrapToken ?? string.Empty;
        AgentNameTextBox.Text = settings.AgentName ?? string.Empty;
        AgentNameHint.Text = $"비워두면 '{Environment.MachineName}' 사용";

        // Focus on first empty field
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(ServerUrlTextBox.Text))
                ServerUrlTextBox.Focus();
            else if (string.IsNullOrEmpty(BootstrapTokenBox.Password))
                BootstrapTokenBox.Focus();
            else
                ServerUrlTextBox.Focus();
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var serverUrl = ServerUrlTextBox.Text.Trim();
        var bootstrapToken = BootstrapTokenBox.Password.Trim();
        var agentName = AgentNameTextBox.Text.Trim();

        // Validate server URL
        if (string.IsNullOrEmpty(serverUrl))
        {
            ShowError("Server URL is required.");
            ServerUrlTextBox.Focus();
            return;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            ShowError("Please enter a valid HTTP or HTTPS URL.");
            ServerUrlTextBox.Focus();
            return;
        }

        // Ensure URL ends with /agent
        if (!serverUrl.EndsWith("/agent", StringComparison.OrdinalIgnoreCase))
        {
            serverUrl = serverUrl.TrimEnd('/') + "/agent";
            ServerUrlTextBox.Text = serverUrl;
        }

        // Check if settings changed
        SettingsChanged = serverUrl != _settings.ServerUrl ||
                          bootstrapToken != _settings.BootstrapToken ||
                          agentName != _settings.AgentName;

        // Update and save settings
        _settings.ServerUrl = serverUrl;
        _settings.BootstrapToken = bootstrapToken;
        _settings.AgentName = agentName;
        _settings.Save();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static void ShowError(string message)
    {
        WpfMessageBox.Show(message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
#endif
