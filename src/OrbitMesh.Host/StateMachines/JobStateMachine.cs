using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using Stateless;

namespace OrbitMesh.Host.StateMachines;

/// <summary>
/// State machine for managing Job lifecycle.
/// </summary>
public sealed class JobStateMachine
{
    private readonly StateMachine<JobStatus, JobTrigger> _machine;
    private readonly Job _job;

    /// <summary>
    /// Current state of the job.
    /// </summary>
    public JobStatus CurrentState => _machine.State;

    /// <summary>
    /// Gets permitted triggers for the current state.
    /// </summary>
#pragma warning disable CS0618 // PermittedTriggers is obsolete but sync API is preferred here
    public IEnumerable<JobTrigger> PermittedTriggers => _machine.PermittedTriggers;
#pragma warning restore CS0618

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event EventHandler<JobStateChangedEventArgs>? StateChanged;

    public JobStateMachine(Job job)
    {
        _job = job;
        _machine = new StateMachine<JobStatus, JobTrigger>(job.Status);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Pending → Assigned (when job is dispatched to agent)
        _machine.Configure(JobStatus.Pending)
            .Permit(JobTrigger.Assign, JobStatus.Assigned)
            .Permit(JobTrigger.Cancel, JobStatus.Cancelled)
            .Permit(JobTrigger.Timeout, JobStatus.TimedOut);

        // Assigned → Running (when agent ACKs) or back to Pending (on NACK/timeout)
        _machine.Configure(JobStatus.Assigned)
            .Permit(JobTrigger.Start, JobStatus.Running)
            .Permit(JobTrigger.Reject, JobStatus.Pending)
            .Permit(JobTrigger.Timeout, JobStatus.Pending) // Re-queue on assignment timeout
            .Permit(JobTrigger.Cancel, JobStatus.Cancelled)
            .Permit(JobTrigger.Fail, JobStatus.Failed);

        // Running → Completed/Failed/Cancelled/TimedOut
        _machine.Configure(JobStatus.Running)
            .Permit(JobTrigger.Complete, JobStatus.Completed)
            .Permit(JobTrigger.Fail, JobStatus.Failed)
            .Permit(JobTrigger.Cancel, JobStatus.Cancelled)
            .Permit(JobTrigger.Timeout, JobStatus.TimedOut);

        // Terminal states - no transitions out
        _machine.Configure(JobStatus.Completed);

        _machine.Configure(JobStatus.Failed)
            .Permit(JobTrigger.Retry, JobStatus.Pending); // Allow retry

        _machine.Configure(JobStatus.Cancelled);

        _machine.Configure(JobStatus.TimedOut)
            .Permit(JobTrigger.Retry, JobStatus.Pending); // Allow retry

        // Global state change handler
        _machine.OnTransitioned(transition =>
        {
            StateChanged?.Invoke(this, new JobStateChangedEventArgs(
                transition.Source,
                transition.Destination,
                transition.Trigger));
        });
    }

    /// <summary>
    /// Assigns the job to an agent.
    /// </summary>
    public bool TryAssign()
    {
        if (!_machine.CanFire(JobTrigger.Assign))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Assign);
        return true;
    }

    /// <summary>
    /// Starts job execution (agent acknowledged).
    /// </summary>
    public bool TryStart()
    {
        if (!_machine.CanFire(JobTrigger.Start))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Start);
        return true;
    }

    /// <summary>
    /// Completes the job successfully.
    /// </summary>
    public bool TryComplete()
    {
        if (!_machine.CanFire(JobTrigger.Complete))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Complete);
        return true;
    }

    /// <summary>
    /// Fails the job.
    /// </summary>
    public bool TryFail()
    {
        if (!_machine.CanFire(JobTrigger.Fail))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Fail);
        return true;
    }

    /// <summary>
    /// Cancels the job.
    /// </summary>
    public bool TryCancel()
    {
        if (!_machine.CanFire(JobTrigger.Cancel))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Cancel);
        return true;
    }

    /// <summary>
    /// Times out the job.
    /// </summary>
    public bool TryTimeout()
    {
        if (!_machine.CanFire(JobTrigger.Timeout))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Timeout);
        return true;
    }

    /// <summary>
    /// Retries a failed or timed out job.
    /// </summary>
    public bool TryRetry()
    {
        if (!_machine.CanFire(JobTrigger.Retry))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Retry);
        return true;
    }

    /// <summary>
    /// Rejects the job assignment (agent NACK).
    /// </summary>
    public bool TryReject()
    {
        if (!_machine.CanFire(JobTrigger.Reject))
        {
            return false;
        }

        _machine.Fire(JobTrigger.Reject);
        return true;
    }

    /// <summary>
    /// Checks if a trigger can be fired.
    /// </summary>
    public bool CanFire(JobTrigger trigger) => _machine.CanFire(trigger);

    /// <summary>
    /// Gets a DOT graph representation for visualization.
    /// </summary>
    public string ToDotGraph() => Stateless.Graph.UmlDotGraph.Format(_machine.GetInfo());
}

/// <summary>
/// Event arguments for job state changes.
/// </summary>
public sealed class JobStateChangedEventArgs : EventArgs
{
    public JobStatus OldState { get; }
    public JobStatus NewState { get; }
    public JobTrigger Trigger { get; }

    public JobStateChangedEventArgs(JobStatus oldState, JobStatus newState, JobTrigger trigger)
    {
        OldState = oldState;
        NewState = newState;
        Trigger = trigger;
    }
}

/// <summary>
/// Triggers for job state transitions.
/// </summary>
public enum JobTrigger
{
    Assign,
    Start,
    Complete,
    Fail,
    Cancel,
    Timeout,
    Retry,
    Reject
}
