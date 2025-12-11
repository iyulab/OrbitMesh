using Microsoft.Extensions.Logging.Abstractions;
using OrbitMesh.Core.Models;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Tests;

public class StreamingServiceTests
{
    #region Interface Tests

    [Fact]
    public void IStreamingService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IStreamingService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void InMemoryStreamingService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(InMemoryStreamingService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IStreamingService));
    }

    #endregion

    #region Publish Tests

    [Fact]
    public async Task PublishAsync_Stores_StreamItem()
    {
        // Arrange
        using var service = CreateService();
        var item = StreamItem.FromText("job-1", 1, "Hello, World!");

        // Act
        await service.PublishAsync(item);

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().NotBeNull();
        state!.CurrentSequence.Should().Be(1);
        state.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_Updates_StreamState_Correctly()
    {
        // Arrange
        using var service = CreateService();

        // Act
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item 1"));
        await service.PublishAsync(StreamItem.FromText("job-1", 2, "Item 2"));
        await service.PublishAsync(StreamItem.FromText("job-1", 3, "Item 3"));

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().NotBeNull();
        state!.CurrentSequence.Should().Be(3);
        state.TotalItems.Should().Be(3);
        state.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_EndOfStream_Marks_Complete()
    {
        // Arrange
        using var service = CreateService();

        // Act
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item"));
        await service.PublishAsync(StreamItem.EndOfStream("job-1", 2));

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().NotBeNull();
        state!.IsComplete.Should().BeTrue();
        state.CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region Subscribe Tests

    [Fact]
    public async Task Subscribe_Receives_Published_Items()
    {
        // Arrange
        using var service = CreateService();
        var receivedItems = new List<StreamItem>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Use synchronous Subscribe which immediately registers the subscriber
        // CA1849: Using synchronous Subscribe intentionally for deterministic test behavior
#pragma warning disable CA1849
        var reader = service.Subscribe("job-1");
#pragma warning restore CA1849

        // Act
        var readerTask = Task.Run(async () =>
        {
            await foreach (var item in reader.ReadAllAsync(cts.Token))
            {
                receivedItems.Add(item);
                if (item.IsEndOfStream) break;
            }
        });

        // Small delay to ensure reader task is started
        await Task.Delay(50);

        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item 1"));
        await service.PublishAsync(StreamItem.FromText("job-1", 2, "Item 2"));
        await service.PublishAsync(StreamItem.EndOfStream("job-1", 3));

        await readerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        receivedItems.Should().HaveCount(3);
        receivedItems[0].SequenceNumber.Should().Be(1);
        receivedItems[1].SequenceNumber.Should().Be(2);
        receivedItems[2].IsEndOfStream.Should().BeTrue();
    }

    [Fact]
    public async Task Multiple_Subscribers_Receive_Same_Items()
    {
        // Arrange
        using var service = CreateService();
        var received1 = new List<StreamItem>();
        var received2 = new List<StreamItem>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Use synchronous Subscribe which immediately registers the subscribers
        // CA1849: Using synchronous Subscribe intentionally for deterministic test behavior
#pragma warning disable CA1849
        var reader1 = service.Subscribe("job-1");
        var reader2 = service.Subscribe("job-1");
#pragma warning restore CA1849

        // Act
        var task1 = Task.Run(async () =>
        {
            await foreach (var item in reader1.ReadAllAsync(cts.Token))
            {
                received1.Add(item);
                if (item.IsEndOfStream) break;
            }
        });
        var task2 = Task.Run(async () =>
        {
            await foreach (var item in reader2.ReadAllAsync(cts.Token))
            {
                received2.Add(item);
                if (item.IsEndOfStream) break;
            }
        });

        // Small delay to ensure reader tasks are started
        await Task.Delay(50);

        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item"));
        await service.PublishAsync(StreamItem.EndOfStream("job-1", 2));

        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        received1.Should().HaveCount(2);
        received2.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSubscriberCount_Returns_Correct_Count()
    {
        // Arrange
        using var service = CreateService();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        using var cts3 = new CancellationTokenSource();

        // Act - Start subscriptions (they will block until cancelled or completed)
        var task1 = Task.Run(() => service.SubscribeAsync("job-1", cts1.Token).GetAsyncEnumerator().MoveNextAsync().AsTask());
        var task2 = Task.Run(() => service.SubscribeAsync("job-1", cts2.Token).GetAsyncEnumerator().MoveNextAsync().AsTask());
        var task3 = Task.Run(() => service.SubscribeAsync("job-2", cts3.Token).GetAsyncEnumerator().MoveNextAsync().AsTask());

        // Give time for subscriptions to be created
        await Task.Delay(100);

        // Assert
        service.GetSubscriberCount("job-1").Should().Be(2);
        service.GetSubscriberCount("job-2").Should().Be(1);
        service.GetSubscriberCount("unknown").Should().Be(0);

        // Cleanup
        await cts1.CancelAsync();
        await cts2.CancelAsync();
        await cts3.CancelAsync();
    }

    #endregion

    #region Buffer Tests

    [Fact]
    public async Task GetBufferedItemsAsync_Returns_Items_From_Sequence()
    {
        // Arrange
        using var service = CreateService();
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item 1"));
        await service.PublishAsync(StreamItem.FromText("job-1", 2, "Item 2"));
        await service.PublishAsync(StreamItem.FromText("job-1", 3, "Item 3"));

        // Act
        var items = await service.GetBufferedItemsAsync("job-1", fromSequence: 2);

        // Assert
        items.Should().HaveCount(2);
        items[0].SequenceNumber.Should().Be(2);
        items[1].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task GetBufferedItemsAsync_Respects_MaxItems()
    {
        // Arrange
        using var service = CreateService();
        for (int i = 1; i <= 10; i++)
        {
            await service.PublishAsync(StreamItem.FromText("job-1", i, $"Item {i}"));
        }

        // Act
        var items = await service.GetBufferedItemsAsync("job-1", maxItems: 3);

        // Assert
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBufferedItemsAsync_Returns_Empty_For_Unknown_Job()
    {
        // Arrange
        using var service = CreateService();

        // Act
        var items = await service.GetBufferedItemsAsync("unknown-job");

        // Assert
        items.Should().BeEmpty();
    }

    #endregion

    #region Stream Lifecycle Tests

    [Fact]
    public async Task CompleteStreamAsync_Marks_Stream_Complete()
    {
        // Arrange
        using var service = CreateService();
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item"));

        // Act
        await service.CompleteStreamAsync("job-1");

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().NotBeNull();
        state!.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task AbortStreamAsync_Marks_Stream_Complete()
    {
        // Arrange
        using var service = CreateService();
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item"));

        // Act
        await service.AbortStreamAsync("job-1", "Test abort");

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().NotBeNull();
        state!.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupStreamAsync_Removes_Stream_Data()
    {
        // Arrange
        using var service = CreateService();
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Item"));

        // Act
        await service.CleanupStreamAsync("job-1");

        // Assert
        var state = await service.GetStreamStateAsync("job-1");
        state.Should().BeNull();
    }

    #endregion

    #region Active Streams Tests

    [Fact]
    public async Task GetActiveStreamsAsync_Returns_Active_Streams_Only()
    {
        // Arrange
        using var service = CreateService();
        await service.PublishAsync(StreamItem.FromText("job-1", 1, "Active"));
        await service.PublishAsync(StreamItem.FromText("job-2", 1, "Active"));
        await service.PublishAsync(StreamItem.EndOfStream("job-3", 1)); // Completed

        // Act
        var activeStreams = await service.GetActiveStreamsAsync();

        // Assert
        activeStreams.Should().HaveCount(2);
        activeStreams.Should().Contain("job-1");
        activeStreams.Should().Contain("job-2");
        activeStreams.Should().NotContain("job-3");
    }

    #endregion

    #region StreamItem Model Tests

    [Fact]
    public void StreamItem_FromText_Creates_Correct_Item()
    {
        // Act
        var item = StreamItem.FromText("job-1", 5, "Hello, World!");

        // Assert
        item.JobId.Should().Be("job-1");
        item.SequenceNumber.Should().Be(5);
        item.ContentType.Should().Be("text/plain");
        item.TextData.Should().Be("Hello, World!");
        item.IsEndOfStream.Should().BeFalse();
    }

    [Fact]
    public void StreamItem_EndOfStream_Creates_End_Marker()
    {
        // Act
        var item = StreamItem.EndOfStream("job-1", 10);

        // Assert
        item.JobId.Should().Be("job-1");
        item.SequenceNumber.Should().Be(10);
        item.IsEndOfStream.Should().BeTrue();
        item.Data.Should().BeEmpty();
    }

    [Fact]
    public void StreamItem_FromJson_Serializes_Correctly()
    {
        // Arrange
        var data = new { Name = "Test", Value = 42 };

        // Act
        var item = StreamItem.FromJson("job-1", 1, data);

        // Assert
        item.ContentType.Should().Be("application/x-msgpack");
        item.Data.Should().NotBeEmpty();
    }

    #endregion

    #region Helper Methods

    private static InMemoryStreamingService CreateService()
    {
        return new InMemoryStreamingService(
            NullLogger<InMemoryStreamingService>.Instance,
            maxBufferSize: 100,
            subscriberChannelCapacity: 50);
    }

    private static async Task CollectItemsAsync(
        System.Threading.Channels.ChannelReader<StreamItem> reader,
        List<StreamItem> items)
    {
        await foreach (var item in reader.ReadAllAsync())
        {
            items.Add(item);
            if (item.IsEndOfStream) break;
        }
    }

    #endregion
}
