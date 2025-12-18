using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Transport.P2P;

/// <summary>
/// RFC 5766 TURN client implementation for NAT traversal relay.
/// Provides relay capabilities for Symmetric NAT scenarios.
/// </summary>
public sealed class TurnClient : IAsyncDisposable
{
    // STUN/TURN message types
    private const ushort AllocateRequest = 0x0003;
    private const ushort AllocateResponse = 0x0103;
    private const ushort AllocateErrorResponse = 0x0113;
    private const ushort RefreshRequest = 0x0004;
    private const ushort RefreshResponse = 0x0104;
    private const ushort CreatePermissionRequest = 0x0008;
    private const ushort CreatePermissionResponse = 0x0108;
    private const ushort ChannelBindRequest = 0x0009;
    private const ushort ChannelBindResponse = 0x0109;
    private const ushort SendIndication = 0x0016;
    private const ushort DataIndication = 0x0017;

    // STUN/TURN attributes
    private const ushort AttrUsername = 0x0006;
    private const ushort AttrMessageIntegrity = 0x0008;
    private const ushort AttrErrorCode = 0x0009;
    private const ushort AttrRealm = 0x0014;
    private const ushort AttrNonce = 0x0015;
    private const ushort AttrXorRelayedAddress = 0x0016;
    private const ushort AttrXorMappedAddress = 0x0020;
    private const ushort AttrXorPeerAddress = 0x0012;
    private const ushort AttrData = 0x0013;
    private const ushort AttrLifetime = 0x000D;
    private const ushort AttrRequestedTransport = 0x0019;
    private const ushort AttrChannelNumber = 0x000C;

    // Magic cookie for STUN
    private const uint MagicCookie = 0x2112A442;

    private readonly P2POptions _options;
    private readonly ILogger<TurnClient> _logger;
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _backgroundCts = new();

    private IPEndPoint? _turnServerEndPoint;
    private IPEndPoint? _relayedAddress;
    private IPEndPoint? _mappedAddress;
    private byte[]? _transactionId;
    private string? _realm;
    private string? _nonce;
    private int _lifetime;
    private bool _allocated;
    private Task? _refreshTask;

    // Channel bindings for efficient data transfer
    private readonly Dictionary<string, ushort> _channelBindings = new();
    private readonly Dictionary<ushort, IPEndPoint> _reverseChannelBindings = new();
    private ushort _nextChannelNumber = 0x4000; // Channel numbers start at 0x4000

    /// <summary>
    /// Gets whether the TURN allocation is active.
    /// </summary>
    public bool IsAllocated => _allocated && _relayedAddress != null;

    /// <summary>
    /// Gets the relayed address allocated by the TURN server.
    /// </summary>
    public IPEndPoint? RelayedAddress => _relayedAddress;

    /// <summary>
    /// Gets the reflexive address (public IP as seen by TURN server).
    /// </summary>
    public IPEndPoint? MappedAddress => _mappedAddress;

    /// <summary>
    /// Event raised when data is received from a peer via TURN relay.
    /// </summary>
    public event EventHandler<TurnDataReceivedEventArgs>? DataReceived;

    public TurnClient(IOptions<P2POptions> options, ILogger<TurnClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _udpClient = new UdpClient();
    }

    /// <summary>
    /// Allocates a relay address from the TURN server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocated relay address as an ICE candidate.</returns>
    public async Task<IceCandidate?> AllocateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TurnServer))
        {
            _logger.LogWarning("TURN server not configured");
            return null;
        }

        try
        {
            // Resolve TURN server address
            var addresses = await Dns.GetHostAddressesAsync(_options.TurnServer, cancellationToken);
            if (addresses.Length == 0)
            {
                _logger.LogError("Failed to resolve TURN server: {Server}", _options.TurnServer);
                return null;
            }

            _turnServerEndPoint = new IPEndPoint(addresses[0], _options.TurnPort);
            _logger.LogInformation("Connecting to TURN server at {EndPoint}", _turnServerEndPoint);

            // Bind local UDP socket
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            // Start background receiver
            _ = ReceiveLoopAsync(_backgroundCts.Token);

            // Send initial Allocate request (will get 401 with realm/nonce)
            var firstResponse = await SendAllocateRequestAsync(authenticated: false, cancellationToken);

            if (firstResponse.Type == AllocateErrorResponse)
            {
                // Parse realm and nonce from error response
                ParseAuthenticationParams(firstResponse);

                if (_realm == null || _nonce == null)
                {
                    _logger.LogError("TURN server did not provide authentication parameters");
                    return null;
                }

                // Send authenticated Allocate request
                var authResponse = await SendAllocateRequestAsync(authenticated: true, cancellationToken);

                if (authResponse.Type != AllocateResponse)
                {
                    _logger.LogError("TURN allocation failed: {ErrorCode}", GetErrorCode(authResponse));
                    return null;
                }

                ParseAllocateResponse(authResponse);
            }
            else if (firstResponse.Type == AllocateResponse)
            {
                // Server didn't require authentication (unusual)
                ParseAllocateResponse(firstResponse);
            }
            else
            {
                _logger.LogError("Unexpected response type: 0x{Type:X4}", firstResponse.Type);
                return null;
            }

            if (_relayedAddress == null)
            {
                _logger.LogError("Failed to obtain relayed address");
                return null;
            }

            _allocated = true;
            _logger.LogInformation(
                "TURN allocation successful. Relayed: {Relayed}, Lifetime: {Lifetime}s",
                _relayedAddress, _lifetime);

            // Start refresh task to keep allocation alive
            _refreshTask = RefreshAllocationLoopAsync(_backgroundCts.Token);

            // Create ICE candidate for the relayed address
            var localEndPoint = (IPEndPoint)_udpClient.Client.LocalEndPoint!;
            return IceCandidate.CreateRelayed(
                _relayedAddress.Address.ToString(),
                _relayedAddress.Port,
                localEndPoint.Address.ToString(),
                localEndPoint.Port,
                _options.TurnServer!,
                _options.TurnPort,
                IceCandidate.CalculatePriority(IceCandidateType.Relayed, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TURN allocation failed");
            return null;
        }
    }

    /// <summary>
    /// Creates a permission for the specified peer address.
    /// Required before sending data to a peer.
    /// </summary>
    /// <param name="peerAddress">The peer's address to create permission for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> CreatePermissionAsync(IPEndPoint peerAddress, CancellationToken cancellationToken = default)
    {
        if (!_allocated)
        {
            _logger.LogWarning("Cannot create permission: not allocated");
            return false;
        }

        try
        {
            var response = await SendCreatePermissionRequestAsync(peerAddress, cancellationToken);
            var success = response.Type == CreatePermissionResponse;

            if (success)
            {
                _logger.LogDebug("Created permission for peer {PeerAddress}", peerAddress);
            }
            else
            {
                _logger.LogWarning("Failed to create permission for {PeerAddress}: {Error}",
                    peerAddress, GetErrorCode(response));
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create permission for {PeerAddress}", peerAddress);
            return false;
        }
    }

    /// <summary>
    /// Binds a channel to the specified peer for efficient data transfer.
    /// </summary>
    /// <param name="peerAddress">The peer's address to bind channel to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channel number, or 0 if binding failed.</returns>
    public async Task<ushort> BindChannelAsync(IPEndPoint peerAddress, CancellationToken cancellationToken = default)
    {
        if (!_allocated)
        {
            _logger.LogWarning("Cannot bind channel: not allocated");
            return 0;
        }

        var peerKey = peerAddress.ToString();
        if (_channelBindings.TryGetValue(peerKey, out var existingChannel))
        {
            return existingChannel;
        }

        try
        {
            var channelNumber = _nextChannelNumber++;
            var response = await SendChannelBindRequestAsync(channelNumber, peerAddress, cancellationToken);

            if (response.Type == ChannelBindResponse)
            {
                _channelBindings[peerKey] = channelNumber;
                _reverseChannelBindings[channelNumber] = peerAddress;
                _logger.LogDebug("Bound channel {Channel} to peer {PeerAddress}", channelNumber, peerAddress);
                return channelNumber;
            }

            _logger.LogWarning("Failed to bind channel to {PeerAddress}: {Error}",
                peerAddress, GetErrorCode(response));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bind channel to {PeerAddress}", peerAddress);
            return 0;
        }
    }

    /// <summary>
    /// Sends data to a peer through the TURN relay.
    /// </summary>
    /// <param name="peerAddress">The peer's address.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendDataAsync(IPEndPoint peerAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!_allocated || _turnServerEndPoint == null)
        {
            throw new InvalidOperationException("TURN not allocated");
        }

        var peerKey = peerAddress.ToString();

        // Try to use channel data (more efficient)
        if (_channelBindings.TryGetValue(peerKey, out var channel))
        {
            await SendChannelDataAsync(channel, data, cancellationToken);
        }
        else
        {
            // Fall back to Send indication
            await SendIndicationAsync(peerAddress, data, cancellationToken);
        }
    }

    private async Task SendChannelDataAsync(ushort channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        // ChannelData message format:
        // 2 bytes: Channel Number
        // 2 bytes: Length
        // Variable: Application Data
        // Padding to 4-byte boundary

        var paddedLength = (data.Length + 3) & ~3;
        var message = new byte[4 + paddedLength];

        // Channel number (big-endian)
        message[0] = (byte)(channel >> 8);
        message[1] = (byte)(channel & 0xFF);

        // Length (big-endian)
        message[2] = (byte)(data.Length >> 8);
        message[3] = (byte)(data.Length & 0xFF);

        // Data
        data.CopyTo(message.AsMemory(4));

        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);
    }

    private async Task SendIndicationAsync(IPEndPoint peerAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        // Send indication with XOR-PEER-ADDRESS and DATA attributes
        _transactionId = GenerateTransactionId();

        var attributes = new List<(ushort Type, byte[] Value)>
        {
            (AttrXorPeerAddress, EncodeXorAddress(peerAddress)),
            (AttrData, data.ToArray())
        };

        var message = BuildStunMessage(SendIndication, attributes);
        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);
    }

    private async Task<StunMessage> SendAllocateRequestAsync(bool authenticated, CancellationToken cancellationToken)
    {
        _transactionId = GenerateTransactionId();

        var attributes = new List<(ushort Type, byte[] Value)>
        {
            // REQUESTED-TRANSPORT: UDP (17)
            (AttrRequestedTransport, new byte[] { 17, 0, 0, 0 })
        };

        if (authenticated && _realm != null && _nonce != null)
        {
            attributes.Add((AttrUsername, Encoding.UTF8.GetBytes(_options.TurnUsername ?? "")));
            attributes.Add((AttrRealm, Encoding.UTF8.GetBytes(_realm)));
            attributes.Add((AttrNonce, Encoding.UTF8.GetBytes(_nonce)));
        }

        var message = BuildStunMessage(AllocateRequest, attributes, authenticated);
        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);

        return await ReceiveResponseAsync(_transactionId, cancellationToken);
    }

    private async Task<StunMessage> SendCreatePermissionRequestAsync(IPEndPoint peerAddress, CancellationToken cancellationToken)
    {
        _transactionId = GenerateTransactionId();

        var attributes = new List<(ushort Type, byte[] Value)>
        {
            (AttrXorPeerAddress, EncodeXorAddress(peerAddress)),
            (AttrUsername, Encoding.UTF8.GetBytes(_options.TurnUsername ?? "")),
            (AttrRealm, Encoding.UTF8.GetBytes(_realm ?? "")),
            (AttrNonce, Encoding.UTF8.GetBytes(_nonce ?? ""))
        };

        var message = BuildStunMessage(CreatePermissionRequest, attributes, authenticated: true);
        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);

        return await ReceiveResponseAsync(_transactionId, cancellationToken);
    }

    private async Task<StunMessage> SendChannelBindRequestAsync(ushort channel, IPEndPoint peerAddress, CancellationToken cancellationToken)
    {
        _transactionId = GenerateTransactionId();

        var channelAttr = new byte[4];
        channelAttr[0] = (byte)(channel >> 8);
        channelAttr[1] = (byte)(channel & 0xFF);
        // Reserved (RFFU)
        channelAttr[2] = 0;
        channelAttr[3] = 0;

        var attributes = new List<(ushort Type, byte[] Value)>
        {
            (AttrChannelNumber, channelAttr),
            (AttrXorPeerAddress, EncodeXorAddress(peerAddress)),
            (AttrUsername, Encoding.UTF8.GetBytes(_options.TurnUsername ?? "")),
            (AttrRealm, Encoding.UTF8.GetBytes(_realm ?? "")),
            (AttrNonce, Encoding.UTF8.GetBytes(_nonce ?? ""))
        };

        var message = BuildStunMessage(ChannelBindRequest, attributes, authenticated: true);
        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);

        return await ReceiveResponseAsync(_transactionId, cancellationToken);
    }

    private async Task<StunMessage> SendRefreshRequestAsync(int lifetime, CancellationToken cancellationToken)
    {
        _transactionId = GenerateTransactionId();

        var lifetimeAttr = new byte[4];
        lifetimeAttr[0] = (byte)(lifetime >> 24);
        lifetimeAttr[1] = (byte)((lifetime >> 16) & 0xFF);
        lifetimeAttr[2] = (byte)((lifetime >> 8) & 0xFF);
        lifetimeAttr[3] = (byte)(lifetime & 0xFF);

        var attributes = new List<(ushort Type, byte[] Value)>
        {
            (AttrLifetime, lifetimeAttr),
            (AttrUsername, Encoding.UTF8.GetBytes(_options.TurnUsername ?? "")),
            (AttrRealm, Encoding.UTF8.GetBytes(_realm ?? "")),
            (AttrNonce, Encoding.UTF8.GetBytes(_nonce ?? ""))
        };

        var message = BuildStunMessage(RefreshRequest, attributes, authenticated: true);
        await _udpClient.SendAsync(message, _turnServerEndPoint!, cancellationToken);

        return await ReceiveResponseAsync(_transactionId, cancellationToken);
    }

    private byte[] BuildStunMessage(ushort messageType, List<(ushort Type, byte[] Value)> attributes, bool authenticated = false)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Placeholder for header
        writer.Write(new byte[20]);

        // Write attributes
        foreach (var (attrType, attrValue) in attributes)
        {
            WriteAttribute(writer, attrType, attrValue);
        }

        // Add MESSAGE-INTEGRITY if authenticated
        if (authenticated && _options.TurnPassword != null)
        {
            // Calculate message integrity
            var messageForIntegrity = ms.ToArray();
            var integrityValue = CalculateMessageIntegrity(messageForIntegrity);
            WriteAttribute(writer, AttrMessageIntegrity, integrityValue);
        }

        var data = ms.ToArray();

        // Update header
        // Message Type
        data[0] = (byte)(messageType >> 8);
        data[1] = (byte)(messageType & 0xFF);

        // Message Length (excluding 20-byte header)
        var length = data.Length - 20;
        data[2] = (byte)(length >> 8);
        data[3] = (byte)(length & 0xFF);

        // Magic Cookie
        data[4] = (byte)(MagicCookie >> 24);
        data[5] = (byte)((MagicCookie >> 16) & 0xFF);
        data[6] = (byte)((MagicCookie >> 8) & 0xFF);
        data[7] = (byte)(MagicCookie & 0xFF);

        // Transaction ID
        Array.Copy(_transactionId!, 0, data, 8, 12);

        return data;
    }

    private static void WriteAttribute(BinaryWriter writer, ushort type, byte[] value)
    {
        // Type (big-endian)
        writer.Write((byte)(type >> 8));
        writer.Write((byte)(type & 0xFF));

        // Length (big-endian)
        writer.Write((byte)(value.Length >> 8));
        writer.Write((byte)(value.Length & 0xFF));

        // Value
        writer.Write(value);

        // Padding to 4-byte boundary
        var padding = (4 - (value.Length % 4)) % 4;
        for (var i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }
    }

    private byte[] CalculateMessageIntegrity(byte[] message)
    {
        // Key = MD5(username ":" realm ":" password)
        // Note: MD5 and HMAC-SHA1 are required by TURN RFC 5766 for message integrity
#pragma warning disable CA5351 // MD5 is required by TURN RFC 5766
        var keyInput = $"{_options.TurnUsername}:{_realm}:{_options.TurnPassword}";
        var key = MD5.HashData(Encoding.UTF8.GetBytes(keyInput));
#pragma warning restore CA5351

        // HMAC-SHA1 over message (with length adjusted for MESSAGE-INTEGRITY)
        var adjustedLength = message.Length - 20 + 24; // +24 for MESSAGE-INTEGRITY attribute
        message[2] = (byte)(adjustedLength >> 8);
        message[3] = (byte)(adjustedLength & 0xFF);

#pragma warning disable CA5350 // HMAC-SHA1 is required by TURN RFC 5766
        using var hmac = new HMACSHA1(key);
        return hmac.ComputeHash(message);
#pragma warning restore CA5350
    }

    private static byte[] EncodeXorAddress(IPEndPoint endPoint)
    {
        var result = new byte[8]; // IPv4 family
        result[0] = 0; // Reserved
        result[1] = 0x01; // IPv4 family

        // XOR port with magic cookie high bytes
        var xorPort = (ushort)(endPoint.Port ^ (MagicCookie >> 16));
        result[2] = (byte)(xorPort >> 8);
        result[3] = (byte)(xorPort & 0xFF);

        // XOR address with magic cookie
        var addrBytes = endPoint.Address.GetAddressBytes();
        var magicBytes = BitConverter.GetBytes(MagicCookie);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(magicBytes);
        }

        for (var i = 0; i < 4; i++)
        {
            result[4 + i] = (byte)(addrBytes[i] ^ magicBytes[i]);
        }

        return result;
    }

    private static IPEndPoint DecodeXorAddress(byte[] data)
    {
        // Family at offset 1
        var family = data[1];
        if (family != 0x01) // IPv4
        {
            throw new NotSupportedException("Only IPv4 is supported");
        }

        // XOR port
        var xorPort = (data[2] << 8) | data[3];
        var port = xorPort ^ (int)(MagicCookie >> 16);

        // XOR address
        var magicBytes = BitConverter.GetBytes(MagicCookie);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(magicBytes);
        }

        var addrBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            addrBytes[i] = (byte)(data[4 + i] ^ magicBytes[i]);
        }

        return new IPEndPoint(new IPAddress(addrBytes), port);
    }

    private static byte[] GenerateTransactionId()
    {
        var id = new byte[12];
        RandomNumberGenerator.Fill(id);
        return id;
    }

    private void ParseAuthenticationParams(StunMessage response)
    {
        foreach (var attr in response.Attributes)
        {
            switch (attr.Type)
            {
                case AttrRealm:
                    _realm = Encoding.UTF8.GetString(attr.Value);
                    break;
                case AttrNonce:
                    _nonce = Encoding.UTF8.GetString(attr.Value);
                    break;
            }
        }
    }

    private void ParseAllocateResponse(StunMessage response)
    {
        foreach (var attr in response.Attributes)
        {
            switch (attr.Type)
            {
                case AttrXorRelayedAddress:
                    _relayedAddress = DecodeXorAddress(attr.Value);
                    break;
                case AttrXorMappedAddress:
                    _mappedAddress = DecodeXorAddress(attr.Value);
                    break;
                case AttrLifetime:
                    _lifetime = (attr.Value[0] << 24) | (attr.Value[1] << 16) |
                               (attr.Value[2] << 8) | attr.Value[3];
                    break;
            }
        }
    }

    private static int GetErrorCode(StunMessage response)
    {
        foreach (var attr in response.Attributes)
        {
            if (attr.Type == AttrErrorCode && attr.Value.Length >= 4)
            {
                return (attr.Value[2] * 100) + attr.Value[3];
            }
        }
        return 0;
    }

    private readonly Dictionary<string, TaskCompletionSource<StunMessage>> _pendingRequests = new();

    private async Task<StunMessage> ReceiveResponseAsync(byte[] transactionId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<StunMessage>();
        var key = Convert.ToBase64String(transactionId);
        _pendingRequests[key] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        await using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            try
            {
                return await tcs.Task;
            }
            finally
            {
                _pendingRequests.Remove(key);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                ProcessReceivedData(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in TURN receive loop");
            }
        }
    }

    private void ProcessReceivedData(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (data.Length < 4) return;

        // Check if it's a ChannelData message (channel number starts at 0x4000)
        var firstByte = data[0];
        if (firstByte >= 0x40 && firstByte <= 0x7F)
        {
            ProcessChannelData(data);
            return;
        }

        // Check if it's a STUN message (magic cookie)
        if (data.Length >= 20)
        {
            var magic = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            if (magic == MagicCookie)
            {
                ProcessStunMessage(data);
                return;
            }
        }
    }

    private void ProcessChannelData(byte[] data)
    {
        var channel = (ushort)((data[0] << 8) | data[1]);
        var length = (data[2] << 8) | data[3];

        if (data.Length < 4 + length) return;

        if (_reverseChannelBindings.TryGetValue(channel, out var peerAddress))
        {
            var payload = new byte[length];
            Array.Copy(data, 4, payload, 0, length);
            DataReceived?.Invoke(this, new TurnDataReceivedEventArgs(peerAddress, payload));
        }
    }

    private void ProcessStunMessage(byte[] data)
    {
        var message = ParseStunMessage(data);

        // Check if this is a response to a pending request
        var key = Convert.ToBase64String(message.TransactionId);
        if (_pendingRequests.TryGetValue(key, out var tcs))
        {
            tcs.TrySetResult(message);
            return;
        }

        // Handle Data indication
        if (message.Type == DataIndication)
        {
            ProcessDataIndication(message);
        }
    }

    private void ProcessDataIndication(StunMessage message)
    {
        IPEndPoint? peerAddress = null;
        byte[]? payload = null;

        foreach (var attr in message.Attributes)
        {
            switch (attr.Type)
            {
                case AttrXorPeerAddress:
                    peerAddress = DecodeXorAddress(attr.Value);
                    break;
                case AttrData:
                    payload = attr.Value;
                    break;
            }
        }

        if (peerAddress != null && payload != null)
        {
            DataReceived?.Invoke(this, new TurnDataReceivedEventArgs(peerAddress, payload));
        }
    }

    private static StunMessage ParseStunMessage(byte[] data)
    {
        var type = (ushort)((data[0] << 8) | data[1]);
        var length = (data[2] << 8) | data[3];
        var transactionId = new byte[12];
        Array.Copy(data, 8, transactionId, 0, 12);

        var attributes = new List<StunAttribute>();
        var offset = 20;

        while (offset + 4 <= data.Length && offset < 20 + length)
        {
            var attrType = (ushort)((data[offset] << 8) | data[offset + 1]);
            var attrLength = (data[offset + 2] << 8) | data[offset + 3];

            if (offset + 4 + attrLength > data.Length) break;

            var attrValue = new byte[attrLength];
            Array.Copy(data, offset + 4, attrValue, 0, attrLength);
            attributes.Add(new StunAttribute(attrType, attrValue));

            // Move to next attribute (with padding)
            offset += 4 + attrLength + ((4 - (attrLength % 4)) % 4);
        }

        return new StunMessage(type, transactionId, attributes);
    }

    private async Task RefreshAllocationLoopAsync(CancellationToken cancellationToken)
    {
        // Refresh at 80% of lifetime
        var refreshInterval = TimeSpan.FromSeconds(_lifetime * 0.8);

        while (!cancellationToken.IsCancellationRequested && _allocated)
        {
            try
            {
                await Task.Delay(refreshInterval, cancellationToken);

                var response = await SendRefreshRequestAsync(_lifetime, cancellationToken);
                if (response.Type == RefreshResponse)
                {
                    _logger.LogDebug("TURN allocation refreshed");
                }
                else
                {
                    _logger.LogWarning("TURN refresh failed, allocation may expire");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing TURN allocation");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_allocated && _turnServerEndPoint != null)
        {
            try
            {
                // Send refresh with lifetime=0 to deallocate
                await SendRefreshRequestAsync(0, CancellationToken.None);
            }
            catch
            {
                // Best effort
            }
        }

        await _backgroundCts.CancelAsync();
        _backgroundCts.Dispose();

        if (_refreshTask != null)
        {
            try
            {
                await _refreshTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _udpClient.Dispose();
        _allocated = false;
    }

    private sealed record StunMessage(ushort Type, byte[] TransactionId, List<StunAttribute> Attributes);
    private sealed record StunAttribute(ushort Type, byte[] Value);
}

/// <summary>
/// Event args for TURN data received events.
/// </summary>
public sealed class TurnDataReceivedEventArgs : EventArgs
{
    public IPEndPoint PeerAddress { get; }
    public byte[] Data { get; }

    public TurnDataReceivedEventArgs(IPEndPoint peerAddress, byte[] data)
    {
        PeerAddress = peerAddress;
        Data = data;
    }
}
