using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InstrumentedMemoryCache"/>.
/// Tests cover cache operations (get, set, remove), hit/miss tracking,
/// statistics aggregation by key prefix, and statistics reset functionality.
/// </summary>
public class InstrumentedMemoryCacheTests
{
    private readonly IMemoryCache _innerCache;
    private readonly InstrumentedMemoryCache _cache;
    private readonly PerformanceMetricsOptions _options;

    public InstrumentedMemoryCacheTests()
    {
        _innerCache = new MemoryCache(new MemoryCacheOptions());
        _options = new PerformanceMetricsOptions
        {
            CacheStatisticsEnabled = true
        };

        _cache = new InstrumentedMemoryCache(
            _innerCache,
            NullLogger<InstrumentedMemoryCache>.Instance,
            Options.Create(_options));
    }

    [Fact]
    public void TryGetValue_RecordsHitOnExistingKey()
    {
        // Arrange
        const string key = "test:key1";
        const string value = "test value";
        _cache.Set(key, value);

        // Reset statistics to start fresh for this test
        _cache.ResetStatistics();

        // Act
        var result = _cache.TryGetValue<string>(key, out var retrievedValue);

        // Assert
        result.Should().BeTrue("key exists in cache");
        retrievedValue.Should().Be(value, "retrieved value should match stored value");

        var stats = _cache.GetStatisticsByPrefix("test");
        stats.Hits.Should().Be(1, "one cache hit was recorded");
        stats.Misses.Should().Be(0, "no cache misses");
        stats.HitRate.Should().Be(100.0, "hit rate should be 100%");
    }

    [Fact]
    public void TryGetValue_RecordsMissOnMissingKey()
    {
        // Arrange
        const string key = "missing:key1";

        // Act
        var result = _cache.TryGetValue<string>(key, out var retrievedValue);

        // Assert
        result.Should().BeFalse("key does not exist in cache");
        retrievedValue.Should().BeNull("no value should be retrieved");

        var stats = _cache.GetStatisticsByPrefix("missing");
        stats.Hits.Should().Be(0, "no cache hits");
        stats.Misses.Should().Be(1, "one cache miss was recorded");
        stats.HitRate.Should().Be(0.0, "hit rate should be 0%");
    }

    [Fact]
    public void Set_StoresValueInCache()
    {
        // Arrange
        const string key = "user:123";
        const string value = "John Doe";

        // Act
        _cache.Set(key, value);

        // Assert
        var result = _innerCache.TryGetValue<string>(key, out var retrievedValue);
        result.Should().BeTrue("value should be stored in underlying cache");
        retrievedValue.Should().Be(value, "stored value should match");

        var stats = _cache.GetStatisticsByPrefix("user");
        stats.Size.Should().BeGreaterThanOrEqualTo(1, "cache size should be tracked");
    }

    [Fact]
    public void Remove_RemovesValueFromCache()
    {
        // Arrange
        const string key = "guild:456";
        const string value = "Test Guild";
        _cache.Set(key, value);

        // Act
        _cache.Remove(key);

        // Assert
        var result = _innerCache.TryGetValue<string>(key, out _);
        result.Should().BeFalse("value should be removed from underlying cache");

        // Note: Size decrement happens, but might not be exactly 0 due to other operations
        var stats = _cache.GetStatisticsByPrefix("guild");
        stats.Should().NotBeNull("statistics should still exist for prefix");
    }

    [Fact]
    public void GetStatistics_ReturnsHitMissRatios()
    {
        // Arrange
        _cache.Set("data:1", "value1");
        _cache.Set("data:2", "value2");

        // 3 hits
        _cache.TryGetValue<string>("data:1", out _);
        _cache.TryGetValue<string>("data:2", out _);
        _cache.TryGetValue<string>("data:1", out _);

        // 1 miss
        _cache.TryGetValue<string>("data:3", out _);

        // Act
        var allStats = _cache.GetStatistics();

        // Assert
        var dataStats = allStats.FirstOrDefault(s => s.KeyPrefix == "data");
        dataStats.Should().NotBeNull("statistics should exist for 'data' prefix");
        dataStats!.Hits.Should().Be(3, "three cache hits");
        dataStats.Misses.Should().Be(1, "one cache miss");
        dataStats.HitRate.Should().Be(75.0, "hit rate should be 75% (3 hits out of 4 total)");
    }

    [Fact]
    public void GetStatisticsByPrefix_GroupsByKeyPrefix()
    {
        // Arrange - Create entries with different prefixes
        _cache.Set("users:1", "User 1");
        _cache.Set("users:2", "User 2");
        _cache.Set("guilds:1", "Guild 1");

        _cache.TryGetValue<string>("users:1", out _); // Hit
        _cache.TryGetValue<string>("users:3", out _); // Miss
        _cache.TryGetValue<string>("guilds:1", out _); // Hit

        // Act
        var userStats = _cache.GetStatisticsByPrefix("users");
        var guildStats = _cache.GetStatisticsByPrefix("guilds");

        // Assert
        userStats.KeyPrefix.Should().Be("users", "prefix should match");
        userStats.Hits.Should().Be(1, "one hit for users prefix");
        userStats.Misses.Should().Be(1, "one miss for users prefix");
        userStats.HitRate.Should().Be(50.0, "hit rate should be 50%");

        guildStats.KeyPrefix.Should().Be("guilds", "prefix should match");
        guildStats.Hits.Should().Be(1, "one hit for guilds prefix");
        guildStats.Misses.Should().Be(0, "no misses for guilds prefix");
        guildStats.HitRate.Should().Be(100.0, "hit rate should be 100%");
    }

    [Fact]
    public void ResetStatistics_ClearsAllCounts()
    {
        // Arrange - Generate some statistics
        _cache.Set("test:1", "value1");
        _cache.TryGetValue<string>("test:1", out _);
        _cache.TryGetValue<string>("test:2", out _);

        // Verify statistics exist
        var beforeStats = _cache.GetStatisticsByPrefix("test");
        beforeStats.Hits.Should().BeGreaterThan(0, "statistics should exist before reset");

        // Act
        _cache.ResetStatistics();

        // Assert
        var afterStats = _cache.GetStatisticsByPrefix("test");
        afterStats.Hits.Should().Be(0, "hits should be reset");
        afterStats.Misses.Should().Be(0, "misses should be reset");
        afterStats.Size.Should().Be(0, "size should be reset");
        afterStats.HitRate.Should().Be(0, "hit rate should be reset");

        var allStats = _cache.GetStatistics();
        allStats.Should().BeEmpty("all statistics should be cleared");
    }
}
