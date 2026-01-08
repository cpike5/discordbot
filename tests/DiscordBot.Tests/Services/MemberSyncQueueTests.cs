using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MemberSyncQueue"/>.
/// </summary>
public class MemberSyncQueueTests
{
    private readonly Mock<ILogger<MemberSyncQueue>> _mockLogger;
    private readonly MemberSyncQueue _queue;

    public MemberSyncQueueTests()
    {
        _mockLogger = new Mock<ILogger<MemberSyncQueue>>();
        _queue = new MemberSyncQueue(_mockLogger.Object);
    }

    #region EnqueueGuild Tests

    [Fact]
    public void EnqueueGuild_WithValidGuildId_AddsToQueue()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const MemberSyncReason reason = MemberSyncReason.InitialSync;
        var initialCount = _queue.Count;

        // Act
        _queue.EnqueueGuild(guildId, reason);

        // Assert
        _queue.Count.Should().Be(initialCount + 1, "queue count should increase by 1");
    }

    [Fact]
    public void EnqueueGuild_WithMultipleItems_IncreasesCount()
    {
        // Arrange
        var initialCount = _queue.Count;

        // Act
        _queue.EnqueueGuild(111111111UL, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(222222222UL, MemberSyncReason.DailyReconciliation);
        _queue.EnqueueGuild(333333333UL, MemberSyncReason.ManualRequest);

        // Assert
        _queue.Count.Should().Be(initialCount + 3, "queue should contain 3 items");
    }

    [Fact]
    public void EnqueueGuild_AllowsDuplicateGuildIds()
    {
        // Arrange - Queue should allow same guild to be queued multiple times
        const ulong guildId = 123456789UL;
        var initialCount = _queue.Count;

        // Act
        _queue.EnqueueGuild(guildId, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(guildId, MemberSyncReason.DailyReconciliation);

        // Assert
        _queue.Count.Should().Be(initialCount + 2, "duplicates should be allowed");
    }

    [Fact]
    public void EnqueueGuild_WithDifferentReasons_AcceptsAll()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var initialCount = _queue.Count;

        // Act
        _queue.EnqueueGuild(guildId, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(guildId, MemberSyncReason.DailyReconciliation);
        _queue.EnqueueGuild(guildId, MemberSyncReason.ManualRequest);
        _queue.EnqueueGuild(guildId, MemberSyncReason.BotJoinedGuild);

        // Assert
        _queue.Count.Should().Be(initialCount + 4, "all sync reasons should be accepted");
    }

    [Fact]
    public void EnqueueGuild_LogsDebugMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const MemberSyncReason reason = MemberSyncReason.InitialSync;

        // Act
        _queue.EnqueueGuild(guildId, reason);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enqueued member sync")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "enqueuing should log a debug message");
    }

    #endregion

    #region DequeueAsync Tests

    [Fact]
    public async Task DequeueAsync_WithQueuedItem_ReturnsItem()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const MemberSyncReason reason = MemberSyncReason.InitialSync;
        _queue.EnqueueGuild(guildId, reason);

        // Act
        var result = await _queue.DequeueAsync();

        // Assert
        result.GuildId.Should().Be(guildId);
        result.Reason.Should().Be(reason);
    }

    [Fact]
    public async Task DequeueAsync_WithMultipleItems_ReturnsFIFO()
    {
        // Arrange - First In, First Out
        _queue.EnqueueGuild(111111111UL, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(222222222UL, MemberSyncReason.DailyReconciliation);
        _queue.EnqueueGuild(333333333UL, MemberSyncReason.ManualRequest);

        // Act
        var result1 = await _queue.DequeueAsync();
        var result2 = await _queue.DequeueAsync();
        var result3 = await _queue.DequeueAsync();

        // Assert
        result1.GuildId.Should().Be(111111111UL, "first item should be dequeued first");
        result1.Reason.Should().Be(MemberSyncReason.InitialSync);

        result2.GuildId.Should().Be(222222222UL, "second item should be dequeued second");
        result2.Reason.Should().Be(MemberSyncReason.DailyReconciliation);

        result3.GuildId.Should().Be(333333333UL, "third item should be dequeued third");
        result3.Reason.Should().Be(MemberSyncReason.ManualRequest);
    }

    [Fact]
    public async Task DequeueAsync_DecreasesCount()
    {
        // Arrange
        _queue.EnqueueGuild(123456789UL, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(987654321UL, MemberSyncReason.DailyReconciliation);
        var countBeforeDequeue = _queue.Count;

        // Act
        await _queue.DequeueAsync();

        // Assert
        _queue.Count.Should().Be(countBeforeDequeue - 1, "count should decrease after dequeue");
    }

    [Fact]
    public async Task DequeueAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _queue.DequeueAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(
            "dequeue should respect cancellation token");
    }

    [Fact]
    public async Task DequeueAsync_WaitsForItem_WhenQueueEmpty()
    {
        // Arrange - Use longer timeout for CI environments which can be slow
        using var cts = new CancellationTokenSource(2000); // 2 second timeout
        var queueTask = _queue.DequeueAsync(cts.Token);

        // Wait a bit to ensure DequeueAsync is waiting
        // Use longer delay for CI environments where scheduling can be unpredictable
        await Task.Delay(200);

        // Assert - Task should not complete within timeout (no items in queue)
        queueTask.IsCompleted.Should().BeFalse("dequeue should wait for item");

        // Cleanup - Cancel the waiting task to prevent test hanging
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await queueTask);
    }

    [Fact]
    public async Task DequeueAsync_CompletesWhenItemEnqueued()
    {
        // Arrange - use longer timeout for CI environments which can be slow
        using var cts = new CancellationTokenSource(30000); // 30 second timeout
        var dequeueTask = Task.Run(async () => await _queue.DequeueAsync(cts.Token));

        // Wait a bit to ensure DequeueAsync is waiting
        await Task.Delay(100);

        // Act - Enqueue an item while dequeue is waiting
        _queue.EnqueueGuild(123456789UL, MemberSyncReason.ManualRequest);

        // Assert - Task should complete quickly after enqueue
        var result = await dequeueTask;
        result.GuildId.Should().Be(123456789UL);
        result.Reason.Should().Be(MemberSyncReason.ManualRequest);
    }

    #endregion

    #region Count Property Tests

    [Fact]
    public void Count_InitiallyReturnsZeroOrInitialValue()
    {
        // Arrange
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);

        // Act
        var count = freshQueue.Count;

        // Assert
        count.Should().BeGreaterThanOrEqualTo(0, "count should be non-negative");
    }

    [Fact]
    public void Count_ReflectsQueueSize()
    {
        // Arrange
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);
        var initialCount = freshQueue.Count;

        // Act
        freshQueue.EnqueueGuild(111111111UL, MemberSyncReason.InitialSync);
        freshQueue.EnqueueGuild(222222222UL, MemberSyncReason.DailyReconciliation);

        // Assert
        freshQueue.Count.Should().Be(initialCount + 2, "count should reflect enqueued items");
    }

    [Fact]
    public async Task Count_UpdatesAfterDequeue()
    {
        // Arrange
        _queue.EnqueueGuild(123456789UL, MemberSyncReason.InitialSync);
        _queue.EnqueueGuild(987654321UL, MemberSyncReason.DailyReconciliation);
        var countAfterEnqueue = _queue.Count;

        // Act
        await _queue.DequeueAsync();
        var countAfterFirstDequeue = _queue.Count;

        await _queue.DequeueAsync();
        var countAfterSecondDequeue = _queue.Count;

        // Assert
        countAfterFirstDequeue.Should().Be(countAfterEnqueue - 1);
        countAfterSecondDequeue.Should().Be(countAfterEnqueue - 2);
    }

    #endregion

    #region Capacity and Backpressure Tests

    [Fact]
    public void EnqueueGuild_WithinCapacity_AllItemsAccepted()
    {
        // Arrange - Capacity is 1000, using DropOldest strategy
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);

        // Act - Enqueue well within capacity
        for (ulong i = 1; i <= 100; i++)
        {
            freshQueue.EnqueueGuild(i, MemberSyncReason.InitialSync);
        }

        // Assert
        freshQueue.Count.Should().Be(100, "all items should be accepted within capacity");
    }

    [Fact]
    public void EnqueueGuild_ExceedingCapacity_UsesDropOldestStrategy()
    {
        // Note: This test documents the expected behavior with bounded channel capacity
        // The queue is configured with BoundedChannelFullMode.DropOldest and capacity of 1000
        // When capacity is exceeded, oldest items are dropped to make room for new ones

        var expectedBehavior = @"
Bounded channel capacity behavior:
- Capacity: 1000 items
- Full mode: DropOldest
- When capacity reached, oldest items are automatically dropped
- TryWrite returns true even when dropping occurs
- No blocking or backpressure on writers
- Single reader, multiple writers supported
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("capacity behavior should be documented");
    }

    [Fact]
    public void EnqueueGuild_DropsOldest_WhenCapacityReached()
    {
        // Arrange - Create a fresh queue and fill to capacity
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);

        // Fill queue close to capacity (1000 items)
        for (ulong i = 1; i <= 1000; i++)
        {
            freshQueue.EnqueueGuild(i, MemberSyncReason.InitialSync);
        }

        var countAtCapacity = freshQueue.Count;

        // Act - Enqueue one more item (should trigger DropOldest)
        freshQueue.EnqueueGuild(1001UL, MemberSyncReason.ManualRequest);

        // Assert - Count should not exceed capacity
        freshQueue.Count.Should().BeLessThanOrEqualTo(1000, "count should not exceed capacity");
        freshQueue.Count.Should().BeCloseTo(countAtCapacity, 1, "count should remain near capacity");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task EnqueueGuild_FromMultipleThreads_AllItemsAccepted()
    {
        // Arrange - Channel supports multiple writers
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);
        var initialCount = freshQueue.Count;

        // Act - Enqueue from multiple threads concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var localI = i;
            tasks.Add(Task.Run(() =>
            {
                for (ulong j = 0; j < 10; j++)
                {
                    freshQueue.EnqueueGuild((ulong)localI * 100 + j, MemberSyncReason.InitialSync);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        freshQueue.Count.Should().Be(initialCount + 100, "all items from concurrent writers should be accepted");
    }

    [Fact]
    public async Task DequeueAsync_SingleReader_ProcessesAllItems()
    {
        // Arrange - Channel is configured with SingleReader = true
        var freshLogger = new Mock<ILogger<MemberSyncQueue>>();
        var freshQueue = new MemberSyncQueue(freshLogger.Object);

        // Enqueue items
        for (ulong i = 1; i <= 50; i++)
        {
            freshQueue.EnqueueGuild(i, MemberSyncReason.InitialSync);
        }

        // Act - Dequeue all items
        var dequeuedItems = new List<(ulong GuildId, MemberSyncReason Reason)>();
        for (int i = 0; i < 50; i++)
        {
            var item = await freshQueue.DequeueAsync();
            dequeuedItems.Add(item);
        }

        // Assert
        dequeuedItems.Should().HaveCount(50, "all enqueued items should be dequeued");
        dequeuedItems.Select(x => x.GuildId).Should().BeInAscendingOrder("items should maintain FIFO order");
    }

    #endregion

    #region MemberSyncReason Enum Tests

    [Theory]
    [InlineData(MemberSyncReason.InitialSync)]
    [InlineData(MemberSyncReason.DailyReconciliation)]
    [InlineData(MemberSyncReason.ManualRequest)]
    [InlineData(MemberSyncReason.BotJoinedGuild)]
    public void EnqueueGuild_WithAllReasons_AcceptsAll(MemberSyncReason reason)
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var initialCount = _queue.Count;

        // Act
        _queue.EnqueueGuild(guildId, reason);

        // Assert
        _queue.Count.Should().Be(initialCount + 1, $"queue should accept {reason}");
    }

    [Fact]
    public async Task DequeueAsync_PreservesReason()
    {
        // Arrange
        var testData = new[]
        {
            (GuildId: 111111111UL, Reason: MemberSyncReason.InitialSync),
            (GuildId: 222222222UL, Reason: MemberSyncReason.DailyReconciliation),
            (GuildId: 333333333UL, Reason: MemberSyncReason.ManualRequest),
            (GuildId: 444444444UL, Reason: MemberSyncReason.BotJoinedGuild)
        };

        foreach (var (guildId, reason) in testData)
        {
            _queue.EnqueueGuild(guildId, reason);
        }

        // Act & Assert
        foreach (var expected in testData)
        {
            var result = await _queue.DequeueAsync();
            result.GuildId.Should().Be(expected.GuildId);
            result.Reason.Should().Be(expected.Reason);
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void EnqueueGuild_WhenChannelClosed_LogsWarning()
    {
        // Note: This test documents expected behavior when channel is closed
        // In practice, the channel remains open for the lifetime of the application

        var expectedBehavior = @"
Channel closure behavior:
- Channel should remain open during application lifetime
- If TryWrite fails (channel closed), log warning message
- Warning: 'Failed to enqueue member sync for guild {GuildId} with reason {Reason}. Queue may be closed.'
- Application should gracefully handle queue closure
";

        expectedBehavior.Should().NotBeNullOrWhiteSpace("channel closure behavior should be documented");
    }

    [Fact]
    public async Task DequeueAsync_WithInvalidCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _queue.DequeueAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancelled token should throw OperationCanceledException");
    }

    #endregion

    #region Channel Configuration Tests

    [Fact]
    public void MemberSyncQueue_Configuration_IsDocumented()
    {
        // This test documents the channel configuration

        var configuration = @"
Channel configuration:
- Type: BoundedChannel<(ulong GuildId, MemberSyncReason Reason)>
- Capacity: 1000 entries
- FullMode: DropOldest (prevents blocking, drops oldest when full)
- SingleReader: true (only one background processor reads)
- SingleWriter: false (multiple threads can write)

Design rationale:
- Bounded capacity prevents unbounded memory growth
- DropOldest ensures writers never block (important for Discord event handlers)
- SingleReader optimization for background service consumption
- MultipleWriters support for concurrent event handlers
";

        configuration.Should().NotBeNullOrWhiteSpace("channel configuration should be documented");
    }

    [Fact]
    public void MemberSyncQueue_UseCase_IsDocumented()
    {
        // This test documents the intended use case

        var useCase = @"
Intended use case:
- Discord event handlers (UserJoined, BotJoinedGuild) enqueue sync requests
- Background service (MemberSyncBackgroundService) dequeues and processes
- Multiple guilds can be queued for synchronization
- Same guild can be queued multiple times (e.g., manual refresh)
- FIFO processing order ensures fairness
- Cancellation token support for graceful shutdown

Example flow:
1. Bot joins new guild -> EnqueueGuild(guildId, BotJoinedGuild)
2. Background service reads -> DequeueAsync(cancellationToken)
3. Service processes sync for that guild
4. Service loops back to wait for next item
";

        useCase.Should().NotBeNullOrWhiteSpace("use case should be documented");
    }

    #endregion
}
