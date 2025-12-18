namespace OrbitMesh.Core.Transport;

/// <summary>
/// Event arguments for transport state changes.
/// </summary>
public class TransportStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous state.
    /// </summary>
    public TransportState PreviousState { get; }

    /// <summary>
    /// Gets the new state.
    /// </summary>
    public TransportState NewState { get; }

    /// <summary>
    /// Gets the optional exception if the state change was due to an error.
    /// </summary>
    public Exception? Exception { get; }

    public TransportStateChangedEventArgs(TransportState previousState, TransportState newState, Exception? exception = null)
    {
        PreviousState = previousState;
        NewState = newState;
        Exception = exception;
    }
}

/// <summary>
/// Event arguments for data received from a transport.
/// </summary>
public class DataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the peer identifier or endpoint from which data was received.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the received data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Gets the channel or stream identifier (optional).
    /// </summary>
    public byte Channel { get; }

    public DataReceivedEventArgs(string source, ReadOnlyMemory<byte> data, byte channel = 0)
    {
        Source = source;
        Data = data;
        Channel = channel;
    }
}
