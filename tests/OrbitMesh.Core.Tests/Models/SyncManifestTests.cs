using FluentAssertions;
using OrbitMesh.Core.Models;

namespace OrbitMesh.Core.Tests.Models;

/// <summary>
/// Tests for SyncManifest time-agnostic sync functionality.
/// </summary>
public class SyncManifestTests
{
    #region ContentHash Tests - Time Agnostic Versioning

    [Fact]
    public void ComputeContentHash_SameFiles_ReturnsSameHash()
    {
        // Arrange
        var files1 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 },
            new() { Path = "file2.txt", Checksum = "DEF456", Size = 200 }
        };

        var files2 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 },
            new() { Path = "file2.txt", Checksum = "DEF456", Size = 200 }
        };

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().Be(hash2, "identical file sets should produce identical content hashes");
    }

    [Fact]
    public void ComputeContentHash_DifferentOrder_ReturnsSameHash()
    {
        // Arrange - files in different order
        var files1 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 },
            new() { Path = "file2.txt", Checksum = "DEF456", Size = 200 }
        };

        var files2 = new List<SyncFileEntry>
        {
            new() { Path = "file2.txt", Checksum = "DEF456", Size = 200 },
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 }
        };

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().Be(hash2, "order should not affect content hash (deterministic sorting applied)");
    }

    [Fact]
    public void ComputeContentHash_DifferentChecksum_ReturnsDifferentHash()
    {
        // Arrange
        var files1 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 }
        };

        var files2 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "XYZ789", Size = 100 } // Different checksum
        };

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().NotBe(hash2, "different file content should produce different hash");
    }

    [Fact]
    public void ComputeContentHash_DifferentSize_ReturnsDifferentHash()
    {
        // Arrange
        var files1 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 }
        };

        var files2 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 200 } // Different size
        };

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().NotBe(hash2, "different file size should produce different hash");
    }

    [Fact]
    public void ComputeContentHash_AdditionalFile_ReturnsDifferentHash()
    {
        // Arrange
        var files1 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 }
        };

        var files2 = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "ABC123", Size = 100 },
            new() { Path = "file2.txt", Checksum = "DEF456", Size = 200 }
        };

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().NotBe(hash2, "adding a file should change the content hash");
    }

    [Fact]
    public void ComputeContentHash_EmptyList_ReturnsConsistentHash()
    {
        // Arrange
        var files1 = new List<SyncFileEntry>();
        var files2 = new List<SyncFileEntry>();

        // Act
        var hash1 = SyncManifest.ComputeContentHash(files1);
        var hash2 = SyncManifest.ComputeContentHash(files2);

        // Assert
        hash1.Should().Be(hash2, "empty file lists should produce identical hashes");
        hash1.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region DiffersFrom Tests - Time Agnostic Comparison

    [Fact]
    public void DiffersFrom_NullManifest_ReturnsTrue()
    {
        // Arrange
        var manifest = new SyncManifest
        {
            ContentHash = "ABC123",
            Files = new List<SyncFileEntry>()
        };

        // Act
        var differs = manifest.DiffersFrom(null);

        // Assert
        differs.Should().BeTrue("null manifest means no prior state, so it differs");
    }

    [Fact]
    public void DiffersFrom_SameContentHash_ReturnsFalse()
    {
        // Arrange
        var manifest1 = new SyncManifest
        {
            ContentHash = "ABC123",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var manifest2 = new SyncManifest
        {
            ContentHash = "ABC123",
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-1) // Different time!
        };

        // Act
        var differs = manifest1.DiffersFrom(manifest2);

        // Assert
        differs.Should().BeFalse("same content hash means same content, regardless of timestamp");
    }

    [Fact]
    public void DiffersFrom_DifferentContentHash_ReturnsTrue()
    {
        // Arrange
        var manifest1 = new SyncManifest
        {
            ContentHash = "ABC123",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var manifest2 = new SyncManifest
        {
            ContentHash = "XYZ789",
            GeneratedAt = DateTimeOffset.UtcNow // Same time!
        };

        // Act
        var differs = manifest1.DiffersFrom(manifest2);

        // Assert
        differs.Should().BeTrue("different content hash means different content");
    }

    [Fact]
    public void DiffersFrom_CaseInsensitive()
    {
        // Arrange
        var manifest1 = new SyncManifest { ContentHash = "abc123" };
        var manifest2 = new SyncManifest { ContentHash = "ABC123" };

        // Act
        var differs = manifest1.DiffersFrom(manifest2);

        // Assert
        differs.Should().BeFalse("content hash comparison should be case-insensitive");
    }

    #endregion

    #region GetFilesToSync Tests

    [Fact]
    public void GetFilesToSync_NullTarget_ReturnsAllFiles()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 },
                new() { Path = "file2.txt", Checksum = "B", Size = 200 }
            }
        };

        // Act
        var filesToSync = source.GetFilesToSync(null).ToList();

        // Assert
        filesToSync.Should().HaveCount(2, "all files should be synced when target is null");
    }

    [Fact]
    public void GetFilesToSync_IdenticalManifests_ReturnsEmpty()
    {
        // Arrange
        var files = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "A", Size = 100 },
            new() { Path = "file2.txt", Checksum = "B", Size = 200 }
        };

        var source = new SyncManifest { ContentHash = "ABC", Files = files };
        var target = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 },
                new() { Path = "file2.txt", Checksum = "B", Size = 200 }
            }
        };

        // Act
        var filesToSync = source.GetFilesToSync(target).ToList();

        // Assert
        filesToSync.Should().BeEmpty("identical manifests need no sync");
    }

    [Fact]
    public void GetFilesToSync_ModifiedFile_ReturnsModifiedOnly()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A_NEW", Size = 100 }, // Modified
                new() { Path = "file2.txt", Checksum = "B", Size = 200 }
            }
        };

        var target = new SyncManifest
        {
            ContentHash = "XYZ",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A_OLD", Size = 100 }, // Old version
                new() { Path = "file2.txt", Checksum = "B", Size = 200 }
            }
        };

        // Act
        var filesToSync = source.GetFilesToSync(target).ToList();

        // Assert
        filesToSync.Should().ContainSingle()
            .Which.Path.Should().Be("file1.txt");
    }

    [Fact]
    public void GetFilesToSync_NewFile_ReturnsNewFileOnly()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 },
                new() { Path = "newfile.txt", Checksum = "NEW", Size = 300 } // New file
            }
        };

        var target = new SyncManifest
        {
            ContentHash = "XYZ",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 }
            }
        };

        // Act
        var filesToSync = source.GetFilesToSync(target).ToList();

        // Assert
        filesToSync.Should().ContainSingle()
            .Which.Path.Should().Be("newfile.txt");
    }

    #endregion

    #region GetOrphanFiles Tests

    [Fact]
    public void GetOrphanFiles_NullTarget_ReturnsEmpty()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 }
            }
        };

        // Act
        var orphans = source.GetOrphanFiles(null).ToList();

        // Assert
        orphans.Should().BeEmpty("no target means no orphans");
    }

    [Fact]
    public void GetOrphanFiles_IdenticalManifests_ReturnsEmpty()
    {
        // Arrange
        var files = new List<SyncFileEntry>
        {
            new() { Path = "file1.txt", Checksum = "A", Size = 100 }
        };

        var source = new SyncManifest { ContentHash = "ABC", Files = files };
        var target = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 }
            }
        };

        // Act
        var orphans = source.GetOrphanFiles(target).ToList();

        // Assert
        orphans.Should().BeEmpty("no orphans when manifests are identical");
    }

    [Fact]
    public void GetOrphanFiles_DeletedFile_ReturnsOrphan()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 }
            }
        };

        var target = new SyncManifest
        {
            ContentHash = "XYZ",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 },
                new() { Path = "orphan.txt", Checksum = "ORPHAN", Size = 50 } // Not in source
            }
        };

        // Act
        var orphans = source.GetOrphanFiles(target).ToList();

        // Assert
        orphans.Should().ContainSingle()
            .Which.Path.Should().Be("orphan.txt");
    }

    [Fact]
    public void GetOrphanFiles_CaseInsensitivePaths()
    {
        // Arrange
        var source = new SyncManifest
        {
            ContentHash = "ABC",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "FILE1.TXT", Checksum = "A", Size = 100 }
            }
        };

        var target = new SyncManifest
        {
            ContentHash = "XYZ",
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file1.txt", Checksum = "A", Size = 100 } // Different case
            }
        };

        // Act
        var orphans = source.GetOrphanFiles(target).ToList();

        // Assert
        orphans.Should().BeEmpty("path comparison should be case-insensitive");
    }

    #endregion

    #region Time-Agnostic Simulation Tests

    [Fact]
    public void TimeAgnostic_DifferentDeviceTimes_SyncCorrectly()
    {
        // Simulate: Server time is 1 hour ahead of agent time
        // This should NOT affect sync decisions

        // Arrange
        var serverTime = DateTimeOffset.UtcNow;
        var agentTime = serverTime.AddHours(-1); // Agent is 1 hour behind

        var serverManifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "CONTENT_HASH", Size = 100 }
            }),
            GeneratedAt = serverTime, // Server's "current" time
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "CONTENT_HASH", Size = 100 }
            }
        };

        var agentManifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "CONTENT_HASH", Size = 100 }
            }),
            GeneratedAt = agentTime, // Agent's "current" time (1 hour behind)
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "CONTENT_HASH", Size = 100 }
            }
        };

        // Act
        var differs = serverManifest.DiffersFrom(agentManifest);
        var filesToSync = serverManifest.GetFilesToSync(agentManifest);

        // Assert
        differs.Should().BeFalse("same content should not differ despite time difference");
        filesToSync.Should().BeEmpty("no files need sync when content is identical");
    }

    [Fact]
    public void TimeAgnostic_ContentChange_DetectedRegardlessOfTime()
    {
        // Simulate: File content changed, but agent has "future" timestamp
        // Sync should still detect the change based on checksum

        // Arrange
        var oldManifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "OLD_CONTENT", Size = 100 }
            }),
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(1), // Future time!
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "OLD_CONTENT", Size = 100 }
            }
        };

        var newManifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "NEW_CONTENT", Size = 150 }
            }),
            GeneratedAt = DateTimeOffset.UtcNow, // "Past" compared to old manifest
            Files = new List<SyncFileEntry>
            {
                new() { Path = "file.txt", Checksum = "NEW_CONTENT", Size = 150 }
            }
        };

        // Act
        var differs = newManifest.DiffersFrom(oldManifest);
        var filesToSync = newManifest.GetFilesToSync(oldManifest).ToList();

        // Assert
        differs.Should().BeTrue("content changed, should detect difference");
        filesToSync.Should().ContainSingle()
            .Which.Path.Should().Be("file.txt");
    }

    #endregion

    #region Multi-Agent Concurrent Sync Simulation

    [Fact]
    public void MultiAgent_SameSourceDifferentAgents_ConsistentSync()
    {
        // Simulate: Multiple agents sync from same source
        // All agents should get identical sync decisions

        // Arrange - Source manifest (server)
        var sourceFiles = new List<SyncFileEntry>
        {
            new() { Path = "app.dll", Checksum = "DLL_V2", Size = 50000 },
            new() { Path = "config.json", Checksum = "CONFIG_V1", Size = 500 },
            new() { Path = "data/cache.db", Checksum = "CACHE_V3", Size = 10000 }
        };

        var sourceManifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(sourceFiles),
            SequenceNumber = 5,
            Files = sourceFiles
        };

        // Agent 1 - has older version
        var agent1Files = new List<SyncFileEntry>
        {
            new() { Path = "app.dll", Checksum = "DLL_V1", Size = 48000 }, // Old version
            new() { Path = "config.json", Checksum = "CONFIG_V1", Size = 500 }
            // Missing data/cache.db
        };
        var agent1Manifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(agent1Files),
            SequenceNumber = 3,
            Files = agent1Files
        };

        // Agent 2 - same state as Agent 1
        var agent2Manifest = new SyncManifest
        {
            ContentHash = SyncManifest.ComputeContentHash(agent1Files),
            SequenceNumber = 3,
            Files = new List<SyncFileEntry>
            {
                new() { Path = "app.dll", Checksum = "DLL_V1", Size = 48000 },
                new() { Path = "config.json", Checksum = "CONFIG_V1", Size = 500 }
            }
        };

        // Act
        var syncForAgent1 = sourceManifest.GetFilesToSync(agent1Manifest).ToList();
        var syncForAgent2 = sourceManifest.GetFilesToSync(agent2Manifest).ToList();

        // Assert
        syncForAgent1.Should().HaveCount(2, "Agent 1 needs app.dll (updated) and data/cache.db (new)");
        syncForAgent2.Should().HaveCount(2, "Agent 2 needs same files as Agent 1");

        syncForAgent1.Select(f => f.Path).Should().BeEquivalentTo(
            syncForAgent2.Select(f => f.Path),
            "both agents should sync the same files");
    }

    [Fact]
    public void MultiAgent_RaceCondition_SequenceNumberDetectsStale()
    {
        // Simulate: Agent gets manifest, source changes, agent tries to sync with stale manifest

        // Arrange - Original state
        var originalManifest = new SyncManifest
        {
            ContentHash = "HASH_V1",
            SequenceNumber = 1
        };

        // Source updated while agent was processing
        var updatedManifest = new SyncManifest
        {
            ContentHash = "HASH_V2",
            SequenceNumber = 2
        };

        // Act - Check if agent can detect it has stale data
        var agentHasStaleData = updatedManifest.SequenceNumber > originalManifest.SequenceNumber;
        var contentChanged = updatedManifest.DiffersFrom(originalManifest);

        // Assert
        agentHasStaleData.Should().BeTrue("sequence number increased indicates new version");
        contentChanged.Should().BeTrue("content hash differs");
    }

    #endregion
}
