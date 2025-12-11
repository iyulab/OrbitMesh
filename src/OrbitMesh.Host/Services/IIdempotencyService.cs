namespace OrbitMesh.Host.Services;

/// <summary>
/// Service for managing idempotency of operations.
/// Ensures duplicate requests with the same key return the cached result.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to acquire an exclusive lock for the given idempotency key.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was acquired, false if key already exists.</returns>
    Task<bool> TryAcquireLockAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the result for a completed operation.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="key">The idempotency key.</param>
    /// <param name="result">The operation result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetResultAsync<T>(string key, T result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cached result for an idempotency key.
    /// </summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached result or default if not found.</returns>
    Task<T?> GetResultAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lock for an idempotency key.
    /// Used when an operation fails and should be retryable.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key is currently being processed (locked but no result yet).
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the key is locked and processing, false otherwise.</returns>
    Task<bool> IsProcessingAsync(string key, CancellationToken cancellationToken = default);
}
