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
/// Unit tests for RatWatchRepository analytics methods.
/// Tests cover GetAnalyticsSummaryAsync, GetTimeSeriesAsync, and GetActivityHeatmapAsync.
/// </summary>
public class RatWatchRepositoryAnalyticsTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly RatWatchRepository _repository;
    private readonly Mock<ILogger<RatWatchRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<RatWatch>>> _mockBaseLogger;

    public RatWatchRepositoryAnalyticsTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<RatWatchRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<RatWatch>>>();
        _repository = new RatWatchRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
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
        RatWatchStatus status,
        DateTime? createdAt = null,
        DateTime? scheduledAt = null,
        int guiltyVotes = 0,
        int notGuiltyVotes = 0)
    {
        var watch = new RatWatch
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = 111111111,
            AccusedUserId = 222222222,
            InitiatorUserId = 333333333,
            OriginalMessageId = 444444444,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow.AddHours(1),
            CreatedAt = createdAt ?? DateTime.UtcNow,
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
            ulong voterIdBase = 500000000;
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

    #region GetAnalyticsSummaryAsync Tests

    [Fact]
    public async Task GetAnalyticsSummaryAsync_WithEmptyDataset_ReturnsZeros()
    {
        // Arrange
        await SeedGuildAsync();

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.Should().NotBeNull();
        result.TotalWatches.Should().Be(0);
        result.ActiveWatches.Should().Be(0);
        result.GuiltyCount.Should().Be(0);
        result.ClearedEarlyCount.Should().Be(0);
        result.GuiltyRate.Should().Be(0);
        result.EarlyCheckInRate.Should().Be(0);
        result.AvgVotingParticipation.Should().Be(0);
        result.AvgVoteMargin.Should().Be(0);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CountsTotalWatches()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.TotalWatches.Should().Be(3);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CountsActiveWatches()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Voting, guiltyVotes: 3, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.ActiveWatches.Should().Be(2, "Pending and Voting are active statuses");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CountsGuiltyVerdicts()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.GuiltyCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CountsClearedEarly()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.ClearedEarlyCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CalculatesGuiltyRate()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // 3 guilty, 1 not guilty, 1 cleared early = 5 completed, 3 guilty = 60%
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 4, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending); // Not completed, should not count

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.GuiltyRate.Should().BeApproximately(60.0, 0.01, "3 guilty out of 5 completed = 60%");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CalculatesEarlyCheckInRate()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // 2 cleared early, 3 guilty verdicts = 5 completed, 2 early = 40%
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 4, notGuiltyVotes: 1);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.EarlyCheckInRate.Should().BeApproximately(40.0, 0.01, "2 cleared early out of 5 completed = 40%");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CalculatesAvgVotingParticipation()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // Watch 1: 7 votes (5 guilty + 2 not guilty)
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        // Watch 2: 4 votes (3 guilty + 1 not guilty)
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        // Watch 3: 5 votes (1 guilty + 4 not guilty)
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);
        // Cleared early - should not count
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);

        // Average = (7 + 4 + 5) / 3 = 5.33
        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.AvgVotingParticipation.Should().BeApproximately(5.33, 0.01, "average of 7, 4, and 5 votes");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_CalculatesAvgVoteMargin()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // Watch 1: 5 guilty, 2 not guilty -> margin = |5-2| = 3
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        // Watch 2: 3 guilty, 1 not guilty -> margin = |3-1| = 2
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        // Watch 3: 1 guilty, 4 not guilty -> margin = |1-4| = 3
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.NotGuilty, guiltyVotes: 1, notGuiltyVotes: 4);

        // Average margin = (3 + 2 + 3) / 3 = 2.67
        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.AvgVoteMargin.Should().BeApproximately(2.67, 0.01, "average margin of 3, 2, and 3");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_FiltersByGuildId()
    {
        // Arrange
        var guild1 = await SeedGuildAsync(111111111);
        var guild2 = await SeedGuildAsync(222222222);

        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.Guilty, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild2.Id, RatWatchStatus.Guilty, guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild2.Id, RatWatchStatus.Pending);

        // Act - Filter by guild1
        var result = await _repository.GetAnalyticsSummaryAsync(guild1.Id, null, null);

        // Assert
        result.TotalWatches.Should().Be(2, "only guild1 watches should be counted");
        result.GuiltyCount.Should().Be(1);
        result.ClearedEarlyCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_FiltersByDateRange()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow;

        // Old watch - outside range
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-10), guiltyVotes: 5, notGuiltyVotes: 2);

        // In range
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-2));

        // Future watch - outside range
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, createdAt: now.AddDays(1));

        // Act - Filter last 7 days
        var result = await _repository.GetAnalyticsSummaryAsync(null, now.AddDays(-7), now);

        // Assert
        result.TotalWatches.Should().Be(2, "only watches within date range should be counted");
        result.GuiltyCount.Should().Be(1);
        result.ClearedEarlyCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_WithOnlyStartDate_FiltersCorrectly()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-10), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now);

        // Act - Only start date, no end date
        var result = await _repository.GetAnalyticsSummaryAsync(null, now.AddDays(-5), null);

        // Assert
        result.TotalWatches.Should().Be(2, "watches after start date should be counted");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_WithOnlyEndDate_FiltersCorrectly()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-10), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now);

        // Act - Only end date, no start date
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, now.AddDays(-5));

        // Assert
        result.TotalWatches.Should().Be(1, "only watches before end date should be counted");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_HandlesZeroDivision_WhenNoCompletedWatches()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Voting, guiltyVotes: 3, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Cancelled);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.GuiltyRate.Should().Be(0, "no completed watches, rate should be 0");
        result.EarlyCheckInRate.Should().Be(0, "no completed watches, rate should be 0");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_HandlesZeroDivision_WhenNoVotingWatches()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending);

        // Act
        var result = await _repository.GetAnalyticsSummaryAsync(null, null, null);

        // Assert
        result.AvgVotingParticipation.Should().Be(0, "no voting watches, average should be 0");
        result.AvgVoteMargin.Should().Be(0, "no voting watches, average should be 0");
    }

    #endregion

    #region GetTimeSeriesAsync Tests

    [Fact]
    public async Task GetTimeSeriesAsync_WithEmptyDataset_ReturnsEmptyList()
    {
        // Arrange
        await SeedGuildAsync();
        var now = DateTime.UtcNow;

        // Act
        var result = await _repository.GetTimeSeriesAsync(null, now.AddDays(-7), now);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimeSeriesAsync_GroupsByDate()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow.Date; // Use .Date to normalize to midnight

        // Create watches on different dates
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3).AddHours(5), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-2));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, createdAt: now.AddDays(-1));

        // Act
        var result = await _repository.GetTimeSeriesAsync(null, now.AddDays(-7), now);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3, "watches on 3 different dates");

        // Day -3 should have 2 watches
        var day3Data = resultList.FirstOrDefault(r => r.Date == now.AddDays(-3));
        day3Data.Should().NotBeNull();
        day3Data!.TotalCount.Should().Be(2);
        day3Data.GuiltyCount.Should().Be(2);
        day3Data.ClearedCount.Should().Be(0);

        // Day -2 should have 1 watch
        var day2Data = resultList.FirstOrDefault(r => r.Date == now.AddDays(-2));
        day2Data.Should().NotBeNull();
        day2Data!.TotalCount.Should().Be(1);
        day2Data.ClearedCount.Should().Be(1);

        // Day -1 should have 1 watch
        var day1Data = resultList.FirstOrDefault(r => r.Date == now.AddDays(-1));
        day1Data.Should().NotBeNull();
        day1Data!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTimeSeriesAsync_CountsGuiltyAndClearedSeparately()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var targetDate = DateTime.UtcNow.Date;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: targetDate, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: targetDate.AddHours(2), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: targetDate.AddHours(4));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.NotGuilty, createdAt: targetDate.AddHours(6), guiltyVotes: 1, notGuiltyVotes: 4);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, createdAt: targetDate.AddHours(8));

        // Act
        var result = await _repository.GetTimeSeriesAsync(null, targetDate, targetDate.AddDays(1));

        // Assert
        var data = result.Single();
        data.Date.Should().Be(targetDate);
        data.TotalCount.Should().Be(5);
        data.GuiltyCount.Should().Be(2, "only Guilty status counts");
        data.ClearedCount.Should().Be(1, "only ClearedEarly status counts");
    }

    [Fact]
    public async Task GetTimeSeriesAsync_OrdersByDateAscending()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow.Date;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-5), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-2), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-4));

        // Act
        var result = await _repository.GetTimeSeriesAsync(null, now.AddDays(-7), now);

        // Assert
        var resultList = result.ToList();
        resultList.Should().BeInAscendingOrder(r => r.Date);
        resultList[0].Date.Should().Be(now.AddDays(-5));
        resultList[1].Date.Should().Be(now.AddDays(-4));
        resultList[2].Date.Should().Be(now.AddDays(-2));
    }

    [Fact]
    public async Task GetTimeSeriesAsync_FiltersByGuildId()
    {
        // Arrange
        var guild1 = await SeedGuildAsync(111111111);
        var guild2 = await SeedGuildAsync(222222222);
        var now = DateTime.UtcNow.Date;

        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.Guilty, createdAt: now, guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.ClearedEarly, createdAt: now);
        await CreateRatWatchAsync(guild2.Id, RatWatchStatus.Guilty, createdAt: now, guiltyVotes: 3, notGuiltyVotes: 1);

        // Act - Filter by guild1
        var result = await _repository.GetTimeSeriesAsync(guild1.Id, now, now.AddDays(1));

        // Assert
        var data = result.Single();
        data.TotalCount.Should().Be(2, "only guild1 watches should be counted");
        data.GuiltyCount.Should().Be(1);
        data.ClearedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTimeSeriesAsync_FiltersByDateRange()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow.Date;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-10), guiltyVotes: 5, notGuiltyVotes: 2);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Guilty, createdAt: now.AddDays(-3), guiltyVotes: 3, notGuiltyVotes: 1);
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.ClearedEarly, createdAt: now.AddDays(-2));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, createdAt: now.AddDays(1));

        // Act - Get last 7 days
        var result = await _repository.GetTimeSeriesAsync(null, now.AddDays(-7), now);

        // Assert
        result.Should().HaveCount(2, "only watches within date range");
    }

    #endregion

    #region GetActivityHeatmapAsync Tests

    [Fact]
    public async Task GetActivityHeatmapAsync_WithEmptyDataset_ReturnsEmptyList()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow;

        // Act
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, now.AddDays(-7), now);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_GroupsByDayAndHour()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Monday

        // Create watches at different times
        // Monday 10:00
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddHours(10));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddHours(10).AddMinutes(30));

        // Tuesday 14:00
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddDays(1).AddHours(14));

        // Monday 10:00 (same as first group)
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddDays(7).AddHours(10));

        // Act
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, baseDate, baseDate.AddDays(14));

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(2, "two unique day/hour combinations");

        // Monday (1) at hour 10 should have 3 watches
        var mondayData = resultList.FirstOrDefault(r => r.DayOfWeek == 1 && r.Hour == 10);
        mondayData.Should().NotBeNull();
        mondayData!.Count.Should().Be(3);

        // Tuesday (2) at hour 14 should have 1 watch
        var tuesdayData = resultList.FirstOrDefault(r => r.DayOfWeek == 2 && r.Hour == 14);
        tuesdayData.Should().NotBeNull();
        tuesdayData!.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_UsesScheduledAtNotCreatedAt()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var createdDate = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var scheduledDate = new DateTime(2024, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        await CreateRatWatchAsync(
            guild.Id,
            RatWatchStatus.Pending,
            createdAt: createdDate,
            scheduledAt: scheduledDate);

        // Act
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, scheduledDate.AddDays(-1), scheduledDate.AddDays(1));

        // Assert
        var data = result.Single();
        data.Hour.Should().Be(14, "should use ScheduledAt hour, not CreatedAt hour");
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_OrdersByDayAndHour()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Create in non-sequential order
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddDays(3).AddHours(15)); // Thursday 15:00
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddHours(8)); // Monday 8:00
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddHours(14)); // Monday 14:00

        // Act
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, baseDate, baseDate.AddDays(7));

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);

        // Should be ordered by day, then hour
        resultList[0].DayOfWeek.Should().Be(1); // Monday
        resultList[0].Hour.Should().Be(8);

        resultList[1].DayOfWeek.Should().Be(1); // Monday
        resultList[1].Hour.Should().Be(14);

        resultList[2].DayOfWeek.Should().Be(4); // Thursday
        resultList[2].Hour.Should().Be(15);
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_FiltersByGuildId()
    {
        // Arrange
        var guild1 = await SeedGuildAsync(111111111);
        var guild2 = await SeedGuildAsync(222222222);
        var baseDate = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.Pending, scheduledAt: baseDate);
        await CreateRatWatchAsync(guild1.Id, RatWatchStatus.Pending, scheduledAt: baseDate.AddMinutes(30));
        await CreateRatWatchAsync(guild2.Id, RatWatchStatus.Pending, scheduledAt: baseDate);

        // Act - Filter by guild1
        var result = await _repository.GetActivityHeatmapAsync(guild1.Id, baseDate.AddDays(-1), baseDate.AddDays(1));

        // Assert
        var data = result.Single();
        data.Count.Should().Be(2, "only guild1 watches should be counted");
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_FiltersByDateRange()
    {
        // Arrange
        var guild = await SeedGuildAsync();
        var now = DateTime.UtcNow;

        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: now.AddDays(-10));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: now.AddDays(-3));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: now.AddDays(-2));
        await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: now.AddDays(1));

        // Act - Get last 7 days
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, now.AddDays(-7), now);

        // Assert
        result.Should().HaveCount(2, "only watches within date range");
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_HandlesDayOfWeekValues()
    {
        // Arrange
        var guild = await SeedGuildAsync();

        // Create watches for each day of the week
        var sunday = new DateTime(2024, 1, 7, 10, 0, 0, DateTimeKind.Utc); // Sunday

        for (int i = 0; i < 7; i++)
        {
            await CreateRatWatchAsync(guild.Id, RatWatchStatus.Pending, scheduledAt: sunday.AddDays(i).AddHours(10));
        }

        // Act
        var result = await _repository.GetActivityHeatmapAsync(guild.Id, sunday, sunday.AddDays(7));

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(7, "one entry for each day of the week");

        // Verify day of week values (0=Sunday through 6=Saturday)
        resultList.Should().Contain(r => r.DayOfWeek == 0); // Sunday
        resultList.Should().Contain(r => r.DayOfWeek == 1); // Monday
        resultList.Should().Contain(r => r.DayOfWeek == 2); // Tuesday
        resultList.Should().Contain(r => r.DayOfWeek == 3); // Wednesday
        resultList.Should().Contain(r => r.DayOfWeek == 4); // Thursday
        resultList.Should().Contain(r => r.DayOfWeek == 5); // Friday
        resultList.Should().Contain(r => r.DayOfWeek == 6); // Saturday
    }

    #endregion
}
