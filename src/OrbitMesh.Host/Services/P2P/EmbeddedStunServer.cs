using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Transport.Models;

namespace OrbitMesh.Host.Services.P2P;

/// <summary>
/// Embedded STUN server for NAT type detection.
/// Implements a simplified STUN protocol (RFC 5389) for NAT analysis.
/// </summary>
public class EmbeddedStunServer : BackgroundService, IStunServer
{
    private readonly P2PServerOptions _options;
    private readonly ILogger<EmbeddedStunServer> _logger;

    private UdpClient? _primarySocket;
    private UdpClient? _secondarySocket;
    private readonly List<IPEndPoint> _serverEndpoints = new();

    // STUN magic cookie (RFC 5389)
    private static readonly byte[] MagicCookie = [0x21, 0x12, 0xA4, 0x42];

    // STUN message types
    private const ushort BindingRequest = 0x0001;
    private const ushort BindingResponse = 0x0101;

    // STUN attribute types
    private const ushort AttrMappedAddress = 0x0001;
    private const ushort AttrXorMappedAddress = 0x0020;
    private const ushort AttrChangeRequest = 0x0003;

    public bool IsRunning { get; private set; }

    public EmbeddedStunServer(
        IOptions<P2PServerOptions> options,
        ILogger<EmbeddedStunServer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.EnableEmbeddedStun)
        {
            _logger.LogInformation("Embedded STUN server is disabled");
            return;
        }

        try
        {
            await StartStunServerAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start embedded STUN server");
        }
    }

    private async Task StartStunServerAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Bind primary socket
            _primarySocket = new UdpClient(_options.StunPort);
            _serverEndpoints.Add(new IPEndPoint(IPAddress.Any, _options.StunPort));
            _logger.LogInformation("STUN server primary socket bound to port {Port}", _options.StunPort);

            // Bind secondary socket for NAT type detection
            _secondarySocket = new UdpClient(_options.StunSecondaryPort);
            _serverEndpoints.Add(new IPEndPoint(IPAddress.Any, _options.StunSecondaryPort));
            _logger.LogInformation("STUN server secondary socket bound to port {Port}", _options.StunSecondaryPort);

            IsRunning = true;

            // Process requests on both sockets concurrently
            var primaryTask = ProcessRequestsAsync(_primarySocket, false, stoppingToken);
            var secondaryTask = ProcessRequestsAsync(_secondarySocket, true, stoppingToken);

            await Task.WhenAll(primaryTask, secondaryTask);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Socket error in STUN server. Port may be in use.");
            throw;
        }
        finally
        {
            IsRunning = false;
            _primarySocket?.Dispose();
            _secondarySocket?.Dispose();
        }
    }

    private async Task ProcessRequestsAsync(UdpClient socket, bool isSecondary, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting STUN request processing on {SocketType} socket",
            isSecondary ? "secondary" : "primary");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveAsync(stoppingToken);
                await ProcessStunRequestAsync(socket, result, isSecondary, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing STUN request");
            }
        }
    }

    private async Task ProcessStunRequestAsync(
        UdpClient socket,
        UdpReceiveResult result,
        bool isSecondary,
        CancellationToken cancellationToken)
    {
        var data = result.Buffer;
        var remoteEndPoint = result.RemoteEndPoint;

        if (data.Length < 20)
        {
            _logger.LogDebug("Received packet too small to be STUN from {EndPoint}", remoteEndPoint);
            return;
        }

        // Verify magic cookie
        if (data[4] != MagicCookie[0] || data[5] != MagicCookie[1] ||
            data[6] != MagicCookie[2] || data[7] != MagicCookie[3])
        {
            _logger.LogDebug("Invalid magic cookie from {EndPoint}", remoteEndPoint);
            return;
        }

        var messageType = (ushort)((data[0] << 8) | data[1]);
        if (messageType != BindingRequest)
        {
            _logger.LogDebug("Non-binding request from {EndPoint}: type={Type:X4}", remoteEndPoint, messageType);
            return;
        }

        // Extract transaction ID
        var transactionId = new byte[12];
        Array.Copy(data, 8, transactionId, 0, 12);

        _logger.LogDebug("STUN Binding Request from {EndPoint} on {SocketType} socket",
            remoteEndPoint, isSecondary ? "secondary" : "primary");

        // Check for CHANGE-REQUEST attribute (for NAT type detection)
        var changeIp = false;
        var changePort = false;
        ParseChangeRequest(data, out changeIp, out changePort);

        // Determine which socket to respond from
        var responseSocket = socket;
        if (changePort && !isSecondary && _secondarySocket != null)
        {
            responseSocket = _secondarySocket;
        }

        // Build and send response
        var response = BuildBindingResponse(transactionId, remoteEndPoint);
        await responseSocket.SendAsync(response, remoteEndPoint, cancellationToken);

        _logger.LogDebug("Sent STUN Binding Response to {EndPoint}", remoteEndPoint);
    }

    private static void ParseChangeRequest(byte[] data, out bool changeIp, out bool changePort)
    {
        changeIp = false;
        changePort = false;

        if (data.Length < 24)
        {
            return;
        }

        // Parse attributes starting at byte 20
        var offset = 20;
        var messageLength = (data[2] << 8) | data[3];
        var endOffset = Math.Min(20 + messageLength, data.Length);

        while (offset + 4 <= endOffset)
        {
            var attrType = (ushort)((data[offset] << 8) | data[offset + 1]);
            var attrLength = (ushort)((data[offset + 2] << 8) | data[offset + 3]);

            if (attrType == AttrChangeRequest && attrLength >= 4 && offset + 8 <= endOffset)
            {
                var flags = (data[offset + 4] << 24) | (data[offset + 5] << 16) |
                           (data[offset + 6] << 8) | data[offset + 7];
                changeIp = (flags & 0x04) != 0;
                changePort = (flags & 0x02) != 0;
                return;
            }

            // Move to next attribute (4-byte aligned)
            offset += 4 + ((attrLength + 3) & ~3);
        }
    }

    private static byte[] BuildBindingResponse(byte[] transactionId, IPEndPoint clientEndPoint)
    {
        var response = new List<byte>();

        // Message type: Binding Response
        response.Add((byte)(BindingResponse >> 8));
        response.Add((byte)(BindingResponse & 0xFF));

        // Placeholder for message length (will be filled later)
        var lengthIndex = response.Count;
        response.Add(0);
        response.Add(0);

        // Magic cookie
        response.AddRange(MagicCookie);

        // Transaction ID
        response.AddRange(transactionId);

        // Add XOR-MAPPED-ADDRESS attribute
        var xorMapped = BuildXorMappedAddressAttribute(clientEndPoint, transactionId);
        response.AddRange(xorMapped);

        // Add MAPPED-ADDRESS attribute for compatibility
        var mapped = BuildMappedAddressAttribute(clientEndPoint);
        response.AddRange(mapped);

        // Update message length (excluding 20-byte header)
        var messageLength = response.Count - 20;
        response[lengthIndex] = (byte)(messageLength >> 8);
        response[lengthIndex + 1] = (byte)(messageLength & 0xFF);

        return response.ToArray();
    }

    private static byte[] BuildXorMappedAddressAttribute(IPEndPoint endPoint, byte[] transactionId)
    {
        var attr = new List<byte>();

        // Attribute type
        attr.Add((byte)(AttrXorMappedAddress >> 8));
        attr.Add((byte)(AttrXorMappedAddress & 0xFF));

        // Attribute length (8 for IPv4)
        attr.Add(0);
        attr.Add(8);

        // Reserved
        attr.Add(0);

        // Address family (IPv4 = 0x01)
        attr.Add(0x01);

        // XOR'd port
        var port = endPoint.Port;
        var xorPort = (ushort)(port ^ 0x2112); // XOR with magic cookie high bits
        attr.Add((byte)(xorPort >> 8));
        attr.Add((byte)(xorPort & 0xFF));

        // XOR'd address
        var addressBytes = endPoint.Address.GetAddressBytes();
        attr.Add((byte)(addressBytes[0] ^ MagicCookie[0]));
        attr.Add((byte)(addressBytes[1] ^ MagicCookie[1]));
        attr.Add((byte)(addressBytes[2] ^ MagicCookie[2]));
        attr.Add((byte)(addressBytes[3] ^ MagicCookie[3]));

        return attr.ToArray();
    }

    private static byte[] BuildMappedAddressAttribute(IPEndPoint endPoint)
    {
        var attr = new List<byte>();

        // Attribute type
        attr.Add((byte)(AttrMappedAddress >> 8));
        attr.Add((byte)(AttrMappedAddress & 0xFF));

        // Attribute length (8 for IPv4)
        attr.Add(0);
        attr.Add(8);

        // Reserved
        attr.Add(0);

        // Address family (IPv4 = 0x01)
        attr.Add(0x01);

        // Port
        var port = endPoint.Port;
        attr.Add((byte)(port >> 8));
        attr.Add((byte)(port & 0xFF));

        // Address
        attr.AddRange(endPoint.Address.GetAddressBytes());

        return attr.ToArray();
    }

    /// <inheritdoc />
    public Task<NatInfo> AnalyzeNatAsync(
        IPAddress? clientAddress,
        int clientPort,
        string? localAddress = null,
        int? localPort = null,
        CancellationToken cancellationToken = default)
    {
        if (clientAddress is null)
        {
            // Cannot determine NAT without client address
            return Task.FromResult(CreateNatInfo(NatType.Unknown, "0.0.0.0", 0, localAddress, localPort));
        }

        var publicAddress = clientAddress.ToString();

        // Determine NAT type based on available information
        var natType = DetermineNatType(clientAddress, clientPort, localAddress, localPort);

        var natInfo = CreateNatInfo(natType, publicAddress, clientPort, localAddress, localPort);

        _logger.LogDebug(
            "NAT analysis result: Type={NatType}, Public={PublicAddress}:{PublicPort}, Local={LocalAddress}:{LocalPort}",
            natType,
            publicAddress,
            clientPort,
            localAddress ?? "unknown",
            localPort?.ToString(CultureInfo.InvariantCulture) ?? "unknown");

        return Task.FromResult(natInfo);
    }

    private static NatType DetermineNatType(
        IPAddress clientAddress,
        int clientPort,
        string? localAddress,
        int? localPort)
    {
        // If client is using a private IP that's visible to us, they're likely on same network
        if (IsPrivateAddress(clientAddress))
        {
            return NatType.Open;
        }

        // If local address matches public address, no NAT
        if (!string.IsNullOrEmpty(localAddress) &&
            IPAddress.TryParse(localAddress, out var localIp) &&
            localIp.Equals(clientAddress))
        {
            return NatType.Open;
        }

        // If we have both local and public info, check for port preservation
        if (localPort.HasValue)
        {
            if (localPort.Value == clientPort)
            {
                // Port preserved - likely FullCone or Restricted
                return NatType.FullCone;
            }
            else
            {
                // Port changed - could be PortRestricted or Symmetric
                // Without additional tests, assume PortRestricted as more common
                return NatType.PortRestricted;
            }
        }

        // Default assumption without full NAT detection
        // Most home NATs are FullCone or Restricted
        return NatType.Restricted;
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false; // Not IPv4
        }

        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        // 127.0.0.0/8 (loopback)
        if (bytes[0] == 127)
        {
            return true;
        }

        return false;
    }

    private static NatInfo CreateNatInfo(
        NatType type,
        string publicAddress,
        int publicPort,
        string? localAddress,
        int? localPort)
    {
        return new NatInfo
        {
            Type = type,
            PublicAddress = publicAddress,
            PublicPort = publicPort,
            LocalAddress = localAddress,
            LocalPort = localPort,
            MappingLifetime = TimeSpan.FromMinutes(5), // Common NAT mapping timeout
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<IPEndPoint> GetServerEndpoints()
    {
        return _serverEndpoints.AsReadOnly();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _primarySocket?.Dispose();
        _secondarySocket?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
