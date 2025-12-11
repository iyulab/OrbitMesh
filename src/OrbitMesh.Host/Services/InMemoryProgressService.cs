using System.Collections.Concurrent;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Host.Services;

/// <summary>
/// In-memory implementation of the progress service.
/// </summary>
public sealed class InMemoryProgressService : IProgressService, IDisposable
{
    private readonly ConcurrentDictionary<string, JobProgressTracker> _trackers = new();
    private readonly int _maxHistorySize;
    private bool _disposed;

    /// <summary>
    /// Creates a new in-memory progress service.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of history entries to keep per job.</param>
    public InMemoryProgressService(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <inheritdoc />
    public Task ReportProgressAsync(JobProgress progress, CancellationToken cancellationToken = default)
    {
        var tracker = _trackers.GetOrAdd(progress.JobId, _ => new JobProgressTracker(_maxHistorySize));
        tracker.Report(progress);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<JobProgress?> GetProgressAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_trackers.TryGetValue(jobId, out var tracker))
        {
            return Task.FromResult<JobProgress?>(tracker.Latest);
        }

        return Task.FromResult<JobProgress?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobProgress>> GetProgressHistoryAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_trackers.TryGetValue(jobId, out var tracker))
        {
            return Task.FromResult<IReadOnlyList<JobProgress>>(tracker.GetHistory());
        }

        return Task.FromResult<IReadOnlyList<JobProgress>>(Array.Empty<JobProgress>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobProgress>> GetAllProgressAsync(CancellationToken cancellationToken = default)
    {
        var allProgress = _trackers.Values
            .Select(t => t.Latest)
            .Where(p => p is not null)
            .Cast<JobProgress>()
            .ToList();

        return Task.FromResult<IReadOnlyList<JobProgress>>(allProgress);
    }

    /// <inheritdoc />
    public Task ClearProgressAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_trackers.TryRemove(jobId, out var tracker))
        {
            tracker.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string jobId, Func<JobProgress, Task> callback)
    {
        var tracker = _trackers.GetOrAdd(jobId, _ => new JobProgressTracker(_maxHistorySize));
        return tracker.Subscribe(callback);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var tracker in _trackers.Values)
        {
            tracker.Dispose();
        }

        _trackers.Clear();
    }

    /// <summary>
    /// Tracks progress for a single job including history and subscriptions.
    /// </summary>
    private sealed class JobProgressTracker : IDisposable
    {
        private readonly object _lock = new();
        private readonly List<JobProgress> _history = [];
        private readonly List<Func<JobProgress, Task>> _subscribers = [];
        private readonly int _maxHistorySize;
        private JobProgress? _latest;
        private bool _disposed;

        public JobProgress? Latest
        {
            get
            {
                lock (_lock)
                {
                    return _latest;
                }
            }
        }

        public JobProgressTracker(int maxHistorySize)
        {
            _maxHistorySize = maxHistorySize;
        }

        public void Report(JobProgress progress)
        {
            List<Func<JobProgress, Task>> subscribersCopy;

            lock (_lock)
            {
                _latest = progress;
                _history.Add(progress);

                // Trim history if needed
                while (_history.Count > _maxHistorySize)
                {
                    _history.RemoveAt(0);
                }

                subscribersCopy = [.. _subscribers];
            }

            // Notify subscribers outside lock
            foreach (var subscriber in subscribersCopy)
            {
                try
                {
                    _ = subscriber(progress);
                }
                catch
                {
                    // Don't let subscriber errors affect progress tracking
                }
            }
        }

        public List<JobProgress> GetHistory()
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }

        public IDisposable Subscribe(Func<JobProgress, Task> callback)
        {
            lock (_lock)
            {
                _subscribers.Add(callback);
            }

            return new Subscription(this, callback);
        }

        private void Unsubscribe(Func<JobProgress, Task> callback)
        {
            lock (_lock)
            {
                _subscribers.Remove(callback);
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
                _subscribers.Clear();
                _history.Clear();
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly JobProgressTracker _tracker;
            private readonly Func<JobProgress, Task> _callback;
            private bool _disposed;

            public Subscription(JobProgressTracker tracker, Func<JobProgress, Task> callback)
            {
                _tracker = tracker;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _tracker.Unsubscribe(_callback);
            }
        }
    }
}
