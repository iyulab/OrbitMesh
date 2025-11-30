using System.Collections.Concurrent;

namespace OrbitMesh.Server.Services;

/// <summary>
/// In-memory implementation of idempotency service with TTL support.
/// </summary>
public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _entries = new();
    private readonly TimeSpan _defaultTtl;

    /// <summary>
    /// Creates a new in-memory idempotency service.
    /// </summary>
    /// <param name="defaultTtl">Default time-to-live for entries. Defaults to 24 hours.</param>
    public InMemoryIdempotencyService(TimeSpan? defaultTtl = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(24);
    }

    /// <inheritdoc />
    public Task<bool> TryAcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        CleanupExpired();

        var entry = new IdempotencyEntry
        {
            Key = key,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_defaultTtl)
        };

        var acquired = _entries.TryAdd(key, entry);
        return Task.FromResult(acquired);
    }

    /// <inheritdoc />
    public Task SetResultAsync<T>(string key, T result, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            entry.Result = result;
            entry.CompletedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<T?> GetResultAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _entries.TryRemove(key, out _);
                return Task.FromResult<T?>(default);
            }

            if (entry.Result is T typedResult)
            {
                return Task.FromResult<T?>(typedResult);
            }
        }

        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsProcessingAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _entries.TryRemove(key, out _);
                return Task.FromResult(false);
            }

            // Processing = locked but no result yet
            return Task.FromResult(entry.Result is null);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Removes expired entries from the cache.
    /// </summary>
    private void CleanupExpired()
    {
        var expiredKeys = _entries
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _entries.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Internal entry for tracking idempotency state.
    /// </summary>
    private sealed class IdempotencyEntry
    {
        public required string Key { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? CompletedAt { get; set; }
        public object? Result { get; set; }

        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    }
}
