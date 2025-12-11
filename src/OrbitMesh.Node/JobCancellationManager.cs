using System.Collections.Concurrent;

namespace OrbitMesh.Node;

/// <summary>
/// Manages cancellation tokens for running jobs, enabling cooperative cancellation.
/// </summary>
public sealed class JobCancellationManager : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellationSources = new();
    private bool _disposed;

    /// <summary>
    /// Registers a job and returns a cancellation token for it.
    /// </summary>
    /// <param name="jobId">The job ID to register.</param>
    /// <returns>A cancellation token that can be used to cancel the job.</returns>
    public CancellationToken RegisterJob(string jobId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        var cts = new CancellationTokenSource();
        _jobCancellationSources[jobId] = cts;
        return cts.Token;
    }

    /// <summary>
    /// Cancels a running job by its ID.
    /// </summary>
    /// <param name="jobId">The job ID to cancel.</param>
    /// <returns>True if the job was found and cancelled, false otherwise.</returns>
    public bool CancelJob(string jobId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(jobId))
        {
            return false;
        }

        if (_jobCancellationSources.TryGetValue(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Marks a job as completed and removes its cancellation token source.
    /// </summary>
    /// <param name="jobId">The job ID to mark as completed.</param>
    public void CompleteJob(string jobId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        if (_jobCancellationSources.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }

    /// <summary>
    /// Checks if a job is currently running.
    /// </summary>
    /// <param name="jobId">The job ID to check.</param>
    /// <returns>True if the job is running, false otherwise.</returns>
    public bool IsJobRunning(string jobId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(jobId))
        {
            return false;
        }

        return _jobCancellationSources.ContainsKey(jobId);
    }

    /// <summary>
    /// Gets all currently running job IDs.
    /// </summary>
    /// <returns>A read-only list of running job IDs.</returns>
    public IReadOnlyList<string> GetRunningJobIds()
    {
        return _jobCancellationSources.Keys.ToList();
    }

    /// <summary>
    /// Cancels all running jobs.
    /// </summary>
    /// <returns>The number of jobs cancelled.</returns>
    public int CancelAllJobs()
    {
        var count = 0;
        foreach (var kvp in _jobCancellationSources)
        {
            try
            {
                kvp.Value.Cancel();
                count++;
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        return count;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var kvp in _jobCancellationSources)
        {
            try
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        _jobCancellationSources.Clear();
    }
}
