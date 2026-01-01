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
/// Unit tests for MemberActivityRepository.
/// </summary>
public class MemberActivityRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly MemberActivityRepository _repository;
    private readonly Mock<ILogger<MemberActivityRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<MemberActivitySnapshot>>> _mockBaseLogger;

    public MemberActivityRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<MemberActivityRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<MemberActivitySnapshot>>>();
        _repository = new MemberActivityRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
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

    /// <summary>
    /// Helper method to create and save a User entity for testing.
    /// </summary>
    private async Task CreateTestUserAsync(ulong userId)
    {
        var user = new User
        {
            Id = userId,
            Username = $"TestUser{userId}",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByMemberAsync_WithExistingSnapshots_ReturnsFilteredSnapshots()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);
        await CreateTestUserAsync(userId);

        var snapshot1 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = userId,
            PeriodStart = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            UniqueChannelsActive = 3,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = userId,
            PeriodStart = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            UniqueChannelsActive = 5,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot outside date range
        var snapshot3 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = userId,
            PeriodStart = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            UniqueChannelsActive = 2,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByMemberAsync(guildId, userId, startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.MessageCount == 50);
        result.Should().Contain(s => s.MessageCount == 75);
        result.Should().NotContain(s => s.MessageCount == 100);
        result.Should().BeInAscendingOrder(s => s.PeriodStart);
    }

    [Fact]
    public async Task GetByMemberAsync_WithGranularityFilter_ReturnsOnlyMatchingGranularity()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);
        await CreateTestUserAsync(userId);

        var hourlySnapshot = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = userId,
            PeriodStart = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Hourly,
            MessageCount = 10,
            CreatedAt = DateTime.UtcNow
        };

        var dailySnapshot = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = userId,
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(hourlySnapshot, dailySnapshot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByMemberAsync(guildId, userId, startDate, endDate, SnapshotGranularity.Daily);

        // Assert
        result.Should().HaveCount(1);
        result.First().Granularity.Should().Be(SnapshotGranularity.Daily);
        result.First().MessageCount.Should().Be(50);
    }

    [Fact]
    public async Task GetByMemberAsync_WithNoSnapshots_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var userId = 987654321UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetByMemberAsync(guildId, userId, startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopActiveMembersAsync_ReturnsCorrectlyOrderedResults()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        var user1 = new User
        {
            Id = 111UL,
            Username = "User1",
            Discriminator = "0001",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        var user2 = new User
        {
            Id = 222UL,
            Username = "User2",
            Discriminator = "0002",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        var user3 = new User
        {
            Id = 333UL,
            Username = "User3",
            Discriminator = "0003",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Users.AddRangeAsync(user1, user2, user3);
        await _context.SaveChangesAsync();

        // User1: 150 total messages (50 + 100)
        var snapshot1a = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = user1.Id,
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot1b = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = user1.Id,
            PeriodStart = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            CreatedAt = DateTime.UtcNow
        };

        // User2: 200 total messages
        var snapshot2 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = user2.Id,
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 200,
            CreatedAt = DateTime.UtcNow
        };

        // User3: 75 total messages
        var snapshot3 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = user3.Id,
            PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(snapshot1a, snapshot1b, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTopActiveMembersAsync(guildId, startDate, endDate, limit: 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].User.Should().NotBeNull();
        result[0].User!.Id.Should().Be(user2.Id); // Most active with 200 messages
        result[1].UserId.Should().Be(user1.Id); // Second with 150 messages
        result[2].UserId.Should().Be(user3.Id); // Third with 75 messages
    }

    [Fact]
    public async Task GetTopActiveMembersAsync_WithLimit_ReturnsOnlyTopN()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);

        for (int i = 1; i <= 5; i++)
        {
            var user = new User
            {
                Id = (ulong)i,
                Username = $"User{i}",
                Discriminator = $"{i:D4}",
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            await _context.Users.AddAsync(user);

            var snapshot = new MemberActivitySnapshot
            {
                GuildId = guildId,
                UserId = (ulong)i,
                PeriodStart = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Granularity = SnapshotGranularity.Daily,
                MessageCount = i * 10,
                CreatedAt = DateTime.UtcNow
            };
            await _context.MemberActivitySnapshots.AddAsync(snapshot);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTopActiveMembersAsync(guildId, startDate, endDate, limit: 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(5UL); // Highest message count
        result[1].UserId.Should().Be(4UL);
        result[2].UserId.Should().Be(3UL);
    }

    [Fact]
    public async Task GetActivityTimeSeriesAsync_GroupsDataCorrectly()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 3, 23, 59, 59, DateTimeKind.Utc);
        var period1 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var period2 = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(guildId);
        await CreateTestUserAsync(111UL);
        await CreateTestUserAsync(222UL);
        await CreateTestUserAsync(333UL);

        // Period 1: 2 users, 150 total messages
        var snapshot1a = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 111UL,
            PeriodStart = period1,
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot1b = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 222UL,
            PeriodStart = period1,
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        // Period 2: 1 user, 75 total messages
        var snapshot2 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 333UL,
            PeriodStart = period2,
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(snapshot1a, snapshot1b, snapshot2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActivityTimeSeriesAsync(guildId, startDate, endDate, SnapshotGranularity.Daily);

        // Assert
        result.Should().HaveCount(2);

        var firstPeriod = result.First(r => r.Period == period1);
        firstPeriod.TotalMessages.Should().Be(150);
        firstPeriod.ActiveMembers.Should().Be(2);

        var secondPeriod = result.First(r => r.Period == period2);
        secondPeriod.TotalMessages.Should().Be(75);
        secondPeriod.ActiveMembers.Should().Be(1);

        result.Should().BeInAscendingOrder(r => r.Period);
    }

    [Fact]
    public async Task GetActivityTimeSeriesAsync_WithNoData_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetActivityTimeSeriesAsync(guildId, startDate, endDate, SnapshotGranularity.Daily);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_WithNewSnapshot_CreatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);
        await CreateTestUserAsync(987654321UL);

        var snapshot = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 987654321UL,
            PeriodStart = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            ReactionCount = 20,
            VoiceMinutes = 30,
            UniqueChannelsActive = 5
        };

        // Act
        await _repository.UpsertAsync(snapshot);

        // Assert
        var saved = await _context.MemberActivitySnapshots.FindAsync(snapshot.Id);
        saved.Should().NotBeNull();
        saved!.MessageCount.Should().Be(100);
        saved.ReactionCount.Should().Be(20);
        saved.VoiceMinutes.Should().Be(30);
        saved.UniqueChannelsActive.Should().Be(5);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpsertAsync_WithExistingSnapshot_UpdatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);
        await CreateTestUserAsync(987654321UL);

        var existing = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 987654321UL,
            PeriodStart = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            ReactionCount = 10,
            VoiceMinutes = 15,
            UniqueChannelsActive = 2,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _context.MemberActivitySnapshots.AddAsync(existing);
        await _context.SaveChangesAsync();

        var existingId = existing.Id;

        // Detach to simulate fresh update
        _context.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 987654321UL,
            PeriodStart = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            ReactionCount = 25,
            VoiceMinutes = 40,
            UniqueChannelsActive = 6
        };

        // Act
        await _repository.UpsertAsync(updated);

        // Assert
        var saved = await _context.MemberActivitySnapshots.FindAsync(existingId);
        saved.Should().NotBeNull();
        saved!.MessageCount.Should().Be(100);
        saved.ReactionCount.Should().Be(25);
        saved.VoiceMinutes.Should().Be(40);
        saved.UniqueChannelsActive.Should().Be(6);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify only one record exists
        var count = _context.MemberActivitySnapshots.Count();
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecordsInBatches()
    {
        // Arrange
        var cutoff = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(123456789UL);
        await CreateTestUserAsync(111UL);
        await CreateTestUserAsync(222UL);
        await CreateTestUserAsync(333UL);
        await CreateTestUserAsync(444UL);

        // Old records that should be deleted
        var oldSnapshot1 = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 111UL,
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        var oldSnapshot2 = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 222UL,
            PeriodStart = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            CreatedAt = DateTime.UtcNow
        };

        // Recent record that should be kept
        var recentSnapshot = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 333UL,
            PeriodStart = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            CreatedAt = DateTime.UtcNow
        };

        // Hourly snapshot that should be kept (different granularity)
        var hourlySnapshot = new MemberActivitySnapshot
        {
            GuildId = 123456789UL,
            UserId = 444UL,
            PeriodStart = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Hourly,
            MessageCount = 10,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(oldSnapshot1, oldSnapshot2, recentSnapshot, hourlySnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize: 1000);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = await _context.MemberActivitySnapshots.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(s => s.UserId == 333UL); // Recent daily snapshot
        remaining.Should().Contain(s => s.UserId == 444UL && s.Granularity == SnapshotGranularity.Hourly); // Hourly snapshot
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_DeletesOnlyBatchSize()
    {
        // Arrange
        var cutoff = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateTestGuildAsync(123456789UL);
        for (int i = 0; i < 5; i++)
        {
            await CreateTestUserAsync((ulong)(100 + i));
        }

        for (int i = 0; i < 5; i++)
        {
            var snapshot = new MemberActivitySnapshot
            {
                GuildId = 123456789UL,
                UserId = (ulong)(100 + i),
                PeriodStart = new DateTime(2024, 1, 1, i, 0, 0, DateTimeKind.Utc),
                Granularity = SnapshotGranularity.Daily,
                MessageCount = 10,
                CreatedAt = DateTime.UtcNow
            };
            await _context.MemberActivitySnapshots.AddAsync(snapshot);
        }

        await _context.SaveChangesAsync();

        // Act - Delete only 2 at a time
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize: 2);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = _context.MemberActivitySnapshots.Count();
        remaining.Should().Be(3);
    }

    [Fact]
    public async Task GetLastSnapshotTimeAsync_WithSnapshots_ReturnsLatestTime()
    {
        // Arrange
        var guildId = 123456789UL;

        await CreateTestGuildAsync(guildId);
        await CreateTestUserAsync(111UL);
        await CreateTestUserAsync(222UL);
        await CreateTestUserAsync(333UL);

        var snapshot1 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 111UL,
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 222UL,
            PeriodStart = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot3 = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 333UL,
            PeriodStart = new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 100,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var lastTime = await _repository.GetLastSnapshotTimeAsync(guildId, SnapshotGranularity.Daily);

        // Assert
        lastTime.Should().NotBeNull();
        lastTime.Should().Be(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetLastSnapshotTimeAsync_WithDifferentGranularities_ReturnsCorrectGranularity()
    {
        // Arrange
        var guildId = 123456789UL;

        await CreateTestGuildAsync(guildId);
        await CreateTestUserAsync(111UL);
        await CreateTestUserAsync(222UL);

        var hourlySnapshot = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 111UL,
            PeriodStart = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Hourly,
            MessageCount = 10,
            CreatedAt = DateTime.UtcNow
        };

        var dailySnapshot = new MemberActivitySnapshot
        {
            GuildId = guildId,
            UserId = 222UL,
            PeriodStart = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Granularity = SnapshotGranularity.Daily,
            MessageCount = 75,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MemberActivitySnapshots.AddRangeAsync(hourlySnapshot, dailySnapshot);
        await _context.SaveChangesAsync();

        // Act
        var lastHourly = await _repository.GetLastSnapshotTimeAsync(guildId, SnapshotGranularity.Hourly);
        var lastDaily = await _repository.GetLastSnapshotTimeAsync(guildId, SnapshotGranularity.Daily);

        // Assert
        lastHourly.Should().Be(new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc));
        lastDaily.Should().Be(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetLastSnapshotTimeAsync_WithNoSnapshots_ReturnsNull()
    {
        // Arrange
        var guildId = 123456789UL;

        // Act
        var lastTime = await _repository.GetLastSnapshotTimeAsync(guildId, SnapshotGranularity.Daily);

        // Assert
        lastTime.Should().BeNull();
    }
}
