using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Contracts;
using OrbitMesh.Core.Transport;
using OrbitMesh.Core.Transport.Models;
using OrbitMesh.Host.Services.P2P;
using OrbitMesh.Node.Extensions;
using OrbitMesh.Node.P2P;
using OrbitMesh.Transport.P2P;

namespace OrbitMesh.Integration.Tests;

/// <summary>
/// Integration tests for P2P NAT Traversal functionality.
/// </summary>
public class P2PIntegrationTests
{
    #region Type Availability Tests

    [Fact]
    public void P2P_Core_Types_Are_Available()
    {
        // Transport abstractions
        typeof(ITransport).Should().NotBeNull();
        typeof(TransportType).Should().NotBeNull();
        typeof(TransportState).Should().NotBeNull();

        // Transport models
        typeof(IceCandidate).Should().NotBeNull();
        typeof(IceCandidateType).Should().NotBeNull();
        typeof(NatInfo).Should().NotBeNull();
        typeof(NatType).Should().NotBeNull();
        typeof(PeerConnectionState).Should().NotBeNull();
        typeof(PeerConnectionStatus).Should().NotBeNull();
        typeof(ConnectionStrategy).Should().NotBeNull();
    }

    [Fact]
    public void P2P_Contract_Types_Are_Available()
    {
        // P2P contracts
        typeof(IP2PAgentClient).Should().NotBeNull();
        typeof(IP2PServerHub).Should().NotBeNull();
        typeof(IP2PCapableAgent).Should().NotBeNull();
    }

    [Fact]
    public void P2P_Transport_Types_Are_Available()
    {
        // LiteNetLib transport
        typeof(LiteNetP2PTransport).Should().NotBeNull();
        typeof(PeerConnectionManager).Should().NotBeNull();
        typeof(IceGatherer).Should().NotBeNull();
        typeof(P2POptions).Should().NotBeNull();
    }

    [Fact]
    public void P2P_Host_Types_Are_Available()
    {
        // Host services
        typeof(IPeerCoordinator).Should().NotBeNull();
        typeof(PeerCoordinator).Should().NotBeNull();
        typeof(IStunServer).Should().NotBeNull();
        typeof(EmbeddedStunServer).Should().NotBeNull();
        typeof(P2PServerOptions).Should().NotBeNull();
    }

    [Fact]
    public void P2P_Node_Types_Are_Available()
    {
        // Node extensions
        typeof(P2PAgentHandler).Should().NotBeNull();
        typeof(P2PMeshAgentBuilder).Should().NotBeNull();
        typeof(IP2PSignalingProxy).Should().NotBeNull();
        typeof(SignalRP2PSignalingProxy).Should().NotBeNull();
    }

    #endregion

    #region NAT Type Detection Tests

    [Theory]
    [InlineData(NatType.Open, NatType.Open, ConnectionStrategy.DirectConnect)]
    [InlineData(NatType.FullCone, NatType.FullCone, ConnectionStrategy.DirectConnect)]
    [InlineData(NatType.FullCone, NatType.Restricted, ConnectionStrategy.DirectConnect)]
    [InlineData(NatType.Restricted, NatType.Restricted, ConnectionStrategy.SimultaneousOpen)]
    [InlineData(NatType.PortRestricted, NatType.PortRestricted, ConnectionStrategy.SimultaneousOpen)]
    [InlineData(NatType.Symmetric, NatType.Open, ConnectionStrategy.TurnRelay)]
    [InlineData(NatType.Symmetric, NatType.Symmetric, ConnectionStrategy.TurnRelay)]
    public void PeerCoordinator_DetermineStrategy_Returns_Correct_Strategy(
        NatType initiatorNat,
        NatType responderNat,
        ConnectionStrategy expectedStrategy)
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var options = Options.Create(new P2PServerOptions());
        var logger = NullLogger<PeerCoordinator>.Instance;
        var coordinator = new PeerCoordinator(agentRegistry.Object, options, logger);

        var initiatorNatInfo = CreateNatInfo(initiatorNat);
        var responderNatInfo = CreateNatInfo(responderNat);

        // Act
        var strategy = coordinator.DetermineStrategy(initiatorNatInfo, responderNatInfo);

        // Assert
        strategy.Should().Be(expectedStrategy);
    }

    [Fact]
    public void PeerCoordinator_DetermineStrategy_With_Null_NatInfo_Returns_UdpHolePunch()
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var options = Options.Create(new P2PServerOptions());
        var logger = NullLogger<PeerCoordinator>.Instance;
        var coordinator = new PeerCoordinator(agentRegistry.Object, options, logger);

        // Act
        var strategy = coordinator.DetermineStrategy(null, null);

        // Assert
        strategy.Should().Be(ConnectionStrategy.UdpHolePunch);
    }

    [Fact]
    public void PeerCoordinator_CacheNatInfo_And_GetCachedNatInfo_Works()
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var options = Options.Create(new P2PServerOptions());
        var logger = NullLogger<PeerCoordinator>.Instance;
        var coordinator = new PeerCoordinator(agentRegistry.Object, options, logger);

        var agentId = "test-agent-1";
        var natInfo = CreateNatInfo(NatType.FullCone);

        // Act
        coordinator.CacheNatInfo(agentId, natInfo);
        var retrieved = coordinator.GetCachedNatInfo(agentId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be(NatType.FullCone);
    }

    [Fact]
    public void PeerCoordinator_GetCachedNatInfo_Returns_Null_For_Unknown_Agent()
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var options = Options.Create(new P2PServerOptions());
        var logger = NullLogger<PeerCoordinator>.Instance;
        var coordinator = new PeerCoordinator(agentRegistry.Object, options, logger);

        // Act
        var retrieved = coordinator.GetCachedNatInfo("unknown-agent");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region ICE Candidate Tests

    [Fact]
    public async Task IceGatherer_GatherCandidates_Returns_Host_Candidates()
    {
        // Arrange
        var options = Options.Create(new P2POptions());
        var logger = NullLogger<IceGatherer>.Instance;
        var gatherer = new IceGatherer(options, logger);

        var localPort = 12345;
        var natInfo = CreateNatInfo(NatType.Open);

        // Act
        var candidates = await gatherer.GatherCandidatesAsync(localPort, natInfo);

        // Assert
        candidates.Should().NotBeEmpty();
        candidates.Should().Contain(c => c.Type == IceCandidateType.Host);
    }

    [Fact]
    public async Task IceGatherer_GatherCandidates_Includes_ServerReflexive_When_NatInfo_Available()
    {
        // Arrange
        var options = Options.Create(new P2POptions());
        var logger = NullLogger<IceGatherer>.Instance;
        var gatherer = new IceGatherer(options, logger);

        var localPort = 12345;
        var natInfo = CreateNatInfo(NatType.FullCone, "203.0.113.1", 54321);

        // Act
        var candidates = await gatherer.GatherCandidatesAsync(localPort, natInfo);

        // Assert
        candidates.Should().Contain(c => c.Type == IceCandidateType.ServerReflexive);
        var srflxCandidate = candidates.First(c => c.Type == IceCandidateType.ServerReflexive);
        srflxCandidate.Address.Should().Be("203.0.113.1");
        srflxCandidate.Port.Should().Be(54321);
    }

    [Fact]
    public void IceCandidate_ToEndPoint_Returns_Correct_EndPoint()
    {
        // Arrange
        var candidate = new IceCandidate
        {
            CandidateId = "test-1",
            Type = IceCandidateType.Host,
            Address = "192.168.1.100",
            Port = 12345,
            Priority = 100
        };

        // Act
        var endPoint = candidate.ToEndPoint();

        // Assert
        endPoint.Should().NotBeNull();
        endPoint.Address.ToString().Should().Be("192.168.1.100");
        endPoint.Port.Should().Be(12345);
    }

    [Fact]
    public void IceCandidate_Priority_Calculation_Prefers_Host_Over_Relayed()
    {
        // Arrange
        var hostCandidate = new IceCandidate
        {
            CandidateId = "host-1",
            Type = IceCandidateType.Host,
            Address = "192.168.1.100",
            Port = 12345,
            Priority = IceCandidate.CalculatePriority(IceCandidateType.Host, 0)
        };

        var relayedCandidate = new IceCandidate
        {
            CandidateId = "relay-1",
            Type = IceCandidateType.Relayed,
            Address = "10.0.0.1",
            Port = 54321,
            Priority = IceCandidate.CalculatePriority(IceCandidateType.Relayed, 0)
        };

        // Assert
        hostCandidate.Priority.Should().BeGreaterThan(relayedCandidate.Priority);
    }

    #endregion

    #region P2P Transport Tests

    [Fact]
    public async Task LiteNetP2PTransport_Creates_With_Correct_Properties()
    {
        // Arrange
        var agentId = "test-agent";
        var options = Options.Create(new P2POptions { LocalPort = 0 });
        var logger = NullLogger<LiteNetP2PTransport>.Instance;

        // Act
        await using var transport = new LiteNetP2PTransport(agentId, options, logger);

        // Assert
        transport.TransportId.Should().Be($"p2p-{agentId}");
        transport.Type.Should().Be(TransportType.P2P);
        transport.State.Should().Be(TransportState.Disconnected);
        transport.AgentId.Should().Be(agentId);
    }

    [Fact]
    public async Task LiteNetP2PTransport_Connect_Changes_State_To_Connected()
    {
        // Arrange
        var agentId = "test-agent";
        var options = Options.Create(new P2POptions { LocalPort = 0 });
        var logger = NullLogger<LiteNetP2PTransport>.Instance;
        var transport = new LiteNetP2PTransport(agentId, options, logger);

        // Act
        await transport.ConnectAsync();

        // Assert
        transport.State.Should().Be(TransportState.Connected);
        transport.LocalPort.Should().BeGreaterThan(0);

        // Cleanup
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task LiteNetP2PTransport_Disconnect_Changes_State_To_Disconnected()
    {
        // Arrange
        var agentId = "test-agent";
        var options = Options.Create(new P2POptions { LocalPort = 0 });
        var logger = NullLogger<LiteNetP2PTransport>.Instance;
        var transport = new LiteNetP2PTransport(agentId, options, logger);

        await transport.ConnectAsync();

        // Act
        await transport.DisconnectAsync();

        // Assert
        transport.State.Should().Be(TransportState.Disconnected);

        // Cleanup
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task LiteNetP2PTransport_Raises_StateChanged_Event()
    {
        // Arrange
        var agentId = "test-agent";
        var options = Options.Create(new P2POptions { LocalPort = 0 });
        var logger = NullLogger<LiteNetP2PTransport>.Instance;
        var transport = new LiteNetP2PTransport(agentId, options, logger);

        var stateChanges = new List<TransportState>();
        transport.StateChanged += (_, e) => stateChanges.Add(e.NewState);

        // Act
        await transport.ConnectAsync();
        await transport.DisconnectAsync();

        // Assert
        stateChanges.Should().Contain(TransportState.Connected);
        stateChanges.Should().Contain(TransportState.Disconnected);

        // Cleanup
        await transport.DisposeAsync();
    }

    #endregion

    #region PeerConnectionManager Tests

    [Fact]
    public void PeerConnectionManager_GetConnectionState_Returns_Null_For_Unknown_Peer()
    {
        // Arrange
        var (manager, _, _) = CreatePeerConnectionManager();

        // Act
        var state = manager.GetConnectionState("unknown-peer");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void PeerConnectionManager_Connections_Initially_Empty()
    {
        // Arrange
        var (manager, _, _) = CreatePeerConnectionManager();

        // Act & Assert
        manager.Connections.Should().BeEmpty();
    }

    #endregion

    #region P2P Options Tests

    [Fact]
    public void P2POptions_Has_Sensible_Defaults()
    {
        // Arrange & Act
        var options = new P2POptions();

        // Assert
        options.PreferP2P.Should().BeTrue();
        options.FallbackToRelay.Should().BeTrue();
        options.LocalPort.Should().Be(0); // Auto-select
        options.StunPort.Should().Be(3478);
        options.TurnPort.Should().Be(3478);
        options.ConnectionTimeoutMs.Should().Be(10000);
        options.NatPunchRetries.Should().Be(10);
        options.KeepAliveIntervalMs.Should().Be(15000);
        options.DisconnectTimeoutMs.Should().Be(30000);
        options.MaxMtu.Should().Be(1400);
    }

    [Fact]
    public void P2PServerOptions_Has_Sensible_Defaults()
    {
        // Arrange & Act
        var options = new P2PServerOptions();

        // Assert
        options.Enabled.Should().BeFalse(); // Opt-in
        options.StunPort.Should().Be(3478);
        options.StunSecondaryPort.Should().Be(3479);
        options.EnableEmbeddedStun.Should().BeTrue();
        options.AllowRelayFallback.Should().BeTrue();
        options.PeerHealthCheckIntervalSeconds.Should().Be(30);
        options.NatDetectionTimeoutSeconds.Should().Be(5);
    }

    #endregion

    #region P2PAgentHandler Tests

    [Fact]
    public async Task P2PAgentHandler_Creates_With_Correct_AgentId()
    {
        // Arrange
        var agentId = "test-agent-123";
        var options = new P2POptions();
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        await using var handler = new P2PAgentHandler(agentId, options, loggerFactory);

        // Assert
        handler.AgentId.Should().Be(agentId);
        handler.IsReady.Should().BeFalse(); // Not initialized yet
    }

    [Fact]
    public async Task P2PAgentHandler_Transport_Is_P2P_Type()
    {
        // Arrange
        var agentId = "test-agent";
        var options = new P2POptions();
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        await using var handler = new P2PAgentHandler(agentId, options, loggerFactory);

        // Assert
        handler.Transport.Type.Should().Be(TransportType.P2P);
    }

    #endregion

    #region P2PMeshAgentBuilder Tests

    [Fact]
    public void P2PMeshAgentBuilder_Creates_Agent_With_P2P_Capability()
    {
        // Arrange & Act
        var builder = new P2PMeshAgentBuilder("https://localhost:5000")
            .WithId("test-agent")
            .WithName("Test Agent")
            .PreferP2P()
            .WithRelayFallback();

        var (agent, p2pHandler) = builder.Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Id.Should().Be("test-agent");
        agent.Name.Should().Be("Test Agent");

        p2pHandler.Should().NotBeNull();
        p2pHandler.AgentId.Should().Be("test-agent");
    }

    [Fact]
    public void MeshAgentBuilder_WithP2PTransport_Extension_Works()
    {
        // Arrange
        var builder = new OrbitMesh.Node.MeshAgentBuilder("https://localhost:5000")
            .WithId("test-agent");

        // Act
        var p2pBuilder = builder.WithP2PTransport(opt =>
        {
            opt.PreferP2P = true;
            opt.StunServer = "stun.example.com";
        });

        // Assert
        p2pBuilder.Should().NotBeNull();
        p2pBuilder.Should().BeOfType<P2PMeshAgentBuilder>();
    }

    #endregion

    #region Connection Request/Response Tests

    [Fact]
    public void PeerConnectionRequest_Creates_With_UniqueRequestId()
    {
        // Arrange & Act
        var request1 = new PeerConnectionRequest
        {
            FromAgentId = "agent-1",
            NatInfo = CreateNatInfo(NatType.FullCone),
            Candidates = new List<IceCandidate>()
        };

        var request2 = new PeerConnectionRequest
        {
            FromAgentId = "agent-1",
            NatInfo = CreateNatInfo(NatType.FullCone),
            Candidates = new List<IceCandidate>()
        };

        // Assert
        request1.RequestId.Should().NotBeNullOrEmpty();
        request2.RequestId.Should().NotBeNullOrEmpty();
        request1.RequestId.Should().NotBe(request2.RequestId);
    }

    [Fact]
    public void PeerConnectionResponse_Can_Be_Accepted_Or_Rejected()
    {
        // Arrange & Act
        var acceptedResponse = new PeerConnectionResponse
        {
            RequestId = "req-1",
            FromAgentId = "agent-2",
            Accepted = true,
            NatInfo = CreateNatInfo(NatType.FullCone),
            Candidates = new List<IceCandidate>(),
            Strategy = ConnectionStrategy.UdpHolePunch
        };

        var rejectedResponse = new PeerConnectionResponse
        {
            RequestId = "req-2",
            FromAgentId = "agent-3",
            Accepted = false,
            RejectionReason = "P2P not supported"
        };

        // Assert
        acceptedResponse.Accepted.Should().BeTrue();
        acceptedResponse.RejectionReason.Should().BeNull();

        rejectedResponse.Accepted.Should().BeFalse();
        rejectedResponse.RejectionReason.Should().Be("P2P not supported");
    }

    #endregion

    #region PeerConnectionState Tests

    [Fact]
    public void PeerConnectionState_Tracks_Connection_Lifecycle()
    {
        // Arrange & Act
        var newState = new PeerConnectionState
        {
            PeerId = "peer-1",
            Status = PeerConnectionStatus.New
        };

        var gatheringState = newState with { Status = PeerConnectionStatus.Gathering };
        var connectingState = gatheringState with { Status = PeerConnectionStatus.Connecting };
        var connectedState = connectingState with
        {
            Status = PeerConnectionStatus.Connected,
            ActiveTransport = TransportType.P2P,
            ConnectedAt = DateTimeOffset.UtcNow
        };

        // Assert
        newState.Status.Should().Be(PeerConnectionStatus.New);
        gatheringState.Status.Should().Be(PeerConnectionStatus.Gathering);
        connectingState.Status.Should().Be(PeerConnectionStatus.Connecting);
        connectedState.Status.Should().Be(PeerConnectionStatus.Connected);
        connectedState.ActiveTransport.Should().Be(TransportType.P2P);
        connectedState.ConnectedAt.Should().NotBeNull();
    }

    [Fact]
    public void PeerConnectionState_Can_Include_Metrics()
    {
        // Arrange & Act
        var state = new PeerConnectionState
        {
            PeerId = "peer-1",
            Status = PeerConnectionStatus.Connected,
            ActiveTransport = TransportType.P2P,
            Metrics = new PeerConnectionMetrics
            {
                RoundTripTime = TimeSpan.FromMilliseconds(50),
                BytesSent = 1024000,
                BytesReceived = 2048000,
                PacketLossRate = 0.01
            }
        };

        // Assert
        state.Metrics.Should().NotBeNull();
        state.Metrics!.RoundTripTime.Should().Be(TimeSpan.FromMilliseconds(50));
        state.Metrics.BytesSent.Should().Be(1024000);
        state.Metrics.BytesReceived.Should().Be(2048000);
        state.Metrics.PacketLossRate.Should().Be(0.01);
    }

    #endregion

    #region Helper Methods

    private static NatInfo CreateNatInfo(
        NatType type,
        string publicAddress = "203.0.113.50",
        int publicPort = 54321)
    {
        return new NatInfo
        {
            Type = type,
            PublicAddress = publicAddress,
            PublicPort = publicPort,
            MappingLifetime = TimeSpan.FromMinutes(5),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static (PeerConnectionManager Manager, LiteNetP2PTransport Transport, IceGatherer Gatherer)
        CreatePeerConnectionManager()
    {
        var agentId = "test-agent";
        var options = Options.Create(new P2POptions { LocalPort = 0 });

        var transportLogger = NullLogger<LiteNetP2PTransport>.Instance;
        var transport = new LiteNetP2PTransport(agentId, options, transportLogger);

        var gathererLogger = NullLogger<IceGatherer>.Instance;
        var gatherer = new IceGatherer(options, gathererLogger);

        var managerLogger = NullLogger<PeerConnectionManager>.Instance;
        var manager = new PeerConnectionManager(transport, gatherer, options, managerLogger);

        return (manager, transport, gatherer);
    }

    #endregion
}
