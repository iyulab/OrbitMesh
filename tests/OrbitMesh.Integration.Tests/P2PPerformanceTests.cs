using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbitMesh.Core.Transport;
using OrbitMesh.Core.Transport.Models;
using OrbitMesh.Host.Services.P2P;
using OrbitMesh.Transport.P2P;

namespace OrbitMesh.Integration.Tests;

/// <summary>
/// Performance benchmark tests for P2P transport components.
/// These tests measure execution time and throughput of critical P2P operations.
/// </summary>
[Trait("Category", "Performance")]
public class P2PPerformanceTests
{
    private readonly P2POptions _p2pOptions = new()
    {
        StunServer = "stun.l.google.com",
        StunPort = 19302,
        TurnServer = "turn.example.com",
        TurnPort = 3478,
        TurnUsername = "testuser",
        TurnPassword = "testpass",
        ConnectionTimeoutMs = 30000,
        FallbackToRelay = true
    };

    private readonly P2PServerOptions _serverOptions = new();

    #region ICE Candidate Gathering Performance

    [Fact]
    public async Task IceGatherer_GatherHostCandidates_ShouldCompleteQuickly()
    {
        // Arrange
        var gatherer = new IceGatherer(
            Options.Create(_p2pOptions),
            NullLogger<IceGatherer>.Instance);
        var sw = Stopwatch.StartNew();

        // Act
        var candidates = await gatherer.GatherCandidatesAsync(5000, null, CancellationToken.None);
        sw.Stop();

        // Assert - Host candidate gathering should be reasonably fast
        // Note: Network interface enumeration can take longer on some systems
        candidates.Should().NotBeEmpty();
        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Host candidate gathering should complete in under 1s");

        // Output metrics
        OutputMetric("ICE_HostGatherTime_ms", sw.ElapsedMilliseconds);
        OutputMetric("ICE_HostCandidateCount", candidates.Count);
    }

    [Fact]
    public async Task IceGatherer_GatherWithNatInfo_ShouldAddServerReflexive()
    {
        // Arrange
        var gatherer = new IceGatherer(
            Options.Create(_p2pOptions),
            NullLogger<IceGatherer>.Instance);

        var natInfo = new NatInfo
        {
            Type = NatType.FullCone,
            PublicAddress = "203.0.113.1",
            PublicPort = 54321,
            LocalAddress = "192.168.1.100",
            LocalPort = 5000
        };

        var sw = Stopwatch.StartNew();

        // Act
        var candidates = await gatherer.GatherCandidatesAsync(5000, natInfo, CancellationToken.None);
        sw.Stop();

        // Assert
        candidates.Should().Contain(c => c.Type == IceCandidateType.ServerReflexive);
        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Candidate gathering with NAT info should complete in under 1s");

        OutputMetric("ICE_WithNatGatherTime_ms", sw.ElapsedMilliseconds);
    }

    [Fact]
    public void IceCandidate_PriorityCalculation_ShouldBeFast()
    {
        // Arrange
        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            _ = IceCandidate.CalculatePriority(IceCandidateType.Host, i % 256);
            _ = IceCandidate.CalculatePriority(IceCandidateType.ServerReflexive, i % 256);
            _ = IceCandidate.CalculatePriority(IceCandidateType.Relayed, i % 256);
        }
        sw.Stop();

        // Assert - 100k * 3 calculations should complete in < 50ms
        var perCalculationNs = sw.Elapsed.TotalNanoseconds / (iterations * 3);
        sw.ElapsedMilliseconds.Should().BeLessThan(50,
            "300k priority calculations should complete in under 50ms");

        OutputMetric("ICE_PriorityCalcPerOp_ns", perCalculationNs);
        OutputMetric("ICE_PriorityCalc300kTotal_ms", sw.ElapsedMilliseconds);
    }

    #endregion

    #region NAT Detection Performance

    [Fact]
    public void NatType_StrategyDetermination_ShouldBeFast()
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var coordinator = new PeerCoordinator(
            agentRegistry.Object,
            Options.Create(_serverOptions),
            NullLogger<PeerCoordinator>.Instance);

        var natTypes = Enum.GetValues<NatType>();
        const int iterations = 10_000;
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            foreach (var initiator in natTypes)
            {
                foreach (var responder in natTypes)
                {
                    var initNat = new NatInfo
                    {
                        Type = initiator,
                        PublicAddress = "1.2.3.4",
                        PublicPort = 1234
                    };
                    var respNat = new NatInfo
                    {
                        Type = responder,
                        PublicAddress = "5.6.7.8",
                        PublicPort = 5678
                    };
                    _ = coordinator.DetermineStrategy(initNat, respNat);
                }
            }
        }
        sw.Stop();

        // Assert - 10k * 6 * 6 = 360k strategy determinations should be fast
        var totalOperations = iterations * natTypes.Length * natTypes.Length;
        var perOperationNs = sw.Elapsed.TotalNanoseconds / totalOperations;
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "360k strategy determinations should complete in under 500ms");

        OutputMetric("NAT_StrategyPerOp_ns", perOperationNs);
        OutputMetric("NAT_Strategy360kTotal_ms", sw.ElapsedMilliseconds);
    }

    [Fact]
    public void PeerCoordinator_StrategyMatrix_Coverage()
    {
        // Arrange
        var agentRegistry = new Mock<OrbitMesh.Host.Services.IAgentRegistry>();
        var coordinator = new PeerCoordinator(
            agentRegistry.Object,
            Options.Create(_serverOptions),
            NullLogger<PeerCoordinator>.Instance);

        var results = new Dictionary<(NatType, NatType), ConnectionStrategy>();
        var natTypes = Enum.GetValues<NatType>();

        // Act - Test all NAT type combinations
        foreach (var initiator in natTypes)
        {
            foreach (var responder in natTypes)
            {
                var initNat = new NatInfo
                {
                    Type = initiator,
                    PublicAddress = "1.2.3.4",
                    PublicPort = 1234
                };
                var respNat = new NatInfo
                {
                    Type = responder,
                    PublicAddress = "5.6.7.8",
                    PublicPort = 5678
                };
                var strategy = coordinator.DetermineStrategy(initNat, respNat);
                results[(initiator, responder)] = strategy;
            }
        }

        // Assert - Verify strategy matrix coverage
        results.Should().HaveCount(natTypes.Length * natTypes.Length);

        // Symmetric NAT should always result in TurnRelay
        foreach (var other in natTypes)
        {
            results[(NatType.Symmetric, other)].Should().Be(ConnectionStrategy.TurnRelay);
            results[(other, NatType.Symmetric)].Should().Be(ConnectionStrategy.TurnRelay);
        }

        // Open NAT should result in DirectConnect
        results[(NatType.Open, NatType.Open)].Should().Be(ConnectionStrategy.DirectConnect);

        OutputMetric("NAT_StrategyMatrixSize", results.Count);
    }

    #endregion

    #region Transport Lifecycle Performance

    [Fact]
    public async Task LiteNetTransport_Startup_ShouldBeQuick()
    {
        // Arrange
        await using var transport = new LiteNetP2PTransport(
            "perf-test-agent",
            Options.Create(_p2pOptions),
            NullLogger<LiteNetP2PTransport>.Instance);

        var sw = Stopwatch.StartNew();

        // Act
        await transport.ConnectAsync(CancellationToken.None);
        sw.Stop();

        // Assert - Transport startup should be < 100ms
        transport.State.Should().Be(TransportState.Connected);
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "Transport startup should complete in under 100ms");

        OutputMetric("Transport_StartupTime_ms", sw.ElapsedMilliseconds);
    }

    [Fact]
    public async Task LiteNetTransport_MultipleStartStop_ShouldBeStable()
    {
        // Arrange
        const int cycles = 10;
        var times = new List<long>();

        // Act
        for (var i = 0; i < cycles; i++)
        {
            await using var transport = new LiteNetP2PTransport(
                $"cycle-test-{i}",
                Options.Create(_p2pOptions),
                NullLogger<LiteNetP2PTransport>.Instance);

            var sw = Stopwatch.StartNew();
            await transport.ConnectAsync(CancellationToken.None);
            await transport.DisconnectAsync(CancellationToken.None);
            sw.Stop();

            times.Add(sw.ElapsedMilliseconds);
        }

        // Assert - All cycles should complete and be reasonably consistent
        times.Should().HaveCount(cycles);
        var avg = times.Average();
        var max = times.Max();
        // Use absolute threshold since avg can be very small (1-2ms) making multipliers unreliable
        // This test primarily verifies stability (no crashes) rather than strict timing consistency
        max.Should().BeLessThan(500, "Each cycle should complete in under 500ms");

        OutputMetric("Transport_CycleAvg_ms", avg);
        OutputMetric("Transport_CycleMax_ms", max);
        OutputMetric("Transport_CycleCount", cycles);
    }

    #endregion

    #region Connection Manager Performance

    [Fact]
    public void ConnectionState_Creation_ShouldBeFast()
    {
        // Arrange
        const int iterations = 100_000;
        var natInfo = new NatInfo
        {
            Type = NatType.FullCone,
            PublicAddress = "1.2.3.4",
            PublicPort = 1234
        };
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            _ = new PeerConnectionState
            {
                PeerId = $"peer-{i}",
                Status = PeerConnectionStatus.Connected,
                LocalNatInfo = natInfo,
                ActiveTransport = TransportType.P2P
            };
        }
        sw.Stop();

        // Assert
        var perCreationNs = sw.Elapsed.TotalNanoseconds / iterations;
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "100k connection state creations should complete in under 100ms");

        OutputMetric("Connection_StateCreatePerOp_ns", perCreationNs);
        OutputMetric("Connection_StateCreate100k_ms", sw.ElapsedMilliseconds);
    }

    [Fact]
    public void IceCandidate_Creation_ShouldBeFast()
    {
        // Arrange
        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            _ = IceCandidate.CreateHost($"192.168.1.{i % 256}", 5000 + (i % 1000));
            _ = IceCandidate.CreateServerReflexive(
                $"203.0.113.{i % 256}", 10000 + (i % 1000),
                $"192.168.1.{i % 256}", 5000 + (i % 1000));
        }
        sw.Stop();

        // Assert
        var perCreationNs = sw.Elapsed.TotalNanoseconds / (iterations * 2);
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "200k candidate creations should complete in under 500ms");

        OutputMetric("ICE_CandidateCreatePerOp_ns", perCreationNs);
        OutputMetric("ICE_CandidateCreate200k_ms", sw.ElapsedMilliseconds);
    }

    [Fact]
    public void IceCandidate_ToEndPoint_ShouldBeFast()
    {
        // Arrange
        var candidates = Enumerable.Range(0, 1000)
            .Select(i => IceCandidate.CreateHost($"192.168.1.{i % 256}", 5000 + i))
            .ToList();

        const int iterations = 10_000;
        var sw = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            foreach (var candidate in candidates)
            {
                _ = candidate.ToEndPoint();
            }
        }
        sw.Stop();

        // Assert
        var totalOps = iterations * candidates.Count;
        var perOpNs = sw.Elapsed.TotalNanoseconds / totalOps;
        sw.ElapsedMilliseconds.Should().BeLessThan(2000,
            "10M ToEndPoint calls should complete in under 2s");

        OutputMetric("ICE_ToEndPointPerOp_ns", perOpNs);
        OutputMetric("ICE_ToEndPoint10M_ms", sw.ElapsedMilliseconds);
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public void IceCandidate_MemoryEfficiency()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetTotalMemory(true);

        // Act - Create 10,000 candidates
        var candidates = new List<IceCandidate>(10_000);
        for (var i = 0; i < 10_000; i++)
        {
            candidates.Add(IceCandidate.CreateHost($"192.168.1.{i % 256}", 5000 + (i % 1000)));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var after = GC.GetTotalMemory(true);

        // Assert
        var memoryPerCandidate = (after - before) / 10_000.0;
        candidates.Should().HaveCount(10_000);
        memoryPerCandidate.Should().BeLessThan(500,
            "Each candidate should use less than 500 bytes");

        OutputMetric("Memory_PerCandidate_bytes", memoryPerCandidate);
        OutputMetric("Memory_10kCandidates_KB", (after - before) / 1024.0);
    }

    [Fact]
    public void ConnectionState_MemoryEfficiency()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetTotalMemory(true);

        // Act - Create 1,000 connection states
        var states = new List<PeerConnectionState>(1_000);
        for (var i = 0; i < 1_000; i++)
        {
            states.Add(new PeerConnectionState
            {
                PeerId = $"peer-{i}",
                Status = PeerConnectionStatus.Connected,
                ActiveTransport = TransportType.P2P,
                LocalNatInfo = new NatInfo
                {
                    Type = NatType.FullCone,
                    PublicAddress = "1.2.3.4",
                    PublicPort = 1234
                }
            });
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var after = GC.GetTotalMemory(true);

        // Assert
        var memoryPerState = (after - before) / 1_000.0;
        states.Should().HaveCount(1_000);
        memoryPerState.Should().BeLessThan(1000,
            "Each connection state should use less than 1KB");

        OutputMetric("Memory_PerConnectionState_bytes", memoryPerState);
        OutputMetric("Memory_1kStates_KB", (after - before) / 1024.0);
    }

    #endregion

    #region Throughput Simulation

    [Fact]
    public void DataPacket_SerializationThroughput()
    {
        // Arrange
        const int packetSize = 1400; // MTU-safe size
        const int iterations = 100_000;
        var data = new byte[packetSize];
        RandomNumberGenerator.Fill(data);
        var sw = Stopwatch.StartNew();

        // Act - Simulate packet preparation
        for (var i = 0; i < iterations; i++)
        {
            // Simulate ReadOnlyMemory creation and slicing
            var memory = new ReadOnlyMemory<byte>(data);
            _ = memory.Slice(0, Math.Min(1000, memory.Length));
            _ = memory.ToArray();
        }
        sw.Stop();

        // Assert
        var packetsPerSecond = iterations / sw.Elapsed.TotalSeconds;
        var throughputMbps = (iterations * packetSize * 8) / sw.Elapsed.TotalSeconds / 1_000_000;
        packetsPerSecond.Should().BeGreaterThan(100_000,
            "Should process at least 100k packets per second");

        OutputMetric("Throughput_PacketsPerSec", packetsPerSecond);
        OutputMetric("Throughput_Mbps", throughputMbps);
    }

    #endregion

    #region Helper Methods

    private static void OutputMetric(string name, double value)
    {
        // Output in a parseable format for CI/CD integration
        Console.WriteLine($"[PERF] {name}: {value:F2}");
    }

    #endregion
}
