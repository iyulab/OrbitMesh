using Microsoft.EntityFrameworkCore;
using OrbitMesh.Core.Storage;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite.Stores;

/// <summary>
/// SQLite implementation of IEventStore.
/// </summary>
public sealed class SqliteEventStore : IEventStore
{
    private readonly IDbContextFactory<OrbitMeshDbContext> _contextFactory;

    public SqliteEventStore(IDbContextFactory<OrbitMeshDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<long> AppendAsync(
        string streamId,
        IEnumerable<EventData> events,
        long expectedVersion = -1,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Get current stream version
        var currentVersion = await context.Events
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (long?)e.Version, ct) ?? -1;

        // Optimistic concurrency check
        if (expectedVersion >= 0 && currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(
                $"Stream '{streamId}' expected version {expectedVersion} but found {currentVersion}");
        }

        var version = currentVersion;
        var entities = new List<EventEntity>();

        foreach (var evt in events)
        {
            version++;
            entities.Add(new EventEntity
            {
                EventId = evt.EventId,
                StreamId = streamId,
                EventType = evt.EventType,
                Version = version,
                Data = evt.Data,
                Metadata = evt.Metadata,
                Timestamp = evt.Timestamp
            });
        }

        context.Events.AddRange(entities);
        await context.SaveChangesAsync(ct);

        return version;
    }

    public async Task<IReadOnlyList<EventData>> ReadStreamAsync(string streamId, CancellationToken ct = default)
    {
        return await ReadStreamAsync(streamId, 0, ct);
    }

    public async Task<IReadOnlyList<EventData>> ReadStreamAsync(string streamId, long fromVersion, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Events
            .AsNoTracking()
            .Where(e => e.StreamId == streamId && e.Version >= fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<long> GetStreamVersionAsync(string streamId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Events
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (long?)e.Version, ct) ?? -1;
    }

    public async Task<IReadOnlyList<EventData>> ReadAllAsync(long fromPosition = 0, int maxCount = 1000, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entities = await context.Events
            .AsNoTracking()
            .Where(e => e.Position >= fromPosition)
            .OrderBy(e => e.Position)
            .Take(maxCount)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task DeleteStreamAsync(string streamId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        await context.Events
            .Where(e => e.StreamId == streamId)
            .ExecuteDeleteAsync(ct);
    }

    private static EventData ToModel(EventEntity entity) =>
        new()
        {
            EventId = entity.EventId,
            StreamId = entity.StreamId,
            EventType = entity.EventType,
            Data = entity.Data,
            Metadata = entity.Metadata,
            Version = entity.Version,
            Position = entity.Position,
            Timestamp = entity.Timestamp
        };
}

/// <summary>
/// Exception thrown when an optimistic concurrency conflict occurs.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException() { }
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
