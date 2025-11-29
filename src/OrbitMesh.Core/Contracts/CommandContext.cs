using MessagePack;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Contracts;

/// <summary>
/// Provides context for command execution including parameters and metadata.
/// </summary>
public sealed class CommandContext
{
    /// <summary>
    /// The original job request.
    /// </summary>
    public required JobRequest Request { get; init; }

    /// <summary>
    /// The agent ID executing the command.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId => Request.CorrelationId;

    /// <summary>
    /// Creates a new command context.
    /// </summary>
    public CommandContext()
    {
    }

    /// <summary>
    /// Creates a new command context from a job request.
    /// </summary>
    public static CommandContext FromRequest(JobRequest request, string agentId) =>
        new()
        {
            Request = request,
            AgentId = agentId
        };

    /// <summary>
    /// Gets a parameter by deserializing from the request parameters.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <returns>The deserialized parameter or default if not present.</returns>
    public T? GetParameter<T>()
    {
        if (Request.Parameters is null || Request.Parameters.Length == 0)
        {
            return default;
        }

        return MessagePackSerializer.Deserialize<T>(Request.Parameters);
    }

    /// <summary>
    /// Gets a parameter by deserializing from the request parameters.
    /// Throws if the parameter is not present.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <returns>The deserialized parameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when parameters are not present.</exception>
    public T GetRequiredParameter<T>()
    {
        if (Request.Parameters is null || Request.Parameters.Length == 0)
        {
            throw new InvalidOperationException(
                $"Required parameter of type {typeof(T).Name} not found in request.");
        }

        return MessagePackSerializer.Deserialize<T>(Request.Parameters)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize parameter of type {typeof(T).Name}.");
    }

    /// <summary>
    /// Tries to get a metadata value from the request.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value if found.</param>
    /// <returns>True if the metadata key exists, false otherwise.</returns>
    public bool TryGetMetadata(string key, out string? value)
    {
        if (Request.Metadata is null)
        {
            value = null;
            return false;
        }

        return Request.Metadata.TryGetValue(key, out value);
    }
}
