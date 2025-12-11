using System.Collections.Concurrent;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace OrbitMesh.Host.Services;

/// <summary>
/// Implementation of resilience service using Polly policies.
/// </summary>
public class ResilienceService : IResilienceService
{
    private readonly ResilienceOptions _options;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();

    public ResilienceService(ResilienceOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithRetryAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        Action<Exception, int, TimeSpan>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = CreateRetryPipeline<T>(onRetry);
        return await pipeline.ExecuteAsync(
            async token => await operation(token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var state = GetOrCreateCircuitBreaker<T>(operationKey);
        return await state.Pipeline.ExecuteAsync(
            async token => await operation(token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithTimeoutAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs ?? _options.DefaultTimeoutMs);
        var pipeline = new ResiliencePipelineBuilder<T>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout
            })
            .Build();

        return await pipeline.ExecuteAsync(
            async token => await operation(token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithResilienceAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Combine all policies: Timeout -> Circuit Breaker -> Retry
        var timeout = TimeSpan.FromMilliseconds(_options.DefaultTimeoutMs);
        var state = GetOrCreateCircuitBreaker<T>(operationKey);

        var combinedPipeline = new ResiliencePipelineBuilder<T>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout
            })
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.InitialRetryDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.MaxRetryDelayMs),
                ShouldHandle = new PredicateBuilder<T>().Handle<Exception>(ex => IsTransientException(ex))
            })
            .Build();

        // Execute with combined retry and timeout, then through circuit breaker
        return await state.Pipeline.ExecuteAsync(
            async token => await combinedPipeline.ExecuteAsync(
                async innerToken => await operation(innerToken),
                token),
            cancellationToken);
    }

    /// <inheritdoc />
    public bool IsCircuitOpen(string operationKey)
    {
        if (_circuitBreakers.TryGetValue(operationKey, out var state))
        {
            return state.IsOpen;
        }
        return false;
    }

    /// <inheritdoc />
    public void ResetCircuit(string operationKey)
    {
        if (_circuitBreakers.TryGetValue(operationKey, out var state))
        {
            // In Polly 8.x, we need to use CloseAsync to close the circuit
            state.ManualControl?.CloseAsync().GetAwaiter().GetResult();
        }
    }

    private ResiliencePipeline<T> CreateRetryPipeline<T>(Action<Exception, int, TimeSpan>? onRetry)
    {
        var builder = new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.InitialRetryDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.MaxRetryDelayMs),
                ShouldHandle = new PredicateBuilder<T>().Handle<Exception>(ex => IsTransientException(ex)),
                OnRetry = args =>
                {
                    onRetry?.Invoke(
                        args.Outcome.Exception!,
                        args.AttemptNumber,
                        args.RetryDelay);
                    return default;
                }
            });

        return builder.Build();
    }

    private CircuitBreakerState<T> GetOrCreateCircuitBreaker<T>(string operationKey)
    {
        var state = _circuitBreakers.GetOrAdd(operationKey, _ => CreateCircuitBreakerState<T>());
        return (CircuitBreakerState<T>)state;
    }

    private CircuitBreakerState<T> CreateCircuitBreakerState<T>()
    {
        var manualControl = new CircuitBreakerManualControl();
        var circuitBreakerState = new CircuitBreakerState<T>();

        var pipeline = new ResiliencePipelineBuilder<T>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = _options.CircuitBreakerFailureThreshold,
                MinimumThroughput = _options.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerSamplingDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerBreakDurationMs),
                ShouldHandle = new PredicateBuilder<T>().Handle<Exception>(),
                OnOpened = args =>
                {
                    circuitBreakerState.IsOpen = true;
                    return default;
                },
                OnClosed = args =>
                {
                    circuitBreakerState.IsOpen = false;
                    return default;
                },
                OnHalfOpened = args =>
                {
                    circuitBreakerState.IsOpen = true; // Still considered "open"
                    return default;
                },
                ManualControl = manualControl
            })
            .Build();

        circuitBreakerState.Pipeline = pipeline;
        circuitBreakerState.ManualControl = manualControl;

        return circuitBreakerState;
    }

    private static bool IsTransientException(Exception ex)
    {
        // Consider most exceptions as transient for retry purposes
        // Specific non-transient exceptions can be filtered out here
        return ex is not (
            ArgumentException or
            ArgumentNullException or
            InvalidOperationException { Message: "Non-transient" } or
            NotSupportedException);
    }

    private abstract class CircuitBreakerState
    {
        public bool IsOpen { get; set; }
        public CircuitBreakerManualControl? ManualControl { get; set; }
    }

    private sealed class CircuitBreakerState<T> : CircuitBreakerState
    {
        public ResiliencePipeline<T> Pipeline { get; set; } = null!;
    }
}
