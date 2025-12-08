using OrbitMesh.Core.Enums;
using OrbitMesh.Core.Models;
using Stateless;

namespace OrbitMesh.Server.StateMachines;

/// <summary>
/// State machine for managing Agent lifecycle.
/// </summary>
public sealed class AgentStateMachine
{
    private readonly StateMachine<AgentStatus, AgentTrigger> _machine;
    private readonly AgentInfo? _agent;

    /// <summary>
    /// Current state of the agent.
    /// </summary>
    public AgentStatus CurrentState => _machine.State;

    /// <summary>
    /// Gets permitted triggers for the current state.
    /// </summary>
#pragma warning disable CS0618 // PermittedTriggers is obsolete but sync API is preferred here
    public IEnumerable<AgentTrigger> PermittedTriggers => _machine.PermittedTriggers;
#pragma warning restore CS0618

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event EventHandler<AgentStateChangedEventArgs>? StateChanged;

    public AgentStateMachine(AgentInfo agent)
    {
        _agent = agent;
        _machine = new StateMachine<AgentStatus, AgentTrigger>(agent.Status);
        ConfigureStateMachine();
    }

    public AgentStateMachine(AgentStatus initialState)
    {
        _agent = null;
        _machine = new StateMachine<AgentStatus, AgentTrigger>(initialState);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Created → Initializing (when agent starts connecting)
        _machine.Configure(AgentStatus.Created)
            .Permit(AgentTrigger.Initialize, AgentStatus.Initializing);

        // Initializing → Ready (when connection established) or Faulted (on error)
        _machine.Configure(AgentStatus.Initializing)
            .Permit(AgentTrigger.Connect, AgentStatus.Ready)
            .Permit(AgentTrigger.Fault, AgentStatus.Faulted)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected);

        // Ready → Running (when executing job), Paused, Stopping, or Disconnected
        _machine.Configure(AgentStatus.Ready)
            .Permit(AgentTrigger.StartJob, AgentStatus.Running)
            .Permit(AgentTrigger.Pause, AgentStatus.Paused)
            .Permit(AgentTrigger.Stop, AgentStatus.Stopping)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected)
            .Permit(AgentTrigger.Fault, AgentStatus.Faulted);

        // Running → Ready (when job completes), Paused, Stopping, Faulted, or Disconnected
        _machine.Configure(AgentStatus.Running)
            .Permit(AgentTrigger.CompleteJob, AgentStatus.Ready)
            .Permit(AgentTrigger.Pause, AgentStatus.Paused)
            .Permit(AgentTrigger.Stop, AgentStatus.Stopping)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected)
            .Permit(AgentTrigger.Fault, AgentStatus.Faulted);

        // Paused → Ready (on resume), Stopping, or Disconnected
        _machine.Configure(AgentStatus.Paused)
            .Permit(AgentTrigger.Resume, AgentStatus.Ready)
            .Permit(AgentTrigger.Stop, AgentStatus.Stopping)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected)
            .Permit(AgentTrigger.Fault, AgentStatus.Faulted);

        // Stopping → Stopped (graceful) or Disconnected (forced)
        _machine.Configure(AgentStatus.Stopping)
            .Permit(AgentTrigger.Stopped, AgentStatus.Stopped)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected)
            .Permit(AgentTrigger.Fault, AgentStatus.Faulted);

        // Stopped → can reconnect
        _machine.Configure(AgentStatus.Stopped)
            .Permit(AgentTrigger.Initialize, AgentStatus.Initializing);

        // Faulted → can recover
        _machine.Configure(AgentStatus.Faulted)
            .Permit(AgentTrigger.Recover, AgentStatus.Initializing)
            .Permit(AgentTrigger.Disconnect, AgentStatus.Disconnected);

        // Disconnected → can reconnect
        _machine.Configure(AgentStatus.Disconnected)
            .Permit(AgentTrigger.Reconnect, AgentStatus.Initializing)
            .Permit(AgentTrigger.Connect, AgentStatus.Ready); // Fast reconnect

        // Global state change handler
        _machine.OnTransitioned(transition =>
        {
            StateChanged?.Invoke(this, new AgentStateChangedEventArgs(
                transition.Source,
                transition.Destination,
                transition.Trigger));
        });
    }

    /// <summary>
    /// Starts initialization (agent connecting).
    /// </summary>
    public bool TryInitialize()
    {
        if (!_machine.CanFire(AgentTrigger.Initialize))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Initialize);
        return true;
    }

    /// <summary>
    /// Marks agent as connected and ready.
    /// </summary>
    public bool TryConnect()
    {
        if (!_machine.CanFire(AgentTrigger.Connect))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Connect);
        return true;
    }

    /// <summary>
    /// Marks agent as disconnected.
    /// </summary>
    public bool TryDisconnect()
    {
        if (!_machine.CanFire(AgentTrigger.Disconnect))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Disconnect);
        return true;
    }

    /// <summary>
    /// Starts a job on the agent.
    /// </summary>
    public bool TryStartJob()
    {
        if (!_machine.CanFire(AgentTrigger.StartJob))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.StartJob);
        return true;
    }

    /// <summary>
    /// Completes a job on the agent.
    /// </summary>
    public bool TryCompleteJob()
    {
        if (!_machine.CanFire(AgentTrigger.CompleteJob))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.CompleteJob);
        return true;
    }

    /// <summary>
    /// Pauses the agent.
    /// </summary>
    public bool TryPause()
    {
        if (!_machine.CanFire(AgentTrigger.Pause))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Pause);
        return true;
    }

    /// <summary>
    /// Resumes a paused agent.
    /// </summary>
    public bool TryResume()
    {
        if (!_machine.CanFire(AgentTrigger.Resume))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Resume);
        return true;
    }

    /// <summary>
    /// Initiates graceful stop.
    /// </summary>
    public bool TryStop()
    {
        if (!_machine.CanFire(AgentTrigger.Stop))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Stop);
        return true;
    }

    /// <summary>
    /// Marks agent as stopped.
    /// </summary>
    public bool TryStopped()
    {
        if (!_machine.CanFire(AgentTrigger.Stopped))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Stopped);
        return true;
    }

    /// <summary>
    /// Marks agent as faulted.
    /// </summary>
    public bool TryFault()
    {
        if (!_machine.CanFire(AgentTrigger.Fault))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Fault);
        return true;
    }

    /// <summary>
    /// Recovers from faulted state.
    /// </summary>
    public bool TryRecover()
    {
        if (!_machine.CanFire(AgentTrigger.Recover))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Recover);
        return true;
    }

    /// <summary>
    /// Initiates reconnection from disconnected state.
    /// </summary>
    public bool TryReconnect()
    {
        if (!_machine.CanFire(AgentTrigger.Reconnect))
        {
            return false;
        }

        _machine.Fire(AgentTrigger.Reconnect);
        return true;
    }

    /// <summary>
    /// Checks if a trigger can be fired.
    /// </summary>
    public bool CanFire(AgentTrigger trigger) => _machine.CanFire(trigger);

    /// <summary>
    /// Gets a DOT graph representation for visualization.
    /// </summary>
    public string ToDotGraph() => Stateless.Graph.UmlDotGraph.Format(_machine.GetInfo());
}

/// <summary>
/// Event arguments for agent state changes.
/// </summary>
public sealed class AgentStateChangedEventArgs : EventArgs
{
    public AgentStatus OldState { get; }
    public AgentStatus NewState { get; }
    public AgentTrigger Trigger { get; }

    public AgentStateChangedEventArgs(AgentStatus oldState, AgentStatus newState, AgentTrigger trigger)
    {
        OldState = oldState;
        NewState = newState;
        Trigger = trigger;
    }
}

/// <summary>
/// Triggers for agent state transitions.
/// </summary>
public enum AgentTrigger
{
    Initialize,
    Connect,
    Disconnect,
    Reconnect,
    StartJob,
    CompleteJob,
    Pause,
    Resume,
    Stop,
    Stopped,
    Fault,
    Recover
}
