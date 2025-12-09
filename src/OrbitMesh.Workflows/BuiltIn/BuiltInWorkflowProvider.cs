using System.Reflection;
using OrbitMesh.Workflows.Models;
using OrbitMesh.Workflows.Parsing;

namespace OrbitMesh.Workflows.BuiltIn;

/// <summary>
/// Provides access to built-in workflow templates.
/// </summary>
public sealed class BuiltInWorkflowProvider
{
    private readonly WorkflowParser _parser;
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    /// <summary>
    /// Initializes a new instance of BuiltInWorkflowProvider.
    /// </summary>
    public BuiltInWorkflowProvider()
    {
        _parser = new WorkflowParser();
        _assembly = typeof(BuiltInWorkflowProvider).Assembly;
        _resourcePrefix = "OrbitMesh.Workflows.BuiltIn.Templates.";
    }

    /// <summary>
    /// Gets a list of all available built-in workflow template names.
    /// </summary>
    public IReadOnlyList<string> GetAvailableTemplates()
    {
        return _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(_resourcePrefix, StringComparison.OrdinalIgnoreCase)
                     && n.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .Select(n => n[_resourcePrefix.Length..^5]) // Remove prefix and .yaml
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Loads a built-in workflow template by name.
    /// </summary>
    /// <param name="templateName">Name of the template (without extension).</param>
    /// <returns>The parsed workflow definition.</returns>
    /// <exception cref="ArgumentException">Template not found.</exception>
    public WorkflowDefinition LoadTemplate(string templateName)
    {
        var yaml = LoadTemplateYaml(templateName);
        return _parser.Parse(yaml);
    }

    /// <summary>
    /// Loads a built-in workflow template by name asynchronously.
    /// </summary>
    /// <param name="templateName">Name of the template (without extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed workflow definition.</returns>
    public async Task<WorkflowDefinition> LoadTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var yaml = await LoadTemplateYamlAsync(templateName, cancellationToken);
        return _parser.Parse(yaml);
    }

    /// <summary>
    /// Loads the raw YAML content of a built-in template.
    /// </summary>
    /// <param name="templateName">Name of the template (without extension).</param>
    /// <returns>The YAML content.</returns>
    public string LoadTemplateYaml(string templateName)
    {
        var resourceName = $"{_resourcePrefix}{templateName}.yaml";
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException($"Built-in template '{templateName}' not found. Available: {string.Join(", ", GetAvailableTemplates())}", nameof(templateName));

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Loads the raw YAML content of a built-in template asynchronously.
    /// </summary>
    public async Task<string> LoadTemplateYamlAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var resourceName = $"{_resourcePrefix}{templateName}.yaml";
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException($"Built-in template '{templateName}' not found. Available: {string.Join(", ", GetAvailableTemplates())}", nameof(templateName));

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Loads all built-in workflow templates.
    /// </summary>
    /// <returns>Dictionary of template name to workflow definition.</returns>
    public IReadOnlyDictionary<string, WorkflowDefinition> LoadAllTemplates()
    {
        var templates = new Dictionary<string, WorkflowDefinition>();

        foreach (var name in GetAvailableTemplates())
        {
            try
            {
                templates[name] = LoadTemplate(name);
            }
            catch (WorkflowParseException)
            {
                // Skip invalid templates
            }
        }

        return templates.AsReadOnly();
    }

    /// <summary>
    /// Loads all built-in workflow templates asynchronously.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, WorkflowDefinition>> LoadAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new Dictionary<string, WorkflowDefinition>();

        foreach (var name in GetAvailableTemplates())
        {
            try
            {
                templates[name] = await LoadTemplateAsync(name, cancellationToken);
            }
            catch (WorkflowParseException)
            {
                // Skip invalid templates
            }
        }

        return templates.AsReadOnly();
    }

    /// <summary>
    /// Gets the health check workflow template.
    /// </summary>
    public WorkflowDefinition HealthCheck => LoadTemplate("health-check");

    /// <summary>
    /// Gets the file deployment workflow template.
    /// </summary>
    public WorkflowDefinition FileDeployment => LoadTemplate("file-deployment");

    /// <summary>
    /// Gets the service restart workflow template.
    /// </summary>
    public WorkflowDefinition ServiceRestart => LoadTemplate("service-restart");

    /// <summary>
    /// Gets the agent update workflow template.
    /// </summary>
    public WorkflowDefinition AgentUpdate => LoadTemplate("agent-update");

    /// <summary>
    /// Gets the rolling deployment workflow template.
    /// </summary>
    public WorkflowDefinition RollingDeployment => LoadTemplate("rolling-deployment");

    /// <summary>
    /// Gets the file sync workflow template.
    /// </summary>
    public WorkflowDefinition FileSync => LoadTemplate("file-sync");

    /// <summary>
    /// Gets the alert on failure workflow template.
    /// </summary>
    public WorkflowDefinition AlertOnFailure => LoadTemplate("alert-on-failure");
}
