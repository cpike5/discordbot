using DiscordBot.Bot.Collections;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Collections;

/// <summary>
/// Unit tests for <see cref="BoundedTimestampQueue{T}"/>.
/// Tests cover FIFO eviction, time-based filtering, thread safety, and memory estimation.
/// </summary>
public class BoundedTimestampQueueTests
{
    /// <summary>
    /// Test record implementing ITimestamped for testing purposes.
    /// </summary>
    private record TestRecord(DateTime Timestamp, string Data) : ITimestamped;

    #region Constructor Tests

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsException()
    {
        // Arrange & Act
        var act = () => new BoundedTimestampQueue<TestRecord>(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Capacity must be greater than zero*")
            .And.ParamName.Should().Be("capacity");
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsException()
    {
        // Arrange & Act
        var act = () => new BoundedTimestampQueue<TestRecord>(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Capacity must be greater than zero*")
            .And.ParamName.Should().Be("capacity");
    }

    [Fact]
    public void Constructor_ValidCapacity_CreatesQueue()
    {
        // Arrange & Act
        var queue = new BoundedTimestampQueue<TestRecord>(10);

        // Assert
        queue.Capacity.Should().Be(10);
        queue.Count.Should().Be(0);
        queue.IsAtCapacity.Should().BeFalse();
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_BelowCapacity_AddsItem()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var timestamp = DateTime.UtcNow;
        var item = new TestRecord(timestamp, "test-data");

        // Act
        queue.Enqueue(item);

        // Assert
        queue.Count.Should().Be(1, "item should be added to queue");
        queue.IsAtCapacity.Should().BeFalse("queue should not be at capacity");

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items.Should().ContainSingle()
            .Which.Should().Be(item);
    }

    [Fact]
    public void Enqueue_MultipleItems_MaintainsOrder()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var baseTime = DateTime.UtcNow;
        var items = new[]
        {
            new TestRecord(baseTime, "first"),
            new TestRecord(baseTime.AddSeconds(1), "second"),
            new TestRecord(baseTime.AddSeconds(2), "third")
        };

        // Act
        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Assert
        queue.Count.Should().Be(3);
        var retrieved = queue.GetItemsAfter(DateTime.MinValue);
        retrieved.Should().ContainInOrder(items);
    }

    [Fact]
    public void Enqueue_WhenAtCapacity_RemovesOldestItem()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(3);
        var baseTime = DateTime.UtcNow;

        var firstItem = new TestRecord(baseTime, "first");
        var secondItem = new TestRecord(baseTime.AddSeconds(1), "second");
        var thirdItem = new TestRecord(baseTime.AddSeconds(2), "third");
        var fourthItem = new TestRecord(baseTime.AddSeconds(3), "fourth");

        // Act - Fill to capacity
        queue.Enqueue(firstItem);
        queue.Enqueue(secondItem);
        queue.Enqueue(thirdItem);

        // Verify at capacity
        queue.IsAtCapacity.Should().BeTrue("queue should be at capacity");
        queue.Count.Should().Be(3);

        // Act - Add one more to trigger eviction
        queue.Enqueue(fourthItem);

        // Assert
        queue.Count.Should().Be(3, "count should remain at capacity");
        queue.IsAtCapacity.Should().BeTrue("queue should still be at capacity");

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items.Should().HaveCount(3);
        items.Should().NotContain(firstItem, "oldest item should be evicted (FIFO)");
        items.Should().ContainInOrder(secondItem, thirdItem, fourthItem);
    }

    [Fact]
    public void Enqueue_ContinuouslyBeyondCapacity_MaintainsFifoEviction()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(3);
        var baseTime = DateTime.UtcNow;

        // Act - Add 6 items (double the capacity)
        for (int i = 0; i < 6; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddSeconds(i), $"item-{i}"));
        }

        // Assert
        queue.Count.Should().Be(3, "queue should maintain capacity limit");

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items.Should().HaveCount(3);
        items[0].Data.Should().Be("item-3", "should have 4th item (index 3)");
        items[1].Data.Should().Be("item-4", "should have 5th item (index 4)");
        items[2].Data.Should().Be("item-5", "should have 6th item (index 5)");
    }

    #endregion

    #region CountAfter Tests

    [Fact]
    public void CountAfter_ReturnsOnlyItemsInTimeWindow()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        var baseTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-10), "old-1"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-5), "recent-1"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-4), "recent-2"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-3), "recent-3"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-1), "recent-4"));

        // Act
        var countLastFiveMinutes = queue.CountAfter(baseTime.AddMinutes(-5));

        // Assert
        countLastFiveMinutes.Should().Be(4, "should count only items from last 5 minutes (inclusive)");
    }

    [Fact]
    public void CountAfter_EmptyQueue_ReturnsZero()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);

        // Act
        var count = queue.CountAfter(DateTime.UtcNow.AddMinutes(-5));

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CountAfter_NoItemsInWindow_ReturnsZero()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        var baseTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-20), "old-1"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-15), "old-2"));

        // Act
        var count = queue.CountAfter(baseTime.AddMinutes(-5));

        // Assert
        count.Should().Be(0, "no items should fall within the time window");
    }

    [Fact]
    public void CountAfter_AllItemsInWindow_ReturnsTotal()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddMinutes(i), $"item-{i}"));
        }

        // Act
        var count = queue.CountAfter(baseTime);

        // Assert
        count.Should().Be(5, "all items should fall within the time window");
    }

    [Fact]
    public void CountAfter_CutoffInclusiveBoundary_IncludesExactMatch()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var exactTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(exactTime.AddSeconds(-1), "before"));
        queue.Enqueue(new TestRecord(exactTime, "exact"));
        queue.Enqueue(new TestRecord(exactTime.AddSeconds(1), "after"));

        // Act
        var count = queue.CountAfter(exactTime);

        // Assert
        count.Should().Be(2, "cutoff should be inclusive (>= cutoff)");
    }

    #endregion

    #region CountAfterWithPredicate Tests

    [Fact]
    public void CountAfterWithPredicate_FiltersCorrectly()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        var baseTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-5), "apple"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-4), "banana"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-3), "apricot"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-2), "avocado"));
        queue.Enqueue(new TestRecord(baseTime.AddMinutes(-10), "artichoke")); // Outside time window

        // Act - Count items starting with 'a' in last 5 minutes
        var count = queue.CountAfterWithPredicate(
            baseTime.AddMinutes(-5),
            item => item.Data.StartsWith('a'));

        // Assert
        count.Should().Be(3, "should match apple, apricot, and avocado (artichoke is outside time window)");
    }

    [Fact]
    public void CountAfterWithPredicate_NullPredicate_ThrowsException()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        queue.Enqueue(new TestRecord(DateTime.UtcNow, "test"));

        // Act
        var act = () => queue.CountAfterWithPredicate(DateTime.UtcNow, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    [Fact]
    public void CountAfterWithPredicate_NoMatches_ReturnsZero()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        var baseTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(baseTime, "apple"));
        queue.Enqueue(new TestRecord(baseTime.AddSeconds(1), "banana"));

        // Act
        var count = queue.CountAfterWithPredicate(
            baseTime,
            item => item.Data.StartsWith('z'));

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CountAfterWithPredicate_AllMatch_ReturnsTotal()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddSeconds(i), $"item-{i}"));
        }

        // Act
        var count = queue.CountAfterWithPredicate(
            baseTime,
            item => item.Data.StartsWith("item"));

        // Assert
        count.Should().Be(5);
    }

    #endregion

    #region GetItemsAfter Tests

    [Fact]
    public void GetItemsAfter_ReturnsItemsInTimeWindow()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        var baseTime = DateTime.UtcNow;

        var oldItem = new TestRecord(baseTime.AddMinutes(-10), "old");
        var recent1 = new TestRecord(baseTime.AddMinutes(-5), "recent-1");
        var recent2 = new TestRecord(baseTime.AddMinutes(-3), "recent-2");

        queue.Enqueue(oldItem);
        queue.Enqueue(recent1);
        queue.Enqueue(recent2);

        // Act
        var items = queue.GetItemsAfter(baseTime.AddMinutes(-5));

        // Assert
        items.Should().HaveCount(2);
        items.Should().ContainInOrder(recent1, recent2);
        items.Should().NotContain(oldItem);
    }

    [Fact]
    public void GetItemsAfter_EmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);

        // Act
        var items = queue.GetItemsAfter(DateTime.UtcNow);

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public void GetItemsAfter_ReturnsReadOnlyList()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10);
        queue.Enqueue(new TestRecord(DateTime.UtcNow, "test"));

        // Act
        var items = queue.GetItemsAfter(DateTime.MinValue);

        // Assert
        items.Should().BeAssignableTo<IReadOnlyList<TestRecord>>();
    }

    [Fact]
    public void GetItemsAfter_MaintainsChronologicalOrder()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var baseTime = DateTime.UtcNow;

        var items = new[]
        {
            new TestRecord(baseTime.AddSeconds(10), "third"),
            new TestRecord(baseTime.AddSeconds(0), "first"),
            new TestRecord(baseTime.AddSeconds(5), "second"),
            new TestRecord(baseTime.AddSeconds(15), "fourth")
        };

        // Enqueue in non-chronological order
        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Act
        var retrieved = queue.GetItemsAfter(DateTime.MinValue);

        // Assert - Should maintain insertion order (not sorted)
        retrieved.Should().HaveCount(4);
        retrieved.Should().ContainInOrder(items); // FIFO order preserved
    }

    #endregion

    #region EstimatedBytes Tests

    [Fact]
    public void EstimatedBytes_ReturnsCorrectCalculation()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(100);
        const int bytesPerItem = 50;

        // Act
        var estimatedBytes = queue.EstimatedBytes(bytesPerItem);

        // Assert
        // Expected: (100 capacity * 50 bytes per item) + 64 overhead = 5064
        estimatedBytes.Should().Be(5064);
    }

    [Fact]
    public void EstimatedBytes_WithDifferentCapacity_ScalesCorrectly()
    {
        // Arrange
        var smallQueue = new BoundedTimestampQueue<TestRecord>(10);
        var largeQueue = new BoundedTimestampQueue<TestRecord>(100);
        const int bytesPerItem = 100;

        // Act
        var smallEstimate = smallQueue.EstimatedBytes(bytesPerItem);
        var largeEstimate = largeQueue.EstimatedBytes(bytesPerItem);

        // Assert
        smallEstimate.Should().Be(1064); // (10 * 100) + 64
        largeEstimate.Should().Be(10064); // (100 * 100) + 64
        largeEstimate.Should().BeGreaterThan(smallEstimate);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_EmptiesQueue()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(5);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddSeconds(i), $"item-{i}"));
        }

        queue.Count.Should().Be(5, "precondition: queue should have items");

        // Act
        queue.Clear();

        // Assert
        queue.Count.Should().Be(0);
        queue.IsAtCapacity.Should().BeFalse();
        queue.GetItemsAfter(DateTime.MinValue).Should().BeEmpty();
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(3);
        var baseTime = DateTime.UtcNow;

        queue.Enqueue(new TestRecord(baseTime, "first"));
        queue.Clear();

        // Act - Reuse after clear
        var newItem = new TestRecord(baseTime.AddSeconds(10), "second");
        queue.Enqueue(newItem);

        // Assert
        queue.Count.Should().Be(1);
        queue.GetItemsAfter(DateTime.MinValue).Should().ContainSingle()
            .Which.Should().Be(newItem);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ThreadSafety_ConcurrentEnqueues_NoDataCorruption()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(1000);
        var baseTime = DateTime.UtcNow;
        const int threadCount = 10;
        const int itemsPerThread = 100;
        var barrier = new System.Threading.Barrier(threadCount);

        // Act - Multiple threads enqueuing concurrently
        Parallel.For(0, threadCount, threadIndex =>
        {
            barrier.SignalAndWait(); // Ensure all threads start at roughly the same time

            for (int i = 0; i < itemsPerThread; i++)
            {
                var timestamp = baseTime.AddMilliseconds(threadIndex * itemsPerThread + i);
                queue.Enqueue(new TestRecord(timestamp, $"thread-{threadIndex}-item-{i}"));
            }
        });

        // Assert
        queue.Count.Should().Be(1000, "queue should be at capacity with no data loss");

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items.Should().HaveCount(1000);
        items.Should().OnlyHaveUniqueItems(item => item.Data);
    }

    [Fact]
    public void ThreadSafety_ConcurrentEnqueuesAndReads_ConsistentState()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(500);
        var baseTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var testDuration = TimeSpan.FromMilliseconds(500);

        // Act - Concurrent writes and reads
        var writeTask = Task.Run(() =>
        {
            int counter = 0;
            while (stopwatch.Elapsed < testDuration)
            {
                queue.Enqueue(new TestRecord(baseTime.AddMilliseconds(counter), $"item-{counter}"));
                counter++;
                Thread.Sleep(1); // Small delay to allow interleaving
            }
        });

        var readTask = Task.Run(() =>
        {
            while (stopwatch.Elapsed < testDuration)
            {
                var count = queue.Count;
                var items = queue.GetItemsAfter(baseTime);
                var countAfter = queue.CountAfter(baseTime);

                // Sanity checks - no exceptions should occur
                count.Should().BeGreaterThanOrEqualTo(0);
                count.Should().BeLessThanOrEqualTo(queue.Capacity);
                items.Should().NotBeNull();
                countAfter.Should().BeGreaterThanOrEqualTo(0);

                Thread.Sleep(1);
            }
        });

        // Assert - No exceptions or deadlocks
        Task.WaitAll(writeTask, readTask);
        queue.Count.Should().BeLessThanOrEqualTo(queue.Capacity);
    }

    [Fact]
    public void ThreadSafety_ConcurrentCountAfterCalls_NoExceptions()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(100);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddSeconds(i), $"item-{i}"));
        }

        // Act - Multiple threads calling CountAfter concurrently
        Parallel.For(0, 20, i =>
        {
            var cutoff = baseTime.AddSeconds(i * 5);
            var count = queue.CountAfter(cutoff);

            // Assert - Should not throw and should return valid count
            count.Should().BeGreaterThanOrEqualTo(0);
            count.Should().BeLessThanOrEqualTo(100);
        });
    }

    #endregion

    #region Capacity Boundary Tests

    [Fact]
    public void Capacity_SingleItemQueue_WorksCorrectly()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(1);
        var baseTime = DateTime.UtcNow;

        // Act
        queue.Enqueue(new TestRecord(baseTime, "first"));
        queue.Enqueue(new TestRecord(baseTime.AddSeconds(1), "second"));
        queue.Enqueue(new TestRecord(baseTime.AddSeconds(2), "third"));

        // Assert
        queue.Count.Should().Be(1);
        queue.IsAtCapacity.Should().BeTrue();

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items.Should().ContainSingle()
            .Which.Data.Should().Be("third", "last item should remain");
    }

    [Fact]
    public void Capacity_LargeCapacity_HandlesCorrectly()
    {
        // Arrange
        var queue = new BoundedTimestampQueue<TestRecord>(10000);
        var baseTime = DateTime.UtcNow;

        // Act - Fill to capacity
        for (int i = 0; i < 10000; i++)
        {
            queue.Enqueue(new TestRecord(baseTime.AddSeconds(i), $"item-{i}"));
        }

        // Assert
        queue.Count.Should().Be(10000);
        queue.IsAtCapacity.Should().BeTrue();

        // Add one more to test eviction
        queue.Enqueue(new TestRecord(baseTime.AddSeconds(10000), "item-10000"));
        queue.Count.Should().Be(10000, "count should remain at capacity");

        var items = queue.GetItemsAfter(DateTime.MinValue);
        items[0].Data.Should().Be("item-1", "first item should be evicted");
        items[9999].Data.Should().Be("item-10000", "last item should be present");
    }

    #endregion
}
