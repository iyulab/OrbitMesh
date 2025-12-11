namespace OrbitMesh.Host.Services;

/// <summary>
/// Service providing resilience policies for operations.
/// Wraps Polly policies for retry, circuit breaker, timeout, and bulkhead patterns.
/// </summary>
public interface IResilienceService
{
    /// <summary>
    /// Executes an operation with retry policy.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationKey">Key identifying this operation type.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="onRetry">Optional callback invoked on each retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithRetryAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        Action<Exception, int, TimeSpan>? onRetry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with circuit breaker policy.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationKey">Key identifying this operation type (used for circuit state).</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithCircuitBreakerAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with timeout policy.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationKey">Key identifying this operation type.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds. If null, uses default timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithTimeoutAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with combined resilience policies (retry + circuit breaker + timeout).
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operationKey">Key identifying this operation type.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithResilienceAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the circuit breaker for a given operation is open.
    /// </summary>
    /// <param name="operationKey">Key identifying the operation type.</param>
    /// <returns>True if circuit is open or half-open, false if closed.</returns>
    bool IsCircuitOpen(string operationKey);

    /// <summary>
    /// Resets the circuit breaker for a given operation.
    /// </summary>
    /// <param name="operationKey">Key identifying the operation type.</param>
    void ResetCircuit(string operationKey);
}

/// <summary>
/// Configuration options for resilience policies.
/// </summary>
public sealed record ResilienceOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Initial delay between retries in milliseconds.
    /// Uses exponential backoff.
    /// </summary>
    public int InitialRetryDelayMs { get; init; } = 100;

    /// <summary>
    /// Maximum delay between retries in milliseconds.
    /// </summary>
    public int MaxRetryDelayMs { get; init; } = 30000;

    /// <summary>
    /// Default timeout for operations in milliseconds.
    /// </summary>
    public int DefaultTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Failure threshold ratio (0.0 to 1.0) to open circuit breaker.
    /// </summary>
    public double CircuitBreakerFailureThreshold { get; init; } = 0.5;

    /// <summary>
    /// Minimum throughput before circuit breaker evaluates failures.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Duration in milliseconds the circuit stays open before half-open.
    /// </summary>
    public int CircuitBreakerBreakDurationMs { get; init; } = 30000;

    /// <summary>
    /// Sampling duration in milliseconds for circuit breaker failure rate.
    /// </summary>
    public int CircuitBreakerSamplingDurationMs { get; init; } = 10000;
}
