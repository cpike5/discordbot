using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for ChannelActivityRepository.
/// </summary>
public class ChannelActivityRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ChannelActivityRepository _repository;
    private readonly Mock<ILogger<ChannelActivityRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<ChannelActivitySnapshot>>> _mockBaseLogger;

    public ChannelActivityRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<ChannelActivityRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<ChannelActivitySnapshot>>>();
        _repository = new ChannelActivityRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Helper method to create and save a Guild entity for testing.
    /// </summary>
    private async Task CreateTestGuildAsync(ulong guildId)
    {
        var guild = new Guild
        {
            Id = guildId,
            Name = $"Test Guild {guildId}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetChannelRankingsAsync_ReturnsCorrectlyOrderedByMessageCount()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        // Channel 1: 500 total messages (200 + 300)
        var snapshot1a = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 111UL,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 200,
            UniqueUsers = 10,
            AverageMessageLength = 50.5,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot1b = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 111UL,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 300,
            UniqueUsers = 15,
            AverageMessageLength = 55.3,
            CreatedAt = DateTime.UtcNow
        };

        // Channel 2: 800 total messages
        var snapshot2 = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 222UL,
            ChannelName = "memes",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 800,
            UniqueUsers = 25,
            AverageMessageLength = 30.2,
            CreatedAt = DateTime.UtcNow
        };

        // Channel 3: 150 total messages
        var snapshot3 = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 333UL,
            ChannelName = "announcements",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 150,
            UniqueUsers = 5,
            AverageMessageLength = 120.7,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddRangeAsync(snapshot1a, snapshot1b, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelRankingsAsync(guildId, startDate, endDate, limit: 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].ChannelId.Should().Be(222UL); // Most active with 800 messages
        result[0].ChannelName.Should().Be("memes");
        result[1].ChannelId.Should().Be(111UL); // Second with 500 messages
        result[1].ChannelName.Should().Be("general");
        result[2].ChannelId.Should().Be(333UL); // Third with 150 messages
        result[2].ChannelName.Should().Be("announcements");
    }

    [Fact]
    public async Task GetChannelRankingsAsync_WithLimit_ReturnsOnlyTopN()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        for (int i = 1; i <= 5; i++)
        {
            var snapshot = new ChannelActivitySnapshot
            {
                GuildId = guildId,
                ChannelId = (ulong)i,
                ChannelName = $"channel-{i}",
                PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Granularity = SnapshotGranularity.Daily,
                MessageCount = i * 100,
                UniqueUsers = i * 5,
                AverageMessageLength = 50.0,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ChannelActivitySnapshots.AddAsync(snapshot);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelRankingsAsync(guildId, startDate, endDate, limit: 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].ChannelId.Should().Be(5UL); // Highest message count
        result[1].ChannelId.Should().Be(4UL);
        result[2].ChannelId.Should().Be(3UL);
    }

    [Fact]
    public async Task GetChannelRankingsAsync_WithNoSnapshots_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetChannelRankingsAsync(guildId, startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChannelRankingsAsync_FiltersCorrectlyByDateRange()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 10, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        // Snapshot within range
        var snapshotInRange = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 111UL,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 200,
            UniqueUsers = 10,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot outside range (too early)
        var snapshotTooEarly = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 222UL,
            ChannelName = "old",
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 300,
            UniqueUsers = 15,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot outside range (too late)
        var snapshotTooLate = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = 333UL,
            ChannelName = "future",
            PeriodStart = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 400,
            UniqueUsers = 20,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddRangeAsync(snapshotInRange, snapshotTooEarly, snapshotTooLate);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelRankingsAsync(guildId, startDate, endDate);

        // Assert
        result.Should().HaveCount(1);
        result[0].ChannelId.Should().Be(111UL);
        result[0].ChannelName.Should().Be("general");
    }

    [Fact]
    public async Task GetChannelTimeSeriesAsync_ReturnsOrderedSnapshots()
    {
        // Arrange
        var guildId = 123456789UL;
        var channelId = 111UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 5, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        var snapshot1 = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            UniqueUsers = 10,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 200,
            UniqueUsers = 15,
            AverageMessageLength = 55.0,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot3 = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 150,
            UniqueUsers = 12,
            AverageMessageLength = 52.0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetChannelTimeSeriesAsync(guildId, channelId, startDate, endDate, SnapshotGranularity.Daily);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(s => s.PeriodStart);
        result[0].MessageCount.Should().Be(100); // Jan 1
        result[1].MessageCount.Should().Be(150); // Jan 2
        result[2].MessageCount.Should().Be(200); // Jan 3
    }

    [Fact]
    public async Task GetChannelTimeSeriesAsync_FiltersCorrectlyByGranularity()
    {
        // Arrange
        var guildId = 123456789UL;
        var channelId = 111UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 5, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        var hourlySnapshot = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 14, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Hourly,
            MessageCount = 50,
            UniqueUsers = 5,
            AverageMessageLength = 45.0,
            CreatedAt = DateTime.UtcNow
        };

        var dailySnapshot = new ChannelActivitySnapshot
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 200,
            UniqueUsers = 15,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddRangeAsync(hourlySnapshot, dailySnapshot);
        await _context.SaveChangesAsync();

        // Act
        var resultDaily = await _repository.GetChannelTimeSeriesAsync(guildId, channelId, startDate, endDate, SnapshotGranularity.Daily);
        var resultHourly = await _repository.GetChannelTimeSeriesAsync(guildId, channelId, startDate, endDate, SnapshotGranularity.Hourly);

        // Assert
        resultDaily.Should().HaveCount(1);
        resultDaily[0].MessageCount.Should().Be(200);

        resultHourly.Should().HaveCount(1);
        resultHourly[0].MessageCount.Should().Be(50);
    }

    [Fact]
    public async Task GetChannelTimeSeriesAsync_WithNoData_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var channelId = 111UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetChannelTimeSeriesAsync(guildId, channelId, startDate, endDate, SnapshotGranularity.Daily);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_WithNewSnapshot_CreatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);

        var snapshot = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 111UL,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 250,
            UniqueUsers = 20,
            PeakHour = 14,
            PeakHourMessageCount = 75,
            AverageMessageLength = 48.5
        };

        // Act
        await _repository.UpsertAsync(snapshot);

        // Assert
        var saved = await _context.ChannelActivitySnapshots.FindAsync(snapshot.Id);
        saved.Should().NotBeNull();
        saved!.ChannelName.Should().Be("general");
        saved.MessageCount.Should().Be(250);
        saved.UniqueUsers.Should().Be(20);
        saved.PeakHour.Should().Be(14);
        saved.PeakHourMessageCount.Should().Be(75);
        saved.AverageMessageLength.Should().Be(48.5);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpsertAsync_WithExistingSnapshot_UpdatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);

        var existing = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 111UL,
            ChannelName = "general",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            UniqueUsers = 10,
            PeakHour = 12,
            PeakHourMessageCount = 30,
            AverageMessageLength = 40.0,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _context.ChannelActivitySnapshots.AddAsync(existing);
        await _context.SaveChangesAsync();

        var existingId = existing.Id;

        // Detach to simulate fresh update
        _context.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 111UL,
            ChannelName = "general-renamed",
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 300,
            UniqueUsers = 25,
            PeakHour = 15,
            PeakHourMessageCount = 90,
            AverageMessageLength = 52.3
        };

        // Act
        await _repository.UpsertAsync(updated);

        // Assert
        var saved = await _context.ChannelActivitySnapshots.FindAsync(existingId);
        saved.Should().NotBeNull();
        saved!.ChannelName.Should().Be("general-renamed");
        saved.MessageCount.Should().Be(300);
        saved.UniqueUsers.Should().Be(25);
        saved.PeakHour.Should().Be(15);
        saved.PeakHourMessageCount.Should().Be(90);
        saved.AverageMessageLength.Should().Be(52.3);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify only one record exists
        var count = _context.ChannelActivitySnapshots.Count();
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecordsInBatches()
    {
        // Arrange
        var cutoff = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(123456789UL);

        // Old records that should be deleted
        var oldSnapshot1 = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 111UL,
            ChannelName = "old-channel",
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            UniqueUsers = 5,
            AverageMessageLength = 40.0,
            CreatedAt = DateTime.UtcNow
        };

        var oldSnapshot2 = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 222UL,
            ChannelName = "another-old",
            PeriodStart = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            UniqueUsers = 8,
            AverageMessageLength = 45.0,
            CreatedAt = DateTime.UtcNow
        };

        // Recent record that should be kept
        var recentSnapshot = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 333UL,
            ChannelName = "recent-channel",
            PeriodStart = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            UniqueUsers = 10,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        // Hourly snapshot that should be kept (different granularity)
        var hourlySnapshot = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 444UL,
            ChannelName = "hourly-channel",
            PeriodStart = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Hourly,
            MessageCount = 10,
            UniqueUsers = 2,
            AverageMessageLength = 35.0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddRangeAsync(oldSnapshot1, oldSnapshot2, recentSnapshot, hourlySnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize: 1000);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = await _context.ChannelActivitySnapshots.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(s => s.ChannelId == 333UL); // Recent daily snapshot
        remaining.Should().Contain(s => s.ChannelId == 444UL && s.Granularity == SnapshotGranularity.Hourly); // Hourly snapshot
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_DeletesOnlyBatchSize()
    {
        // Arrange
        var cutoff = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(123456789UL);

        for (int i = 0; i < 5; i++)
        {
            var snapshot = new ChannelActivitySnapshot
            {
                GuildId = 123456789UL,
                ChannelId = (ulong)(100 + i),
                ChannelName = $"channel-{i}",
                PeriodStart = new DateTime(2024, 1, 1, i, 0, 0, DateTimeKind.Utc),
                Granularity = SnapshotGranularity.Daily,
                MessageCount = 10,
                UniqueUsers = 2,
                AverageMessageLength = 40.0,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ChannelActivitySnapshots.AddAsync(snapshot);
        }

        await _context.SaveChangesAsync();

        // Act - Delete only 2 at a time
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize: 2);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = _context.ChannelActivitySnapshots.Count();
        remaining.Should().Be(3);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldRecords_ReturnsZero()
    {
        // Arrange
        var cutoff = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(123456789UL);

        var recentSnapshot = new ChannelActivitySnapshot
        {
            GuildId = 123456789UL,
            ChannelId = 111UL,
            ChannelName = "recent",
            PeriodStart = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            UniqueUsers = 10,
            AverageMessageLength = 50.0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ChannelActivitySnapshots.AddAsync(recentSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize: 1000);

        // Assert
        deletedCount.Should().Be(0);

        var remaining = _context.ChannelActivitySnapshots.Count();
        remaining.Should().Be(1);
    }
}
