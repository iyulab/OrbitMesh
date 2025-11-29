using OrbitMesh.Core.Contracts;

namespace OrbitMesh.Agent;

/// <summary>
/// Registry for command handlers.
/// </summary>
public interface IHandlerRegistry
{
    /// <summary>
    /// Registers a handler for a command.
    /// </summary>
    void Register(string command, IMeshHandler handler);

    /// <summary>
    /// Gets the handler for a command.
    /// </summary>
    IMeshHandler? GetHandler(string command);

    /// <summary>
    /// Gets all registered command names.
    /// </summary>
    IEnumerable<string> GetRegisteredCommands();
}

/// <summary>
/// Default implementation of IHandlerRegistry.
/// </summary>
internal sealed class HandlerRegistry : IHandlerRegistry
{
    private readonly Dictionary<string, IMeshHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(string command, IMeshHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers[command] = handler;
    }

    /// <inheritdoc />
    public IMeshHandler? GetHandler(string command) =>
        _handlers.GetValueOrDefault(command);

    /// <inheritdoc />
    public IEnumerable<string> GetRegisteredCommands() =>
        _handlers.Keys;
}
