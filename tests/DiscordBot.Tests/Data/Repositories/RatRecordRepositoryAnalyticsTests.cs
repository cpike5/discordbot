using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for RatRecordRepository analytics methods.
/// Tests cover GetUserMetricsAsync and GetFunStatsAsync.
/// </summary>
public class RatRecordRepositoryAnalyticsTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly RatRecordRepository _repository;
    private readonly Mock<ILogger<RatRecordRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<RatRecord>>> _mockBaseLogger;

    public RatRecordRepositoryAnalyticsTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<RatRecordRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<RatRecord>>>();
        _repository = new RatRecordRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<Guild> SeedGuildAsync(ulong id = 123456789)
    {
        var guild = new Guild
        {
            Id = id,
            Name = $"Test Guild {id}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();
        return guild;
    }

    private async Task<RatWatch> CreateRatWatchAsync(
        ulong guildId,
        ulong accusedUserId,
        RatWatchStatus status,
        DateTime? createdAt = null,
        int guiltyVotes = 0,
        int notGuiltyVotes = 0)
    {
        var now = createdAt ?? DateTime.UtcNow;

        var watch = new RatWatch
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = 111111111,
            AccusedUserId = accusedUserId,
            InitiatorUserId = 333333333,
            OriginalMessageId = 444444444,
            ScheduledAt = now.AddHours(1),
            CreatedAt = now,
            Status = status
        };

        if (status == RatWatchStatus.ClearedEarly)
        {
            watch.ClearedAt = watch.CreatedAt.AddMinutes(30);
        }

        if (status == RatWatchStatus.Voting || status == RatWatchStatus.Guilty || status == RatWatchStatus.NotGuilty)
        {
            watch.VotingStartedAt = watch.ScheduledAt;
        }

        if (status == RatWatchStatus.Guilty || status == RatWatchStatus.NotGuilty)
        {
            watch.VotingEndedAt = watch.VotingStartedAt!.Value.AddMinutes(30);
        }

        await _context.RatWatches.AddAsync(watch);

        // Add votes if specified
        if (guiltyVotes > 0 || notGuiltyVotes > 0)
        {
            ulong voterIdBase = 500000000 + (ulong)Random.Shared.Next(1000000);
            for (int i = 0; i < guiltyVotes; i++)
            {
                await _context.RatVotes.AddAsync(new RatVote
                {
                    Id = Guid.NewGuid(),
                    RatWatchId = watch.Id,
                    VoterUserId = voterIdBase++,
                    IsGuiltyVote = true,
                    VotedAt = watch.VotingStartedAt!.Value.AddMinutes(5)
                });
            }

            for (int i = 0; i < notGuiltyVotes; i++)
            {
                await _context.RatVotes.AddAsync(new RatVote
                {
                    Id = Guid.NewGuid(),
                    RatWatchId = watch.Id,
                    VoterUserId = voterIdBase++,
                    IsGuiltyVote = false,
                    VotedAt = watch.VotingStartedAt!.Value.AddMinutes(5)
                });
            }
        }

        await _context.SaveChangesAsync();
        return watch;
    }

    #region GetUserMetricsAsync Tests

    [Fact]
    public async Task GetUserMetricsAsync_WithEmptyDataset_ReturnsEmptyList()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserMetricsAsync_CalculatesWatchesAgainst()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Pending);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        var metrics = result.Single();
        metrics.UserId.Should().Be(userId);
        metrics.WatchesAgainst.Should().Be(3, "total watches against this user");
    }

    [Fact]
    public async Task GetUserMetricsAsync_CalculatesGuiltyCount()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "guilty", 10);

        // Assert
        var metrics = result.Single();
        metrics.GuiltyCount.Should().Be(2, "two guilty verdicts");
    }

    [Fact]
    public async Task GetUserMetricsAsync_CalculatesEarlyCheckInCount()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "accountability", 10);

        // Assert
        var metrics = result.Single();
        metrics.EarlyCheckInCount.Should().Be(3, "three early check-ins");
    }

    [Fact]
    public async Task GetUserMetricsAsync_CalculatesAccountabilityScore()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        // 3 early check-ins out of 5 total watches = 60%
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "accountability", 10);

        // Assert
        var metrics = result.Single();
        metrics.AccountabilityScore.Should().BeApproximately(60.0, 0.01, "3 early out of 5 total = 60%");
    }

    [Fact]
    public async Task GetUserMetricsAsync_HandlesZeroWatches_AccountabilityScore()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // No watches exist, so no users to return
        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "accountability", 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserMetricsAsync_SetsLastIncidentDate()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;
        var now = DateTime.UtcNow;

        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, createdAt: now.AddDays(-5), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-2));
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Pending, createdAt: now); // Most recent

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        var metrics = result.Single();
        metrics.LastIncidentDate.Should().NotBeNull();
        metrics.LastIncidentDate!.Value.Should().BeCloseTo(now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetUserMetricsAsync_SortsByWatched()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var user3 = 100000003ul;

        // User1: 3 watches
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Pending);

        // User2: 5 watches (most)
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Pending);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // User3: 2 watches
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.ClearedEarly);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].UserId.Should().Be(user2, "user with most watches should be first");
        resultList[0].WatchesAgainst.Should().Be(5);
        resultList[1].UserId.Should().Be(user1);
        resultList[1].WatchesAgainst.Should().Be(3);
        resultList[2].UserId.Should().Be(user3);
        resultList[2].WatchesAgainst.Should().Be(2);
    }

    [Fact]
    public async Task GetUserMetricsAsync_SortsByGuilty()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var user3 = 100000003ul;

        // User1: 1 guilty
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly);

        // User2: 3 guilty (most)
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 4, notGuiltyVotes: 1);

        // User3: 0 guilty
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "guilty", 10);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].UserId.Should().Be(user2, "user with most guilty verdicts should be first");
        resultList[0].GuiltyCount.Should().Be(3);
        resultList[1].UserId.Should().Be(user1);
        resultList[1].GuiltyCount.Should().Be(1);
        resultList[2].UserId.Should().Be(user3);
        resultList[2].GuiltyCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUserMetricsAsync_SortsByAccountability()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var user3 = 100000003ul;

        // User1: 2/4 = 50%
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);

        // User2: 3/3 = 100% (highest)
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly);

        // User3: 0/3 = 0%
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "accountability", 10);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].UserId.Should().Be(user2, "user with highest accountability should be first");
        resultList[0].AccountabilityScore.Should().BeApproximately(100.0, 0.01);
        resultList[1].UserId.Should().Be(user1);
        resultList[1].AccountabilityScore.Should().BeApproximately(50.0, 0.01);
        resultList[2].UserId.Should().Be(user3);
        resultList[2].AccountabilityScore.Should().Be(0);
    }

    [Fact]
    public async Task GetUserMetricsAsync_DefaultsToWatchedSort_WithInvalidSortBy()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;

        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly);

        // Act - Use invalid sort parameter
        var result = await _repository.GetUserMetricsAsync(guild.Id, "invalid-sort", 10);

        // Assert - Should default to "watched" sorting
        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList[0].UserId.Should().Be(user2, "user with more watches should be first (default sort)");
        resultList[0].WatchesAgainst.Should().Be(2);
    }

    [Fact]
    public async Task GetUserMetricsAsync_RespectsLimit()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        for (ulong i = 1; i <= 15; i++)
        {
            await CreateRatWatchAsync(guild.Id, 100000000 + i, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        }

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        result.Should().HaveCount(10, "limit should restrict results");
    }

    [Fact]
    public async Task GetUserMetricsAsync_FiltersByGuildId()
    {
        // Arrange
        var guild1 = await SeedGuildAsync(111111111);
        var guild2 = await SeedGuildAsync(222222222);
        var userId = 333333333ul;

        await CreateRatWatchAsync(guild1.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild1.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild2.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild1.Id, "watched", 10);

        // Assert
        var metrics = result.Single();
        metrics.WatchesAgainst.Should().Be(2, "only guild1 watches should be counted");
    }

    [Fact]
    public async Task GetUserMetricsAsync_GroupsByUser()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;

        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);

        // Act
        var result = await _repository.GetUserMetricsAsync(guild.Id, "watched", 10);

        // Assert
        result.Should().HaveCount(2, "two distinct users");

        var user1Metrics = result.First(m => m.UserId == user1);
        user1Metrics.WatchesAgainst.Should().Be(2);

        var user2Metrics = result.First(m => m.UserId == user2);
        user2Metrics.WatchesAgainst.Should().Be(1);
    }

    #endregion

    #region GetFunStatsAsync Tests

    [Fact]
    public async Task GetFunStatsAsync_WithEmptyDataset_ReturnsNullStats()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.Should().NotBeNull();
        result.LongestGuiltyStreak.Should().BeNull();
        result.LongestCleanStreak.Should().BeNull();
        result.BiggestLandslide.Should().BeNull();
        result.ClosestCall.Should().BeNull();
        result.FastestCheckIn.Should().BeNull();
        result.LatestCheckIn.Should().BeNull();
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesBiggestLandslide()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        // Small margin: 3-2 = 1
        var watch1 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 2);

        // Biggest landslide: 10-1 = 9
        var watch2 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 10, notGuiltyVotes: 1);

        // Medium margin: 5-2 = 3
        var watch3 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.NotGuilty, guiltyVotes: 2, notGuiltyVotes: 5);

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.BiggestLandslide.Should().NotBeNull();
        result.BiggestLandslide!.WatchId.Should().Be(watch2.Id);
        result.BiggestLandslide.UserId.Should().Be(userId);
        result.BiggestLandslide.Description.Should().Contain("10-1");
        result.BiggestLandslide.Description.Should().Contain("Guilty");
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesClosestCall()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        // Large margin: 10-1 = 9
        var watch1 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 10, notGuiltyVotes: 1);

        // Closest guilty: 3-2 = 1
        var watch2 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 2);

        // Medium margin: 5-2 = 3
        var watch3 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);

        // Not guilty - should not count
        var watch4 = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.NotGuilty, guiltyVotes: 2, notGuiltyVotes: 3);

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.ClosestCall.Should().NotBeNull();
        result.ClosestCall!.WatchId.Should().Be(watch2.Id, "smallest margin among guilty verdicts");
        result.ClosestCall.UserId.Should().Be(userId);
        result.ClosestCall.Description.Should().Contain("3-2");
        result.ClosestCall.Description.Should().Contain("Guilty");
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesFastestCheckIn()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var user3 = 100000003ul;
        var now = DateTime.UtcNow;

        // Create watches with different clear times
        var watch1 = await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly, createdAt: now);
        watch1.ClearedAt = watch1.CreatedAt.AddHours(1); // 1 hour
        _context.RatWatches.Update(watch1);

        var watch2 = await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now);
        watch2.ClearedAt = watch2.CreatedAt.AddMinutes(5); // 5 minutes (fastest)
        _context.RatWatches.Update(watch2);

        var watch3 = await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.ClearedEarly, createdAt: now);
        watch3.ClearedAt = watch3.CreatedAt.AddMinutes(30); // 30 minutes
        _context.RatWatches.Update(watch3);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.FastestCheckIn.Should().NotBeNull();
        result.FastestCheckIn!.WatchId.Should().Be(watch2.Id, "watch with shortest duration");
        result.FastestCheckIn.UserId.Should().Be(user2);
        result.FastestCheckIn.Description.Should().Contain("5 minutes");
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesLatestCheckIn()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var user3 = 100000003ul;
        var now = DateTime.UtcNow;

        // Create watches scheduled for 2 hours from creation
        var watch1 = await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly, createdAt: now);
        watch1.ScheduledAt = watch1.CreatedAt.AddHours(2);
        watch1.ClearedAt = watch1.ScheduledAt.AddMinutes(-30); // 30 min before deadline
        _context.RatWatches.Update(watch1);

        var watch2 = await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now);
        watch2.ScheduledAt = watch2.CreatedAt.AddHours(2);
        watch2.ClearedAt = watch2.ScheduledAt.AddMinutes(-5); // 5 min before deadline (latest)
        _context.RatWatches.Update(watch2);

        var watch3 = await CreateRatWatchAsync(guild.Id, user3, RatWatchStatus.ClearedEarly, createdAt: now);
        watch3.ScheduledAt = watch3.CreatedAt.AddHours(2);
        watch3.ClearedAt = watch3.ScheduledAt.AddMinutes(-60); // 60 min before deadline
        _context.RatWatches.Update(watch3);

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.LatestCheckIn.Should().NotBeNull();
        result.LatestCheckIn!.WatchId.Should().Be(watch2.Id, "cleared closest to deadline");
        result.LatestCheckIn.UserId.Should().Be(user2);
        result.LatestCheckIn.Description.Should().Contain("5 minutes");
        result.LatestCheckIn.Description.Should().Contain("before deadline");
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesLongestGuiltyStreak()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var now = DateTime.UtcNow;

        // User1: Guilty, Guilty, Not Guilty, Guilty = max streak 2
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, createdAt: now.AddDays(-10), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, createdAt: now.AddDays(-9), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.NotGuilty, createdAt: now.AddDays(-8), guiltyVotes: 1, notGuiltyVotes: 4);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, createdAt: now.AddDays(-7), guiltyVotes: 5, notGuiltyVotes: 2);

        // User2: Guilty, Guilty, Guilty, Cleared = max streak 3 (longest)
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, createdAt: now.AddDays(-6), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, createdAt: now.AddDays(-5), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, createdAt: now.AddDays(-4), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-3));

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.LongestGuiltyStreak.Should().NotBeNull();
        result.LongestGuiltyStreak!.UserId.Should().Be(user2, "user with longest consecutive guilty verdicts");
        result.LongestGuiltyStreak.StreakCount.Should().Be(3);
    }

    [Fact]
    public async Task GetFunStatsAsync_CalculatesLongestCleanStreak()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var now = DateTime.UtcNow;

        // User1: Cleared, Cleared, Guilty, Cleared = max streak 2
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-10));
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-9));
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, createdAt: now.AddDays(-8), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-7));

        // User2: Cleared, Cleared, Cleared, Cleared = max streak 4 (longest)
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-6));
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-5));
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-4));
        await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-3));

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.LongestCleanStreak.Should().NotBeNull();
        result.LongestCleanStreak!.UserId.Should().Be(user2, "user with longest consecutive early check-ins");
        result.LongestCleanStreak.StreakCount.Should().Be(4);
    }

    [Fact]
    public async Task GetFunStatsAsync_IgnoresClearedEarly_ForVotingStats()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        // Only cleared early watches - should not have voting stats
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.BiggestLandslide.Should().BeNull("no voting watches");
        result.ClosestCall.Should().BeNull("no guilty verdicts");
    }

    [Fact]
    public async Task GetFunStatsAsync_IgnoresVoting_ForClearedEarlyStats()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        // Only voting watches - should not have cleared early stats
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.FastestCheckIn.Should().BeNull("no cleared early watches");
        result.LatestCheckIn.Should().BeNull("no cleared early watches");
    }

    [Fact]
    public async Task GetFunStatsAsync_HandlesNoClearedAtValue()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var userId = 222222222ul;

        var watch = await CreateRatWatchAsync(guild.Id, userId, RatWatchStatus.ClearedEarly);
        watch.ClearedAt = null; // Simulate corrupted data
        _context.RatWatches.Update(watch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert - Should handle gracefully
        result.FastestCheckIn.Should().BeNull("ClearedAt is null");
        result.LatestCheckIn.Should().BeNull("ClearedAt is null");
    }

    [Fact]
    public async Task GetFunStatsAsync_FiltersByGuildId()
    {
        // Arrange
        var guild1 = await SeedGuildAsync(111111111);
        var guild2 = await SeedGuildAsync(222222222);
        var user1 = 100000001ul;
        var user2 = 100000002ul;

        // Guild1 - biggest landslide 10-1
        var watch1 = await CreateRatWatchAsync(guild1.Id, user1, RatWatchStatus.Guilty, guiltyVotes: 10, notGuiltyVotes: 1);

        // Guild2 - biggest landslide 5-1 (different)
        var watch2 = await CreateRatWatchAsync(guild2.Id, user2, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 1);

        // Act
        var result = await _repository.GetFunStatsAsync(guild1.Id);

        // Assert
        result.BiggestLandslide.Should().NotBeNull();
        result.BiggestLandslide!.WatchId.Should().Be(watch1.Id, "only guild1 watches should be counted");
        result.BiggestLandslide.Description.Should().Contain("10-1");
    }

    [Fact]
    public async Task GetFunStatsAsync_HandlesTieBreaking()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var user1 = 100000001ul;
        var user2 = 100000002ul;
        var now = DateTime.UtcNow;

        // Two watches with same margin - should pick first one encountered
        var watch1 = await CreateRatWatchAsync(guild.Id, user1, RatWatchStatus.Guilty, createdAt: now.AddDays(-2), guiltyVotes: 5, notGuiltyVotes: 2);
        var watch2 = await CreateRatWatchAsync(guild.Id, user2, RatWatchStatus.Guilty, createdAt: now.AddDays(-1), guiltyVotes: 5, notGuiltyVotes: 2);

        // Act
        var result = await _repository.GetFunStatsAsync(guild.Id);

        // Assert
        result.BiggestLandslide.Should().NotBeNull();
        // Either watch is acceptable for a tie
        result.BiggestLandslide!.Description.Should().Contain("5-2");
    }

    #endregion
}
