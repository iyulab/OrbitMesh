using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Client;

/// <summary>
/// Client SDK for interacting with OrbitMesh server.
/// Provides job submission, tracking, and result retrieval capabilities.
/// </summary>
public sealed class OrbitMeshClient : IAsyncDisposable, IDisposable
{
    private readonly string _serverUri;
    private readonly OrbitMeshClientOptions _options;
    private HubConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Gets the server URI.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI properties should not be strings",
        Justification = "String URI provides simpler API for SDK consumers")]
    public string ServerUri => _serverUri;

    /// <summary>
    /// Gets whether the client is connected to the server.
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Event raised when the connection state changes.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when a job result is received.
    /// </summary>
    public event EventHandler<JobResultEventArgs>? JobResultReceived;

    /// <summary>
    /// Event raised when job progress is reported.
    /// </summary>
    public event EventHandler<JobProgressEventArgs>? JobProgressReceived;

    /// <summary>
    /// Creates a new OrbitMesh client.
    /// </summary>
    /// <param name="serverUri">The URI of the OrbitMesh server.</param>
    /// <exception cref="ArgumentNullException">Thrown when serverUri is null.</exception>
    /// <exception cref="ArgumentException">Thrown when serverUri is empty.</exception>
    public OrbitMeshClient(string serverUri) : this(serverUri, new OrbitMeshClientOptions())
    {
    }

    /// <summary>
    /// Creates a new OrbitMesh client with custom options.
    /// </summary>
    /// <param name="serverUri">The URI of the OrbitMesh server.</param>
    /// <param name="options">Configuration options.</param>
    public OrbitMeshClient(string serverUri, OrbitMeshClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentException.ThrowIfNullOrEmpty(serverUri);
        ArgumentNullException.ThrowIfNull(options);

        _serverUri = serverUri;
        _options = options;
    }

    /// <summary>
    /// Creates a new OrbitMesh client from options.
    /// </summary>
    /// <param name="options">Configuration options including server URI.</param>
    public OrbitMeshClient(OrbitMeshClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ServerUri);

        _serverUri = options.ServerUri;
        _options = options;
    }

    /// <summary>
    /// Connects to the OrbitMesh server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is not null && _connection.State == HubConnectionState.Connected)
        {
            return;
        }

        _connection = BuildConnection();
        ConfigureConnection();

        await _connection.StartAsync(cancellationToken);
        OnConnectionStateChanged(_connection.State);
    }

    /// <summary>
    /// Disconnects from the OrbitMesh server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        if (_connection.State != HubConnectionState.Disconnected)
        {
            await _connection.StopAsync(cancellationToken);
            OnConnectionStateChanged(_connection.State);
        }
    }

    /// <summary>
    /// Creates a new job request builder.
    /// </summary>
    /// <param name="command">The command/handler name to execute.</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Instance method for consistent API design")]
    public JobRequestBuilder CreateJobRequest(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new JobRequestBuilder(command);
    }

    /// <summary>
    /// Submits a job to the server.
    /// </summary>
    /// <param name="request">The job request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job submission result.</returns>
    public async Task<JobSubmissionResult> SubmitJobAsync(
        JobRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        return await _connection!.InvokeAsync<JobSubmissionResult>(
            "SubmitJobAsync",
            request,
            cancellationToken);
    }

    /// <summary>
    /// Submits a job and waits for the result.
    /// </summary>
    /// <param name="request">The job request.</param>
    /// <param name="timeout">Maximum time to wait for result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job result.</returns>
    public async Task<JobResult> SubmitAndWaitAsync(
        JobRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        var effectiveTimeout = timeout ?? _options.Timeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        var tcs = new TaskCompletionSource<JobResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobId = request.Id;

        void ResultHandler(object? sender, JobResultEventArgs e)
        {
            if (e.Result.JobId == jobId)
            {
                tcs.TrySetResult(e.Result);
            }
        }

        JobResultReceived += ResultHandler;
        try
        {
            await SubmitJobAsync(request, cts.Token);
            return await tcs.Task.WaitAsync(cts.Token);
        }
        finally
        {
            JobResultReceived -= ResultHandler;
        }
    }

    /// <summary>
    /// Gets the status of a job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job status information.</returns>
    public async Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        return await _connection!.InvokeAsync<Job?>(
            "GetJobAsync",
            jobId,
            cancellationToken);
    }

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the cancellation was successful.</returns>
    public async Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        return await _connection!.InvokeAsync<bool>(
            "CancelJobAsync",
            jobId,
            cancellationToken);
    }

    private HubConnection BuildConnection()
    {
        var hubUrl = _serverUri.TrimEnd('/') + _options.HubPath;

        var builder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (_options.AccessTokenProvider is not null)
                {
                    options.AccessTokenProvider = _options.AccessTokenProvider;
                }

                if (_options.Headers is not null)
                {
                    foreach (var header in _options.Headers)
                    {
                        options.Headers[header.Key] = header.Value;
                    }
                }
            })
            .AddMessagePackProtocol();

        if (_options.AutoReconnect)
        {
            builder.WithAutomaticReconnect(new RetryPolicy(_options));
        }

        return builder.Build();
    }

    private void ConfigureConnection()
    {
        if (_connection is null)
        {
            return;
        }

        _connection.Closed += _ =>
        {
            OnConnectionStateChanged(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };

        _connection.Reconnecting += _ =>
        {
            OnConnectionStateChanged(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            OnConnectionStateChanged(HubConnectionState.Connected);
            return Task.CompletedTask;
        };

        // Register handlers for server-initiated messages
        _connection.On<JobResult>("OnJobResult", result =>
        {
            JobResultReceived?.Invoke(this, new JobResultEventArgs(result));
        });

        _connection.On<JobProgress>("OnJobProgress", progress =>
        {
            JobProgressReceived?.Invoke(this, new JobProgressEventArgs(progress));
        });
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(state));
    }

    private void EnsureConnected()
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Client is not connected to the server.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Fire and forget - synchronous disposal cannot await
        _connection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class RetryPolicy : IRetryPolicy
    {
        private readonly OrbitMeshClientOptions _options;

        public RetryPolicy(OrbitMeshClientOptions options) => _options = options;

        [SuppressMessage("Security", "CA5394:Do not use insecure randomizer",
            Justification = "Random jitter for retry delay does not require cryptographic security")]
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount >= _options.MaxReconnectAttempts)
            {
                return null;
            }

            // Exponential backoff with jitter
            var baseDelay = _options.ReconnectDelay.TotalMilliseconds;
            var delay = baseDelay * Math.Pow(2, retryContext.PreviousRetryCount);
            var jitter = Random.Shared.NextDouble() * 0.2 * delay;
            return TimeSpan.FromMilliseconds(delay + jitter);
        }
    }
}
