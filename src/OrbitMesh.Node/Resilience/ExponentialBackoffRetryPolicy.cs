using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;

namespace OrbitMesh.Node.Resilience;

/// <summary>
/// Implements exponential backoff retry policy for SignalR connections.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly ResilienceOptions _options;

    // Jitter is not security-sensitive; it's just to prevent thundering herd
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new ExponentialBackoffRetryPolicy.
    /// </summary>
    /// <param name="options">Resilience options.</param>
    public ExponentialBackoffRetryPolicy(ResilienceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Jitter for retry timing is not security-sensitive")]
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Calculate delay with exponential backoff
        var attempt = retryContext.PreviousRetryCount;
        var delay = _options.InitialReconnectDelay.TotalMilliseconds *
                    Math.Pow(_options.ReconnectBackoffMultiplier, attempt);

        // Add jitter (±20%) to prevent thundering herd
        var jitter = 1.0 + (_random.NextDouble() * 0.4 - 0.2);
        delay *= jitter;

        // Cap at maximum delay
        delay = Math.Min(delay, _options.MaxReconnectDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(delay);
    }
}
