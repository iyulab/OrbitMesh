using MessagePack;
using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Client;

/// <summary>
/// Fluent builder for creating job requests.
/// </summary>
public sealed class JobRequestBuilder
{
    private readonly string _command;
    private string? _idempotencyKey;
    private byte[]? _parameters;
    private int _priority;
    private TimeSpan? _timeout;
    private int _maxRetries;
    private string? _targetAgentId;
    private IReadOnlyList<string>? _requiredCapabilities;
    private string? _correlationId;
    private IReadOnlyDictionary<string, string>? _metadata;
    private ExecutionPattern _pattern = ExecutionPattern.RequestResponse;

    /// <summary>
    /// Creates a new job request builder for the specified command.
    /// </summary>
    /// <param name="command">The command/handler name to execute.</param>
    public JobRequestBuilder(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _command = command;
    }

    /// <summary>
    /// Sets a custom idempotency key for duplicate prevention.
    /// </summary>
    public JobRequestBuilder WithIdempotencyKey(string idempotencyKey)
    {
        _idempotencyKey = idempotencyKey;
        return this;
    }

    /// <summary>
    /// Sets the serialized parameters for the command.
    /// </summary>
    /// <typeparam name="T">The type of parameters.</typeparam>
    /// <param name="parameters">The parameters object.</param>
    public JobRequestBuilder WithParameters<T>(T parameters)
    {
        _parameters = MessagePackSerializer.Serialize(parameters);
        return this;
    }

    /// <summary>
    /// Sets the raw serialized parameters.
    /// </summary>
    public JobRequestBuilder WithRawParameters(byte[] parameters)
    {
        _parameters = parameters;
        return this;
    }

    /// <summary>
    /// Sets the priority level for the job.
    /// </summary>
    public JobRequestBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Sets the timeout for the job execution.
    /// </summary>
    public JobRequestBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    public JobRequestBuilder WithRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Targets a specific agent for execution.
    /// </summary>
    public JobRequestBuilder WithTargetAgent(string agentId)
    {
        _targetAgentId = agentId;
        return this;
    }

    /// <summary>
    /// Specifies required capabilities for agent selection.
    /// </summary>
    public JobRequestBuilder WithRequiredCapabilities(params string[] capabilities)
    {
        _requiredCapabilities = capabilities;
        return this;
    }

    /// <summary>
    /// Sets the correlation ID for distributed tracing.
    /// </summary>
    public JobRequestBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets custom metadata for the job.
    /// </summary>
    public JobRequestBuilder WithMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Sets the execution pattern for the job.
    /// </summary>
    public JobRequestBuilder WithPattern(ExecutionPattern pattern)
    {
        _pattern = pattern;
        return this;
    }

    /// <summary>
    /// Builds the job request.
    /// </summary>
    public JobRequest Build()
    {
        return new JobRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = _idempotencyKey ?? Guid.NewGuid().ToString("N"),
            Command = _command,
            Pattern = _pattern,
            Parameters = _parameters,
            TargetAgentId = _targetAgentId,
            RequiredCapabilities = _requiredCapabilities,
            Priority = _priority,
            Timeout = _timeout,
            MaxRetries = _maxRetries,
            CorrelationId = _correlationId,
            Metadata = _metadata,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
