using DiscordBot.Bot.Collections;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Collections;

/// <summary>
/// Unit tests for <see cref="LruConcurrentDictionary{TKey, TValue}"/>.
/// Tests cover LRU eviction, concurrent access, and dictionary operations.
/// </summary>
public class LruConcurrentDictionaryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsException()
    {
        // Arrange & Act
        var act = () => new LruConcurrentDictionary<string, int>(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Capacity must be greater than zero*")
            .And.ParamName.Should().Be("capacity");
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsException()
    {
        // Arrange & Act
        var act = () => new LruConcurrentDictionary<string, int>(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Capacity must be greater than zero*")
            .And.ParamName.Should().Be("capacity");
    }

    [Fact]
    public void Constructor_ValidCapacity_CreatesDictionary()
    {
        // Arrange & Act
        var dict = new LruConcurrentDictionary<string, int>(10);

        // Assert
        dict.Capacity.Should().Be(10);
        dict.Count.Should().Be(0);
    }

    #endregion

    #region GetOrAdd Tests

    [Fact]
    public void GetOrAdd_NewKey_AddsToFront()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var value = dict.GetOrAdd("key1", k => 100);

        // Assert
        value.Should().Be(100);
        dict.Count.Should().Be(1);
        dict.ContainsKey("key1").Should().BeTrue();

        var all = dict.GetAll();
        all[0].Key.Should().Be("key1", "new item should be at front (MRU)");
    }

    [Fact]
    public void GetOrAdd_ExistingKey_MovesToFront()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.GetOrAdd("key1", k => 1);
        dict.GetOrAdd("key2", k => 2);
        dict.GetOrAdd("key3", k => 3);

        // Key2 is now in the middle of LRU order: [key3, key2, key1]

        // Act - Access key1, should move to front
        var value = dict.GetOrAdd("key1", k => 999);

        // Assert
        value.Should().Be(1, "should return existing value, not call factory");
        dict.Count.Should().Be(3);

        var all = dict.GetAll();
        all[0].Key.Should().Be("key1", "accessed key should move to front (MRU)");
        all[1].Key.Should().Be("key3");
        all[2].Key.Should().Be("key2");
    }

    [Fact]
    public void GetOrAdd_WhenAtCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(3);

        dict.GetOrAdd("key1", k => 1);
        dict.GetOrAdd("key2", k => 2);
        dict.GetOrAdd("key3", k => 3);

        // Current LRU order: [key3, key2, key1] (key1 is least recently used)

        // Act - Add key4, should evict key1
        dict.GetOrAdd("key4", k => 4);

        // Assert
        dict.Count.Should().Be(3, "count should remain at capacity");
        dict.ContainsKey("key1").Should().BeFalse("least recently used item (key1) should be evicted");
        dict.ContainsKey("key2").Should().BeTrue();
        dict.ContainsKey("key3").Should().BeTrue();
        dict.ContainsKey("key4").Should().BeTrue();

        var all = dict.GetAll();
        all[0].Key.Should().Be("key4", "newly added item should be at front");
    }

    [Fact]
    public void GetOrAdd_EvictionWithAccessPattern_EvictsCorrectItem()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(3);

        dict.GetOrAdd("key1", k => 1);
        dict.GetOrAdd("key2", k => 2);
        dict.GetOrAdd("key3", k => 3);

        // Access key1 to make it most recently used
        dict.GetOrAdd("key1", k => 999);

        // Current LRU order: [key1, key3, key2] (key2 is least recently used)

        // Act - Add key4, should evict key2
        dict.GetOrAdd("key4", k => 4);

        // Assert
        dict.ContainsKey("key2").Should().BeFalse("key2 should be evicted as LRU");
        dict.ContainsKey("key1").Should().BeTrue("key1 was accessed recently");
        dict.ContainsKey("key3").Should().BeTrue();
        dict.ContainsKey("key4").Should().BeTrue();
    }

    [Fact]
    public void GetOrAdd_NullValueFactory_ThrowsException()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var act = () => dict.GetOrAdd("key1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("valueFactory");
    }

    [Fact]
    public void GetOrAdd_FactoryIsCalledOnlyForNewKeys()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        var factoryCalls = 0;

        // Act
        var value1 = dict.GetOrAdd("key1", k =>
        {
            factoryCalls++;
            return 100;
        });

        var value2 = dict.GetOrAdd("key1", k =>
        {
            factoryCalls++;
            return 200;
        });

        // Assert
        factoryCalls.Should().Be(1, "factory should only be called once for new key");
        value1.Should().Be(100);
        value2.Should().Be(100, "should return cached value without calling factory");
    }

    #endregion

    #region TryGetValue Tests

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, string>(5);
        dict.GetOrAdd("key1", k => "value1");

        // Act
        var found = dict.TryGetValue("key1", out var value);

        // Assert
        found.Should().BeTrue();
        value.Should().Be("value1");
    }

    [Fact]
    public void TryGetValue_NonExistingKey_ReturnsFalse()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, string>(5);

        // Act
        var found = dict.TryGetValue("nonexistent", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetValue_UpdatesLruOrder()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.GetOrAdd("key1", k => 1);
        dict.GetOrAdd("key2", k => 2);
        dict.GetOrAdd("key3", k => 3);

        // LRU order: [key3, key2, key1]

        // Act - Access key1 via TryGetValue
        dict.TryGetValue("key1", out _);

        // Assert
        var all = dict.GetAll();
        all[0].Key.Should().Be("key1", "TryGetValue should move key to front (MRU)");
    }

    [Fact]
    public void TryGetValue_WithValueType_DefaultIsReturnedOnMiss()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var found = dict.TryGetValue("nonexistent", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().Be(0, "default value for int should be returned");
    }

    #endregion

    #region AddOrUpdate Tests

    [Fact]
    public void AddOrUpdate_NewKey_AddsEntry()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        dict.AddOrUpdate("key1", 100);

        // Assert
        dict.Count.Should().Be(1);
        dict.TryGetValue("key1", out var value).Should().BeTrue();
        value.Should().Be(100);
    }

    [Fact]
    public void AddOrUpdate_ExistingKey_UpdatesValue()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 100);

        // Act
        dict.AddOrUpdate("key1", 200);

        // Assert
        dict.Count.Should().Be(1, "count should not increase");
        dict.TryGetValue("key1", out var value).Should().BeTrue();
        value.Should().Be(200, "value should be updated");
    }

    [Fact]
    public void AddOrUpdate_ExistingKey_MovesToFront()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // LRU order: [key3, key2, key1]

        // Act - Update key1
        dict.AddOrUpdate("key1", 999);

        // Assert
        var all = dict.GetAll();
        all[0].Key.Should().Be("key1", "updated key should move to front");
        all[0].Value.Should().Be(999);
    }

    [Fact]
    public void AddOrUpdate_AtCapacity_EvictsLru()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(3);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // Act - Add key4, should evict key1
        dict.AddOrUpdate("key4", 4);

        // Assert
        dict.Count.Should().Be(3);
        dict.ContainsKey("key1").Should().BeFalse("LRU item should be evicted");
        dict.ContainsKey("key4").Should().BeTrue();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ExistingKey_RemovesEntry()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);

        // Act
        var removed = dict.Remove("key1");

        // Assert
        removed.Should().BeTrue();
        dict.Count.Should().Be(1);
        dict.ContainsKey("key1").Should().BeFalse();
        dict.ContainsKey("key2").Should().BeTrue();
    }

    [Fact]
    public void Remove_NonExistingKey_ReturnsFalse()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var removed = dict.Remove("nonexistent");

        // Assert
        removed.Should().BeFalse();
        dict.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_LastItem_LeavesEmptyDictionary()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);

        // Act
        var removed = dict.Remove("key1");

        // Assert
        removed.Should().BeTrue();
        dict.Count.Should().Be(0);
        dict.GetAll().Should().BeEmpty();
    }

    #endregion

    #region ContainsKey Tests

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);

        // Act
        var contains = dict.ContainsKey("key1");

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_NonExistingKey_ReturnsFalse()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var contains = dict.ContainsKey("nonexistent");

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void ContainsKey_DoesNotUpdateLruOrder()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);

        // Get initial order
        var orderBefore = dict.GetAll().Select(kv => kv.Key).ToList();

        // Act - ContainsKey should not change LRU order
        dict.ContainsKey("key1");

        // Assert
        var orderAfter = dict.GetAll().Select(kv => kv.Key).ToList();
        orderAfter.Should().Equal(orderBefore, "ContainsKey should not modify LRU order");
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsAllEntries()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // Act
        var all = dict.GetAll();

        // Assert
        all.Should().HaveCount(3);
        all.Should().Contain(kv => kv.Key == "key1" && kv.Value == 1);
        all.Should().Contain(kv => kv.Key == "key2" && kv.Value == 2);
        all.Should().Contain(kv => kv.Key == "key3" && kv.Value == 3);
    }

    [Fact]
    public void GetAll_EmptyDictionary_ReturnsEmptyList()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var all = dict.GetAll();

        // Assert
        all.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_ReturnsInLruOrder()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // Access key1 to make it MRU
        dict.GetOrAdd("key1", k => 999);

        // Act
        var all = dict.GetAll();

        // Assert - Should be in MRU order: [key1, key3, key2]
        all[0].Key.Should().Be("key1", "most recently used should be first");
        all[1].Key.Should().Be("key3");
        all[2].Key.Should().Be("key2", "least recently used should be last");
    }

    [Fact]
    public void GetAll_ReturnsReadOnlyList()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);

        // Act
        var all = dict.GetAll();

        // Assert
        all.Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, int>>>();
    }

    #endregion

    #region Keys Tests

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // Act
        var keys = dict.Keys;

        // Assert
        keys.Should().HaveCount(3);
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
        keys.Should().Contain("key3");
    }

    [Fact]
    public void Keys_EmptyDictionary_ReturnsEmptyCollection()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var keys = dict.Keys;

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void Keys_ReturnsReadOnlyCollection()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);

        // Act
        var keys = dict.Keys;

        // Assert
        keys.Should().BeAssignableTo<IReadOnlyCollection<string>>();
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_EmptiesDictionary()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        dict.Count.Should().Be(3, "precondition: dictionary should have items");

        // Act
        dict.Clear();

        // Assert
        dict.Count.Should().Be(0);
        dict.GetAll().Should().BeEmpty();
        dict.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(3);
        dict.AddOrUpdate("key1", 1);
        dict.Clear();

        // Act - Reuse after clear
        dict.AddOrUpdate("key2", 2);

        // Assert
        dict.Count.Should().Be(1);
        dict.ContainsKey("key1").Should().BeFalse();
        dict.ContainsKey("key2").Should().BeTrue();
    }

    #endregion

    #region EstimatedBytes Tests

    [Fact]
    public void EstimatedBytes_ReturnsCorrectCalculation()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        const int bytesPerKey = 20;
        const int bytesPerValue = 4;

        // Act
        var estimatedBytes = dict.EstimatedBytes(bytesPerKey, bytesPerValue);

        // Assert
        // Per entry: 20 (key) + 4 (value) + 40 (node overhead) + 24 (dict overhead) = 88
        // Total: (3 entries * 88) + 64 (object overhead) = 328
        estimatedBytes.Should().Be(328);
    }

    [Fact]
    public void EstimatedBytes_EmptyDictionary_ReturnsBaseOverhead()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(5);

        // Act
        var estimatedBytes = dict.EstimatedBytes(20, 4);

        // Assert
        estimatedBytes.Should().Be(64, "should only include object overhead");
    }

    [Fact]
    public void EstimatedBytes_ScalesWithEntryCount()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(10);

        for (int i = 0; i < 5; i++)
        {
            dict.AddOrUpdate($"key{i}", i);
        }

        var bytes5 = dict.EstimatedBytes(10, 4);

        for (int i = 5; i < 10; i++)
        {
            dict.AddOrUpdate($"key{i}", i);
        }

        var bytes10 = dict.EstimatedBytes(10, 4);

        // Assert
        bytes10.Should().BeGreaterThan(bytes5, "more entries should use more memory");
        (bytes10 - bytes5).Should().Be(5 * (10 + 4 + 40 + 24), "difference should be 5 entries");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ThreadSafety_ConcurrentOperations_NoDataCorruption()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<int, string>(1000);
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var barrier = new System.Threading.Barrier(threadCount);

        // Act - Multiple threads performing concurrent operations
        Parallel.For(0, threadCount, threadIndex =>
        {
            barrier.SignalAndWait(); // Ensure all threads start at roughly the same time

            for (int i = 0; i < operationsPerThread; i++)
            {
                var key = threadIndex * operationsPerThread + i;

                // Mix of operations
                dict.GetOrAdd(key, k => $"value-{k}");
                dict.TryGetValue(key, out _);
                dict.ContainsKey(key);

                if (i % 10 == 0)
                {
                    dict.Remove(key - 5);
                }
            }
        });

        // Assert
        dict.Count.Should().BeLessThanOrEqualTo(1000, "dictionary should respect capacity limit");
        dict.Count.Should().BeGreaterThan(0, "dictionary should have entries");

        // Verify no corruption - should not throw
        var all = dict.GetAll();
        all.Should().NotBeNull();
        all.Count.Should().Be(dict.Count);
    }

    [Fact]
    public void ThreadSafety_ConcurrentGetOrAdd_SameKey_CallsFactoryOnce()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(10);
        var factoryCalls = 0;
        const int threadCount = 20;
        var barrier = new System.Threading.Barrier(threadCount);
        var results = new int[threadCount];

        // Act - Multiple threads trying to add the same key
        Parallel.For(0, threadCount, threadIndex =>
        {
            barrier.SignalAndWait();

            results[threadIndex] = dict.GetOrAdd("shared-key", k =>
            {
                Interlocked.Increment(ref factoryCalls);
                Thread.Sleep(10); // Simulate some work
                return 42;
            });
        });

        // Assert
        factoryCalls.Should().Be(1, "factory should only be called once despite concurrent access");
        results.Should().OnlyContain(x => x == 42, "all threads should get the same value");
        dict.Count.Should().Be(1);
    }

    [Fact]
    public void ThreadSafety_ConcurrentReadsAndWrites_ConsistentState()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<int, string>(500);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var testDuration = TimeSpan.FromMilliseconds(500);

        // Act - Concurrent writes and reads
        var writeTask = Task.Run(() =>
        {
            int counter = 0;
            while (stopwatch.Elapsed < testDuration)
            {
                dict.AddOrUpdate(counter % 100, $"value-{counter}");
                counter++;
                Thread.Sleep(1);
            }
        });

        var readTask = Task.Run(() =>
        {
            while (stopwatch.Elapsed < testDuration)
            {
                var count = dict.Count;
                var keys = dict.Keys;
                var all = dict.GetAll();

                // Sanity checks - no exceptions should occur
                count.Should().BeGreaterThanOrEqualTo(0);
                count.Should().BeLessThanOrEqualTo(dict.Capacity);
                keys.Should().NotBeNull();
                all.Should().NotBeNull();

                Thread.Sleep(1);
            }
        });

        var removeTask = Task.Run(() =>
        {
            int counter = 0;
            while (stopwatch.Elapsed < testDuration)
            {
                dict.Remove(counter % 100);
                counter++;
                Thread.Sleep(2);
            }
        });

        // Assert - No exceptions or deadlocks
        Task.WaitAll(writeTask, readTask, removeTask);
        dict.Count.Should().BeLessThanOrEqualTo(dict.Capacity);
    }

    [Fact]
    public void ThreadSafety_ConcurrentEvictions_MaintainsCapacity()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<int, int>(50);
        const int totalItems = 500;

        // Act - Fill beyond capacity from multiple threads
        Parallel.For(0, totalItems, i =>
        {
            dict.GetOrAdd(i, k => k * 2);
            Thread.Sleep(1); // Small delay to ensure interleaving
        });

        // Assert
        dict.Count.Should().BeLessThanOrEqualTo(50, "should never exceed capacity");
        dict.Count.Should().BeGreaterThan(0, "should have items");
        dict.GetAll().Should().HaveCount(dict.Count);

        // Verify all values are correct (no corruption)
        var all = dict.GetAll();
        foreach (var kvp in all)
        {
            kvp.Value.Should().Be(kvp.Key * 2, "values should match factory function");
        }

        // Verify keys are unique (no duplicates)
        var keys = dict.Keys.ToList();
        keys.Should().OnlyHaveUniqueItems("dictionary should not have duplicate keys");
        keys.Count.Should().Be(dict.Count);
    }

    #endregion

    #region Capacity Boundary Tests

    [Fact]
    public void Capacity_SingleItemDictionary_WorksCorrectly()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(1);

        // Act
        dict.AddOrUpdate("key1", 1);
        dict.AddOrUpdate("key2", 2);
        dict.AddOrUpdate("key3", 3);

        // Assert
        dict.Count.Should().Be(1, "single-item dictionary should only hold one item");
        dict.ContainsKey("key3").Should().BeTrue("last added item should be present");
        dict.ContainsKey("key1").Should().BeFalse("first item should be evicted");
        dict.ContainsKey("key2").Should().BeFalse("second item should be evicted");
    }

    [Fact]
    public void Capacity_LargeCapacity_HandlesCorrectly()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<int, int>(10000);

        // Act - Fill to capacity
        for (int i = 0; i < 10000; i++)
        {
            dict.AddOrUpdate(i, i * 2);
        }

        // Assert
        dict.Count.Should().Be(10000);

        // Add one more to test eviction
        dict.AddOrUpdate(10000, 20000);
        dict.Count.Should().Be(10000, "count should remain at capacity");
        dict.ContainsKey(0).Should().BeFalse("first item (LRU) should be evicted");
        dict.ContainsKey(10000).Should().BeTrue("newly added item should be present");
    }

    #endregion

    #region Complex LRU Scenarios

    [Fact]
    public void LruEviction_ComplexAccessPattern_EvictsCorrectly()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(4);

        // Build initial state
        dict.AddOrUpdate("A", 1);
        dict.AddOrUpdate("B", 2);
        dict.AddOrUpdate("C", 3);
        dict.AddOrUpdate("D", 4);

        // Access pattern: access A and C
        dict.TryGetValue("A", out _);
        dict.TryGetValue("C", out _);

        // Current LRU order: [C, A, D, B] (B is LRU)

        // Act - Add E, should evict B
        dict.AddOrUpdate("E", 5);

        // Assert
        dict.ContainsKey("B").Should().BeFalse("B should be evicted as LRU");
        dict.ContainsKey("A").Should().BeTrue("A was accessed");
        dict.ContainsKey("C").Should().BeTrue("C was accessed");
        dict.ContainsKey("D").Should().BeTrue("D was not accessed but newer than B");
        dict.ContainsKey("E").Should().BeTrue("E is newly added");

        var order = dict.GetAll().Select(kv => kv.Key).ToList();
        order[0].Should().Be("E", "E should be MRU");
        order[3].Should().Be("D", "D should be LRU");
    }

    [Fact]
    public void LruEviction_UpdateExistingKey_DoesNotChangeCount()
    {
        // Arrange
        var dict = new LruConcurrentDictionary<string, int>(3);

        dict.AddOrUpdate("A", 1);
        dict.AddOrUpdate("B", 2);
        dict.AddOrUpdate("C", 3);

        // Act - Update existing key
        dict.AddOrUpdate("B", 999);

        // Assert
        dict.Count.Should().Be(3, "updating existing key should not change count");
        dict.TryGetValue("B", out var value).Should().BeTrue();
        value.Should().Be(999);
    }

    #endregion
}
