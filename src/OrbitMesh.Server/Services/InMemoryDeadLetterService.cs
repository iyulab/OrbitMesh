using System.Collections.Concurrent;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// In-memory implementation of the dead letter queue.
/// </summary>
public class InMemoryDeadLetterService : IDeadLetterService
{
    private readonly ConcurrentDictionary<string, DeadLetterEntry> _entries = new();
    private readonly ConcurrentDictionary<string, string> _jobIdIndex = new(); // JobId -> EntryId

    /// <inheritdoc />
    public Task<DeadLetterEntry> EnqueueAsync(Job job, string reason, CancellationToken cancellationToken = default)
    {
        var entry = new DeadLetterEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Job = job,
            Reason = reason,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        _entries[entry.Id] = entry;
        _jobIdIndex[job.Id] = entry.Id;

        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task<DeadLetterEntry?> GetAsync(string entryId, CancellationToken cancellationToken = default)
    {
        _entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task<DeadLetterEntry?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobIdIndex.TryGetValue(jobId, out var entryId))
        {
            _entries.TryGetValue(entryId, out var entry);
            return Task.FromResult(entry);
        }

        return Task.FromResult<DeadLetterEntry?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values
            .OrderBy(e => e.EnqueuedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterEntry>>(entries);
    }

    /// <inheritdoc />
    public Task<bool> MarkForRetryAsync(string entryId, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(entryId, out var entry))
        {
            var updated = entry with
            {
                RetryRequested = true,
                RetryRequestedAt = DateTimeOffset.UtcNow,
                RetryAttempts = entry.RetryAttempts + 1
            };

            _entries[entryId] = updated;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterEntry>> GetPendingRetryAsync(CancellationToken cancellationToken = default)
    {
        var pending = _entries.Values
            .Where(e => e.RetryRequested)
            .OrderBy(e => e.RetryRequestedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterEntry>>(pending);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string entryId, CancellationToken cancellationToken = default)
    {
        if (_entries.TryRemove(entryId, out var entry))
        {
            _jobIdIndex.TryRemove(entry.Job.Id, out _);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var count = _entries.Count;
        _entries.Clear();
        _jobIdIndex.Clear();
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entries.Count);
    }
}
