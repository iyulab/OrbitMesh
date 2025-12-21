using FluentAssertions;

namespace OrbitMesh.Node.Tests.Handlers;

/// <summary>
/// Tests for file sync safety features including DeleteOrphans protection.
/// These tests simulate the safety window behavior without requiring the actual handler.
/// </summary>
public sealed class FileSyncSafetyTests : IDisposable
{
    private readonly string _testRoot;

    public FileSyncSafetyTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"orbitmesh_safety_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    #region DeleteOrphans Safety Window Tests

    [Fact]
    public void SafetyWindow_RecentlyModifiedFile_ShouldBeProtected()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "recent.txt");
        File.WriteAllText(filePath, "Recently modified content");

        var safetyThreshold = DateTime.UtcNow.AddSeconds(-5);
        var fileInfo = new FileInfo(filePath);

        // Act
        var isProtected = fileInfo.LastWriteTimeUtc > safetyThreshold;

        // Assert
        isProtected.Should().BeTrue(
            "files modified within 5 seconds should be protected from deletion");
    }

    [Fact]
    public async Task SafetyWindow_OldFile_ShouldNotBeProtected()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "old.txt");
        await File.WriteAllTextAsync(filePath, "Old content");

        // Wait for safety window to pass
        await Task.Delay(TimeSpan.FromSeconds(6));

        var safetyThreshold = DateTime.UtcNow.AddSeconds(-5);
        var fileInfo = new FileInfo(filePath);
        fileInfo.Refresh(); // Ensure we have current info

        // Act
        var isProtected = fileInfo.LastWriteTimeUtc > safetyThreshold;

        // Assert
        isProtected.Should().BeFalse(
            "files not modified within 5 seconds should be eligible for deletion");
    }

    [Fact]
    public void SafetyWindow_SimulateConcurrentWrite_ProtectsActiveFile()
    {
        // Simulate: Agent A is writing, Agent B tries to delete as orphan

        // Arrange
        var filePath = Path.Combine(_testRoot, "concurrent_write.txt");

        // Agent A starts writing (simulated by creating file)
        using (var stream = File.Create(filePath))
        {
            stream.WriteByte(0x00); // Partial write
        }

        var safetyThreshold = DateTime.UtcNow.AddSeconds(-5);
        var fileInfo = new FileInfo(filePath);

        // Agent B checks if it's safe to delete
        var isSafeToDelete = fileInfo.LastWriteTimeUtc <= safetyThreshold;

        // Assert
        isSafeToDelete.Should().BeFalse(
            "file being written should not be safe to delete");
    }

    #endregion

    #region Multi-Agent Orphan Detection Simulation

    [Fact]
    public async Task MultiAgent_OrphanDetection_RaceConditionSafety()
    {
        // Simulate: Agent A creates file, Agent B's manifest doesn't include it yet

        // Arrange - Initial state: only file1.txt in both manifests
        var file1Path = Path.Combine(_testRoot, "file1.txt");
        await File.WriteAllTextAsync(file1Path, "Original file");

        var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "file1.txt" };

        // Simulate Agent A creating a new file while Agent B is syncing
        var newFilePath = Path.Combine(_testRoot, "newfile.txt");
        await File.WriteAllTextAsync(newFilePath, "New file from Agent A");

        // Agent B's manifest doesn't have newfile.txt yet
        // Agent B checks for orphans

        var safetyThreshold = DateTime.UtcNow.AddSeconds(-5);
        var orphanCandidates = new List<string>();

        foreach (var file in Directory.EnumerateFiles(_testRoot))
        {
            var relativePath = Path.GetFileName(file);
            if (!sourceFiles.Contains(relativePath))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc <= safetyThreshold)
                {
                    orphanCandidates.Add(relativePath);
                }
            }
        }

        // Assert - newfile.txt should NOT be in orphan candidates (safety window)
        orphanCandidates.Should().NotContain("newfile.txt",
            "recently created file should be protected by safety window");

        // Wait for safety window to pass
        await Task.Delay(TimeSpan.FromSeconds(6));

        // Re-check
        orphanCandidates.Clear();
        safetyThreshold = DateTime.UtcNow.AddSeconds(-5);

        foreach (var file in Directory.EnumerateFiles(_testRoot))
        {
            var relativePath = Path.GetFileName(file);
            if (!sourceFiles.Contains(relativePath))
            {
                var fileInfo = new FileInfo(file);
                fileInfo.Refresh();
                if (fileInfo.LastWriteTimeUtc <= safetyThreshold)
                {
                    orphanCandidates.Add(relativePath);
                }
            }
        }

        // After safety window, it would be detected as orphan
        // (In real scenario, manifest would have updated by now)
        orphanCandidates.Should().Contain("newfile.txt",
            "after safety window, file would be detected as orphan if not in manifest");
    }

    [Fact]
    public void MultiAgent_ConcurrentSync_NoDeleteConflict()
    {
        // Simulate: Multiple agents syncing same folder with DeleteOrphans

        // Arrange
        var sharedFiles = new[] { "app.dll", "config.json" };
        foreach (var file in sharedFiles)
        {
            File.WriteAllText(Path.Combine(_testRoot, file), $"Content of {file}");
        }

        // Both agents have same manifest
        var manifest = new HashSet<string>(sharedFiles, StringComparer.OrdinalIgnoreCase);

        // Agent A starts sync first
        var agent1DeleteList = new List<string>();
        var agent2DeleteList = new List<string>();

        // Simulate parallel orphan detection
        Parallel.Invoke(
            () =>
            {
                // Agent 1
                foreach (var file in Directory.EnumerateFiles(_testRoot))
                {
                    var name = Path.GetFileName(file);
                    if (!manifest.Contains(name))
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc <= DateTime.UtcNow.AddSeconds(-5))
                        {
                            lock (agent1DeleteList)
                                agent1DeleteList.Add(name);
                        }
                    }
                }
            },
            () =>
            {
                // Agent 2
                foreach (var file in Directory.EnumerateFiles(_testRoot))
                {
                    var name = Path.GetFileName(file);
                    if (!manifest.Contains(name))
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc <= DateTime.UtcNow.AddSeconds(-5))
                        {
                            lock (agent2DeleteList)
                                agent2DeleteList.Add(name);
                        }
                    }
                }
            }
        );

        // Assert - No files should be marked for deletion (all in manifest)
        agent1DeleteList.Should().BeEmpty();
        agent2DeleteList.Should().BeEmpty();
    }

    #endregion

    #region Time-Agnostic Sync Decision Tests

    [Fact]
    public void SyncDecision_BasedOnChecksum_NotTimestamp()
    {
        // Simulate: File has "future" timestamp but different content

        // Arrange
        var filePath = Path.Combine(_testRoot, "timewarp.txt");
        File.WriteAllText(filePath, "Old content");

        // Set file time to future (simulating clock skew)
        var futureTime = DateTime.UtcNow.AddHours(1);
        File.SetLastWriteTimeUtc(filePath, futureTime);

        var localChecksum = ComputeFileChecksum(filePath);
        var serverChecksum = ComputeChecksum("New content"); // Different content

        // Act - Sync decision based on checksum
        var needsSync = !string.Equals(localChecksum, serverChecksum, StringComparison.OrdinalIgnoreCase);

        // Assert
        needsSync.Should().BeTrue("sync should be based on checksum, not timestamp");
    }

    [Fact]
    public void SyncDecision_SameContent_NoSync()
    {
        // Simulate: Different timestamps, same content

        // Arrange
        var content = "Identical content";
        var filePath = Path.Combine(_testRoot, "same_content.txt");
        File.WriteAllText(filePath, content);

        // Set to old timestamp
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddDays(-30));

        var localChecksum = ComputeFileChecksum(filePath);
        var serverChecksum = ComputeChecksum(content); // Same content

        // Act
        var needsSync = !string.Equals(localChecksum, serverChecksum, StringComparison.OrdinalIgnoreCase);

        // Assert
        needsSync.Should().BeFalse("same content should not trigger sync regardless of timestamp");
    }

    [Fact]
    public void SyncDecision_ClockSkew_HandledCorrectly()
    {
        // Simulate: Server clock is 2 hours ahead of agent

        // Arrange
        var agentTime = DateTime.UtcNow;
        var serverTime = agentTime.AddHours(2); // Server is ahead

        var filePath = Path.Combine(_testRoot, "clock_skew.txt");
        File.WriteAllText(filePath, "Content v1");

        // Server says file was modified at "future" time
        var serverFileModifiedAt = serverTime;

        // Agent's file was modified at "current" time
        var agentFileModifiedAt = File.GetLastWriteTimeUtc(filePath);

        // Traditional time-based sync would be confused
        var wouldTimeBasedSyncWork = serverFileModifiedAt > agentFileModifiedAt;

        // Checksum-based sync works correctly
        var localChecksum = ComputeFileChecksum(filePath);
        var serverChecksum = ComputeChecksum("Content v1"); // Same content
        var checksumBasedNeedsSync = !string.Equals(localChecksum, serverChecksum, StringComparison.OrdinalIgnoreCase);

        // Assert
        wouldTimeBasedSyncWork.Should().BeTrue("time-based would incorrectly think sync needed");
        checksumBasedNeedsSync.Should().BeFalse("checksum-based correctly identifies no sync needed");
    }

    #endregion

    #region Helper Methods

    private static string ComputeFileChecksum(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    #endregion
}
