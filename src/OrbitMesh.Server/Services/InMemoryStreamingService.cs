using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Server.Services;

/// <summary>
/// In-memory implementation of the streaming service.
/// Manages streaming data flows with buffering and pub-sub support.
/// </summary>
public sealed class InMemoryStreamingService : IStreamingService, IDisposable
{
    private readonly ConcurrentDictionary<string, StreamContext> _streams = new();
    private readonly ILogger<InMemoryStreamingService> _logger;
    private readonly int _maxBufferSize;
    private readonly int _subscriberChannelCapacity;
    private bool _disposed;

    /// <summary>
    /// Creates a new in-memory streaming service.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxBufferSize">Maximum items to buffer per stream for replay.</param>
    /// <param name="subscriberChannelCapacity">Capacity of subscriber channels.</param>
    public InMemoryStreamingService(
        ILogger<InMemoryStreamingService> logger,
        int maxBufferSize = 1000,
        int subscriberChannelCapacity = 100)
    {
        _logger = logger;
        _maxBufferSize = maxBufferSize;
        _subscriberChannelCapacity = subscriberChannelCapacity;
    }

    /// <inheritdoc />
    public async Task PublishAsync(StreamItem item, CancellationToken cancellationToken = default)
    {
        var context = _streams.GetOrAdd(item.JobId, _ => new StreamContext(_maxBufferSize));

        // Update state
        context.UpdateState(item);

        // Buffer the item for replay
        context.BufferItem(item);

        // Publish to all subscribers
        var subscribers = context.GetSubscribers();
        var publishTasks = new List<Task>();

        foreach (var subscriber in subscribers)
        {
            publishTasks.Add(PublishToSubscriberAsync(subscriber, item, cancellationToken));
        }

        await Task.WhenAll(publishTasks);

        // Handle end of stream
        if (item.IsEndOfStream)
        {
            await CompleteStreamAsync(item.JobId, cancellationToken);
        }

        _logger.LogDebug(
            "Published stream item. JobId: {JobId}, Sequence: {Sequence}, Subscribers: {Count}",
            item.JobId,
            item.SequenceNumber,
            subscribers.Count);
    }

    private async Task PublishToSubscriberAsync(
        Channel<StreamItem> subscriber,
        StreamItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            await subscriber.Writer.WriteAsync(item, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            // Subscriber has been disposed
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish to subscriber. JobId: {JobId}", item.JobId);
        }
    }

    /// <inheritdoc />
    public ChannelReader<StreamItem> Subscribe(string jobId, long fromSequence = 0)
    {
        var context = _streams.GetOrAdd(jobId, _ => new StreamContext(_maxBufferSize));

        var channel = Channel.CreateBounded<StreamItem>(new BoundedChannelOptions(_subscriberChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        context.AddSubscriber(channel);

        // Send buffered items if requested
        if (fromSequence > 0)
        {
            _ = Task.Run(async () =>
            {
                var bufferedItems = context.GetBufferedItems(fromSequence);
                foreach (var item in bufferedItems)
                {
                    try
                    {
                        await channel.Writer.WriteAsync(item);
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        _logger.LogDebug(
            "New subscriber added. JobId: {JobId}, FromSequence: {FromSequence}, TotalSubscribers: {Count}",
            jobId,
            fromSequence,
            context.SubscriberCount);

        return channel.Reader;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamItem> SubscribeAsync(
        string jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = Subscribe(jobId);

        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item;

            if (item.IsEndOfStream)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public Task<StreamState?> GetStreamStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_streams.TryGetValue(jobId, out var context))
        {
            return Task.FromResult<StreamState?>(context.State);
        }

        return Task.FromResult<StreamState?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StreamItem>> GetBufferedItemsAsync(
        string jobId,
        long fromSequence = 0,
        int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        if (_streams.TryGetValue(jobId, out var context))
        {
            var items = context.GetBufferedItems(fromSequence).Take(maxItems).ToList();
            return Task.FromResult<IReadOnlyList<StreamItem>>(items);
        }

        return Task.FromResult<IReadOnlyList<StreamItem>>(Array.Empty<StreamItem>());
    }

    /// <inheritdoc />
    public Task CompleteStreamAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_streams.TryGetValue(jobId, out var context))
        {
            context.Complete();

            _logger.LogInformation(
                "Stream completed. JobId: {JobId}, TotalItems: {TotalItems}, TotalBytes: {TotalBytes}",
                jobId,
                context.State?.TotalItems,
                context.State?.TotalBytes);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AbortStreamAsync(string jobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        if (_streams.TryGetValue(jobId, out var context))
        {
            context.Abort(errorMessage);

            _logger.LogWarning(
                "Stream aborted. JobId: {JobId}, Error: {ErrorMessage}",
                jobId,
                errorMessage);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CleanupStreamAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_streams.TryRemove(jobId, out var context))
        {
            context.Dispose();

            _logger.LogDebug("Stream cleaned up. JobId: {JobId}", jobId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public int GetSubscriberCount(string jobId)
    {
        if (_streams.TryGetValue(jobId, out var context))
        {
            return context.SubscriberCount;
        }

        return 0;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetActiveStreamsAsync(CancellationToken cancellationToken = default)
    {
        var activeStreams = _streams
            .Where(kvp => !kvp.Value.State?.IsComplete ?? true)
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(activeStreams);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var context in _streams.Values)
        {
            context.Dispose();
        }

        _streams.Clear();
    }

    /// <summary>
    /// Context for managing a single stream including subscribers and buffering.
    /// </summary>
    private sealed class StreamContext : IDisposable
    {
        private readonly object _lock = new();
        private readonly List<Channel<StreamItem>> _subscribers = [];
        private readonly LinkedList<StreamItem> _buffer = new();
        private readonly int _maxBufferSize;
        private StreamState _state;
        private bool _isComplete;
        private bool _disposed;

        public StreamState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        public int SubscriberCount
        {
            get
            {
                lock (_lock)
                {
                    return _subscribers.Count;
                }
            }
        }

        public StreamContext(int maxBufferSize)
        {
            _maxBufferSize = maxBufferSize;
            _state = new StreamState
            {
                JobId = string.Empty,
                CurrentSequence = 0,
                TotalItems = 0,
                TotalBytes = 0
            };
        }

        public void UpdateState(StreamItem item)
        {
            lock (_lock)
            {
                _state = _state with
                {
                    JobId = item.JobId,
                    CurrentSequence = item.SequenceNumber,
                    TotalItems = _state.TotalItems + 1,
                    TotalBytes = _state.TotalBytes + item.Data.Length,
                    IsComplete = item.IsEndOfStream,
                    CompletedAt = item.IsEndOfStream ? DateTimeOffset.UtcNow : null
                };
            }
        }

        public void BufferItem(StreamItem item)
        {
            lock (_lock)
            {
                _buffer.AddLast(item);

                // Trim buffer if needed
                while (_buffer.Count > _maxBufferSize)
                {
                    _buffer.RemoveFirst();
                }
            }
        }

        public List<StreamItem> GetBufferedItems(long fromSequence)
        {
            lock (_lock)
            {
                return _buffer
                    .Where(i => i.SequenceNumber >= fromSequence)
                    .ToList();
            }
        }

        public void AddSubscriber(Channel<StreamItem> channel)
        {
            lock (_lock)
            {
                _subscribers.Add(channel);
            }
        }

        public List<Channel<StreamItem>> GetSubscribers()
        {
            lock (_lock)
            {
                // Remove closed subscribers
                _subscribers.RemoveAll(s => s.Reader.Completion.IsCompleted);
                return _subscribers.ToList();
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                if (_isComplete)
                {
                    return;
                }

                _isComplete = true;

                // Update state to mark as complete
                _state = _state with
                {
                    IsComplete = true,
                    CompletedAt = _state.CompletedAt ?? DateTimeOffset.UtcNow
                };

                foreach (var subscriber in _subscribers)
                {
                    subscriber.Writer.TryComplete();
                }
            }
        }

        public void Abort(string error)
        {
            lock (_lock)
            {
                if (_isComplete)
                {
                    return;
                }

                _isComplete = true;

                // Update state to mark as complete (aborted)
                _state = _state with
                {
                    IsComplete = true,
                    CompletedAt = _state.CompletedAt ?? DateTimeOffset.UtcNow
                };

                var exception = new OperationCanceledException(error);
                foreach (var subscriber in _subscribers)
                {
                    subscriber.Writer.TryComplete(exception);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (_lock)
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber.Writer.TryComplete();
                }

                _subscribers.Clear();
                _buffer.Clear();
            }
        }
    }
}
