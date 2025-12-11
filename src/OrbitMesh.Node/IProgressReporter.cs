using OrbitMesh.Core.Models;

namespace OrbitMesh.Node;

/// <summary>
/// Interface for reporting job progress to the server.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// The job ID this reporter is associated with.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Reports progress as a percentage.
    /// </summary>
    /// <param name="percentage">Progress percentage (0-100).</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportAsync(int percentage, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports step-based progress.
    /// </summary>
    /// <param name="currentStep">Current step number.</param>
    /// <param name="totalSteps">Total number of steps.</param>
    /// <param name="stepDescription">Description of the current step.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportStepAsync(int currentStep, int totalSteps, string? stepDescription = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an IProgress{T} adapter for use with APIs that require IProgress.
    /// </summary>
    /// <returns>An IProgress adapter that forwards to this reporter.</returns>
    IProgress<JobProgress> AsProgress();
}

/// <summary>
/// Progress reporter that sends updates via a callback function.
/// </summary>
public sealed class ProgressReporter : IProgressReporter
{
    private readonly Func<JobProgress, Task> _reportCallback;

    /// <inheritdoc />
    public string JobId { get; }

    /// <summary>
    /// Creates a new progress reporter.
    /// </summary>
    /// <param name="jobId">The job ID to report progress for.</param>
    /// <param name="reportCallback">Callback to invoke when progress is reported.</param>
    public ProgressReporter(string jobId, Func<JobProgress, Task> reportCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(reportCallback);

        JobId = jobId;
        _reportCallback = reportCallback;
    }

    /// <inheritdoc />
    public Task ReportAsync(int percentage, string? message = null, CancellationToken cancellationToken = default)
    {
        var progress = JobProgress.Create(JobId, percentage, message);
        return _reportCallback(progress);
    }

    /// <inheritdoc />
    public Task ReportStepAsync(int currentStep, int totalSteps, string? stepDescription = null, CancellationToken cancellationToken = default)
    {
        var progress = JobProgress.CreateStep(JobId, currentStep, totalSteps, stepDescription);
        return _reportCallback(progress);
    }

    /// <inheritdoc />
    public IProgress<JobProgress> AsProgress()
    {
        return new ProgressAdapter(_reportCallback);
    }

    /// <summary>
    /// Adapter that implements IProgress{JobProgress} and forwards to a callback.
    /// </summary>
    private sealed class ProgressAdapter : IProgress<JobProgress>
    {
        private readonly Func<JobProgress, Task> _callback;

        public ProgressAdapter(Func<JobProgress, Task> callback)
        {
            _callback = callback;
        }

        public void Report(JobProgress value)
        {
            // Fire and forget - IProgress.Report is synchronous by design
            _ = _callback(value);
        }
    }
}
