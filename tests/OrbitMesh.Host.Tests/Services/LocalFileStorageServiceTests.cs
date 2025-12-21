using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests.Services;

/// <summary>
/// Tests for LocalFileStorageService manifest generation and locking.
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Test code only")]
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly LocalFileStorageService _service;
    private readonly Mock<ILogger<LocalFileStorageService>> _loggerMock;

    public LocalFileStorageServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"orbitmesh_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _loggerMock = new Mock<ILogger<LocalFileStorageService>>();
        _service = new LocalFileStorageService(_testRoot, _loggerMock.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    #region Manifest Generation Tests

    [Fact]
    public async Task GetManifestAsync_EmptyDirectory_ReturnsEmptyManifest()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "empty");
        Directory.CreateDirectory(subDir);

        // Act
        var manifest = await _service.GetManifestAsync("empty");

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Files.Should().BeEmpty();
        manifest.FileCount.Should().Be(0);
        manifest.TotalSize.Should().Be(0);
        manifest.ContentHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetManifestAsync_WithFiles_ReturnsCorrectManifest()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "withfiles");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file1.txt"), "Hello World");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.txt"), "Test Content");

        // Act
        var manifest = await _service.GetManifestAsync("withfiles");

        // Assert
        manifest.Should().NotBeNull();
        manifest!.Files.Should().HaveCount(2);
        manifest.FileCount.Should().Be(2);
        manifest.TotalSize.Should().BeGreaterThan(0);
        manifest.ContentHash.Should().NotBeNullOrEmpty();
        manifest.Files.Should().Contain(f => f.Path == "file1.txt");
        manifest.Files.Should().Contain(f => f.Path == "file2.txt");
    }

    [Fact]
    public async Task GetManifestAsync_SequenceNumber_IncreasesOnContentChange()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "sequence");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "Version 1");

        // Act - First call
        var manifest1 = await _service.GetManifestAsync("sequence");

        // Modify file
        await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "Version 2");

        // Act - Second call
        var manifest2 = await _service.GetManifestAsync("sequence");

        // Assert
        manifest1.Should().NotBeNull();
        manifest2.Should().NotBeNull();
        manifest2!.SequenceNumber.Should().BeGreaterThan(manifest1!.SequenceNumber,
            "sequence number should increase when content changes");
        manifest2.ContentHash.Should().NotBe(manifest1.ContentHash,
            "content hash should differ when file content changes");
    }

    [Fact]
    public async Task GetManifestAsync_SequenceNumber_StaysConstantWhenUnchanged()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "unchanged");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "Static Content");

        // Act - Multiple calls without changes
        var manifest1 = await _service.GetManifestAsync("unchanged");
        var manifest2 = await _service.GetManifestAsync("unchanged");
        var manifest3 = await _service.GetManifestAsync("unchanged");

        // Assert
        manifest1.Should().NotBeNull();
        manifest2.Should().NotBeNull();
        manifest3.Should().NotBeNull();
        manifest1!.SequenceNumber.Should().Be(manifest2!.SequenceNumber);
        manifest2.SequenceNumber.Should().Be(manifest3!.SequenceNumber);
        manifest1.ContentHash.Should().Be(manifest2.ContentHash);
    }

    [Fact]
    public async Task GetManifestAsync_ContentHash_IsDeterministic()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "deterministic");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "a.txt"), "Content A");
        await File.WriteAllTextAsync(Path.Combine(subDir, "b.txt"), "Content B");

        // Act - Multiple calls
        var manifest1 = await _service.GetManifestAsync("deterministic");
        var manifest2 = await _service.GetManifestAsync("deterministic");

        // Assert
        manifest1.Should().NotBeNull();
        manifest2.Should().NotBeNull();
        manifest1!.ContentHash.Should().Be(manifest2!.ContentHash,
            "content hash should be deterministic for same file set");
    }

    #endregion

    #region Concurrent Access Tests (Locking)

    [Fact]
    public async Task GetManifestAsync_ConcurrentCalls_ProducesConsistentResults()
    {
        // Arrange - Create a directory with files
        var subDir = Path.Combine(_testRoot, "concurrent");
        Directory.CreateDirectory(subDir);
        for (int i = 0; i < 10; i++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(subDir, $"file{i}.txt"),
                $"Content {i}");
        }

        // Act - Make 20 concurrent manifest requests
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _service.GetManifestAsync("concurrent"))
            .ToArray();

        var manifests = await Task.WhenAll(tasks);

        // Assert - All manifests should be identical
        var firstHash = manifests[0]!.ContentHash;
        manifests.Should().AllSatisfy(m =>
        {
            m.Should().NotBeNull();
            m!.ContentHash.Should().Be(firstHash,
                "all concurrent manifest generations should produce identical results");
        });
    }

    [Fact]
    public async Task GetManifestAsync_ConcurrentWithFileChanges_MaintainsIntegrity()
    {
        // Arrange - Use multiple files to avoid single-file contention
        var subDir = Path.Combine(_testRoot, "concurrentchanges");
        Directory.CreateDirectory(subDir);

        // Create initial files
        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(subDir, $"file{i}.txt"), $"Initial {i}");
        }

        var manifestTasks = new List<Task<Core.Models.SyncManifest?>>();
        var changeTasks = new List<Task>();

        // Act - Start concurrent manifest reads and file writes to DIFFERENT files
        for (int i = 0; i < 5; i++)
        {
            var iteration = i;
            manifestTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(iteration * 20); // Stagger slightly
                return await _service.GetManifestAsync("concurrentchanges");
            }));

            // Write to different files to avoid contention
            changeTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(iteration * 25);
                try
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(subDir, $"file{iteration}.txt"),
                        $"Content v{iteration}");
                }
                catch (IOException)
                {
                    // File contention may occur on Windows, ignore for test purposes
                }
            }));
        }

        await Task.WhenAll(manifestTasks);
        await Task.WhenAll(changeTasks);

        // Assert - Final manifest should be valid and consistent
        var finalManifest = await _service.GetManifestAsync("concurrentchanges");
        finalManifest.Should().NotBeNull();
        finalManifest!.Files.Should().HaveCount(5);
        finalManifest.ContentHash.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region File Save and Checksum Tests

    [Fact]
    public async Task SaveFileAsync_WithExpectedChecksum_VerifiesIntegrity()
    {
        // Arrange
        var content = "Test content for checksum verification"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Compute expected checksum
        var expectedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content));

        // Act
        var result = await _service.SaveFileAsync(
            "checksum_test.txt",
            stream,
            overwrite: true,
            expectedChecksum: expectedChecksum);

        // Assert
        result.Success.Should().BeTrue();
        result.Checksum.Should().BeEquivalentTo(expectedChecksum);
    }

    [Fact]
    public async Task SaveFileAsync_WithWrongChecksum_Fails()
    {
        // Arrange
        var content = "Test content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var wrongChecksum = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = await _service.SaveFileAsync(
            "wrong_checksum.txt",
            stream,
            overwrite: true,
            expectedChecksum: wrongChecksum);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Checksum mismatch");
    }

    #endregion

    #region Multi-Agent Simulation Tests

    [Fact]
    public async Task MultiAgent_SameSourceFolder_ConsistentManifests()
    {
        // Simulate: Multiple agents requesting manifest from same source folder

        // Arrange
        var deployDir = Path.Combine(_testRoot, "deploy_source");
        Directory.CreateDirectory(deployDir);
        await File.WriteAllTextAsync(Path.Combine(deployDir, "app.dll"), "App binary content");
        await File.WriteAllTextAsync(Path.Combine(deployDir, "config.json"), "{\"setting\":1}");
        Directory.CreateDirectory(Path.Combine(deployDir, "data"));
        await File.WriteAllTextAsync(Path.Combine(deployDir, "data", "cache.db"), "cache data");

        // Act - Simulate 5 agents requesting manifest simultaneously
        var agentTasks = Enumerable.Range(1, 5)
            .Select(agentId => Task.Run(async () =>
            {
                // Add random delay to simulate network latency differences
                await Task.Delay(Random.Shared.Next(0, 50));
                return await _service.GetManifestAsync("deploy_source");
            }))
            .ToArray();

        var manifests = await Task.WhenAll(agentTasks);

        // Assert
        var referenceManifest = manifests[0];
        referenceManifest.Should().NotBeNull();
        referenceManifest!.Files.Should().HaveCount(3);

        foreach (var manifest in manifests.Skip(1))
        {
            manifest.Should().NotBeNull();
            manifest!.ContentHash.Should().Be(referenceManifest.ContentHash,
                "all agents should receive identical manifest");
            manifest.SequenceNumber.Should().Be(referenceManifest.SequenceNumber);
            manifest.FileCount.Should().Be(referenceManifest.FileCount);
        }
    }

    [Fact]
    public async Task MultiAgent_SequentialUpdates_CorrectSequencing()
    {
        // Simulate: Server updates files, multiple agents sync at different times

        // Arrange
        var deployDir = Path.Combine(_testRoot, "sequential_updates");
        Directory.CreateDirectory(deployDir);

        // Version 1
        await File.WriteAllTextAsync(Path.Combine(deployDir, "version.txt"), "1.0.0");
        var manifestV1 = await _service.GetManifestAsync("sequential_updates");

        // Version 2
        await File.WriteAllTextAsync(Path.Combine(deployDir, "version.txt"), "2.0.0");
        var manifestV2 = await _service.GetManifestAsync("sequential_updates");

        // Version 3
        await File.WriteAllTextAsync(Path.Combine(deployDir, "version.txt"), "3.0.0");
        var manifestV3 = await _service.GetManifestAsync("sequential_updates");

        // Assert
        manifestV1.Should().NotBeNull();
        manifestV2.Should().NotBeNull();
        manifestV3.Should().NotBeNull();

        manifestV2!.SequenceNumber.Should().BeGreaterThan(manifestV1!.SequenceNumber);
        manifestV3!.SequenceNumber.Should().BeGreaterThan(manifestV2.SequenceNumber);

        // All content hashes should be different
        var hashes = new[] { manifestV1.ContentHash, manifestV2.ContentHash, manifestV3.ContentHash };
        hashes.Distinct().Should().HaveCount(3, "each version should have unique content hash");
    }

    #endregion
}
