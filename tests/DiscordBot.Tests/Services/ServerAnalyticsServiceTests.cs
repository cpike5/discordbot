using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ServerAnalyticsService"/>.
/// </summary>
public class ServerAnalyticsServiceTests
{
    private readonly Mock<IMemberActivityRepository> _mockMemberActivityRepository;
    private readonly Mock<IChannelActivityRepository> _mockChannelActivityRepository;
    private readonly Mock<IGuildMetricsRepository> _mockGuildMetricsRepository;
    private readonly Mock<IMessageLogRepository> _mockMessageLogRepository;
    private readonly Mock<ILogger<ServerAnalyticsService>> _mockLogger;
    private readonly ServerAnalyticsService _service;

    public ServerAnalyticsServiceTests()
    {
        _mockMemberActivityRepository = new Mock<IMemberActivityRepository>();
        _mockChannelActivityRepository = new Mock<IChannelActivityRepository>();
        _mockGuildMetricsRepository = new Mock<IGuildMetricsRepository>();
        _mockMessageLogRepository = new Mock<IMessageLogRepository>();
        _mockLogger = new Mock<ILogger<ServerAnalyticsService>>();
        _service = new ServerAnalyticsService(
            _mockMemberActivityRepository.Object,
            _mockChannelActivityRepository.Object,
            _mockGuildMetricsRepository.Object,
            _mockMessageLogRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCorrectSummaryMetrics()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        var latestMetrics = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = DateOnly.FromDateTime(now),
            TotalMembers = 500,
            MembersJoined = 10,
            MembersLeft = 5
        };

        var activitySnapshots = new List<(DateTime Period, int TotalMessages, int ActiveMembers)>
        {
            (now.AddDays(-1), 1000, 100),
            (now.AddDays(-5), 800, 80),
            (now.AddDays(-15), 900, 90)
        };

        var metricsLast7d = new List<GuildMetricsSnapshot>
        {
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(now.AddDays(-7)), TotalMembers = 480 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(now), TotalMembers = 500 }
        };

        var channelSnapshots = new List<ChannelActivitySnapshot>
        {
            new() { ChannelId = 111, ChannelName = "general", MessageCount = 500 },
            new() { ChannelId = 222, ChannelName = "bot-commands", MessageCount = 300 }
        };

        // Message counts from MessageLogs (source of truth for message counts)
        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(now.AddDays(-1)), 150),
            (DateOnly.FromDateTime(now.AddDays(-5)), 100)
        };

        _mockGuildMetricsRepository
            .Setup(r => r.GetLatestAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestMetrics);

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                SnapshotGranularity.Daily, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activitySnapshots);

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metricsLast7d);

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelSnapshots);

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalMembers.Should().Be(500);
        result.OnlineMembers.Should().Be(0, "online member count is not tracked in snapshots");
        result.ActiveChannels.Should().Be(2, "there are 2 active channels");
        result.MemberGrowth7d.Should().Be(20, "500 - 480 = 20 new members");
        result.MemberGrowthPercent.Should().BeApproximately(4.17m, 0.1m, "(20 / 480) * 100 = 4.17%");
        result.Messages24h.Should().Be(150, "message from yesterday should be counted");
        result.Messages7d.Should().Be(250, "both messages from last 7 days should be counted");
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoMetrics_ShouldReturnZeroValues()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockGuildMetricsRepository
            .Setup(r => r.GetLatestAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMetricsSnapshot?)null);

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                SnapshotGranularity.Daily, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateTime Period, int TotalMessages, int ActiveMembers)>());

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelActivitySnapshot>());

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalMembers.Should().Be(0);
        result.ActiveMembers24h.Should().Be(0);
        result.ActiveMembers7d.Should().Be(0);
        result.ActiveMembers30d.Should().Be(0);
        result.Messages24h.Should().Be(0);
        result.Messages7d.Should().Be(0);
        result.MemberGrowth7d.Should().Be(0);
        result.MemberGrowthPercent.Should().Be(0);
        result.ActiveChannels.Should().Be(0);
    }

    [Fact]
    public async Task GetActivityTimeSeriesAsync_ShouldReturnDataFromRepository()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var activitySnapshots = new List<(DateTime Period, int TotalMessages, int ActiveMembers)>
        {
            (start, 1000, 100),
            (start.AddDays(1), 1200, 120),
            (start.AddDays(2), 1100, 110)
        };

        var channelSnapshots = new List<ChannelActivitySnapshot>
        {
            new() { ChannelId = 111, ChannelName = "general", MessageCount = 500 },
            new() { ChannelId = 222, ChannelName = "bot-commands", MessageCount = 300 }
        };

        // Message counts from MessageLogs (source of truth)
        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(start), 1500),
            (DateOnly.FromDateTime(start.AddDays(1)), 1800),
            (DateOnly.FromDateTime(start.AddDays(2)), 1600)
        };

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, start, end, SnapshotGranularity.Daily,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activitySnapshots);

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelSnapshots);

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetActivityTimeSeriesAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 activity snapshots");
        result[0].Date.Should().Be(start);
        result[0].MessageCount.Should().Be(1500, "message count should come from MessageLogs");
        result[0].ActiveMembers.Should().Be(100);
        result[0].ActiveChannels.Should().Be(2, "total unique channels in the period");
    }

    [Fact]
    public async Task GetActivityTimeSeriesAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, start, end, SnapshotGranularity.Daily,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateTime Period, int TotalMessages, int ActiveMembers)>());

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelActivitySnapshot>());

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        // Act
        var result = await _service.GetActivityTimeSeriesAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no activity data is available");
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_ShouldGroupByDayAndHour()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Create hourly snapshots: Monday 9am, Monday 10am, Tuesday 9am
        var hourlySnapshots = new List<(DateTime Period, int TotalMessages, int ActiveMembers)>
        {
            (new DateTime(2023, 1, 2, 9, 0, 0, DateTimeKind.Utc), 100, 10), // Monday 9am
            (new DateTime(2023, 1, 9, 9, 0, 0, DateTimeKind.Utc), 150, 15), // Monday 9am (another week)
            (new DateTime(2023, 1, 2, 10, 0, 0, DateTimeKind.Utc), 80, 8),  // Monday 10am
            (new DateTime(2023, 1, 3, 9, 0, 0, DateTimeKind.Utc), 120, 12)  // Tuesday 9am
        };

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, start, end, SnapshotGranularity.Hourly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hourlySnapshots);

        // Act
        var result = await _service.GetActivityHeatmapAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 unique day/hour combinations");

        // Monday 9am should have 250 messages (100 + 150)
        var monday9am = result.FirstOrDefault(x => x.DayOfWeek == 1 && x.Hour == 9);
        monday9am.Should().NotBeNull();
        monday9am!.MessageCount.Should().Be(250);

        // Monday 10am should have 80 messages
        var monday10am = result.FirstOrDefault(x => x.DayOfWeek == 1 && x.Hour == 10);
        monday10am.Should().NotBeNull();
        monday10am!.MessageCount.Should().Be(80);

        // Tuesday 9am should have 120 messages
        var tuesday9am = result.FirstOrDefault(x => x.DayOfWeek == 2 && x.Hour == 9);
        tuesday9am.Should().NotBeNull();
        tuesday9am!.MessageCount.Should().Be(120);
    }

    [Fact]
    public async Task GetActivityHeatmapAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, start, end, SnapshotGranularity.Hourly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateTime Period, int TotalMessages, int ActiveMembers)>());

        // Act
        var result = await _service.GetActivityHeatmapAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no hourly activity data is available");
    }

    [Fact]
    public async Task GetTopContributorsAsync_ShouldReturnTopMembersByMessageCount()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var limit = 5;

        var user1 = new User { Id = 111, Username = "User1", AvatarHash = "abc123" };
        var user2 = new User { Id = 222, Username = "User2", GlobalDisplayName = "Display2" };
        var user3 = new User { Id = 333, Username = "User3" };

        var topMembers = new List<MemberActivitySnapshot>
        {
            new() { UserId = 111, MessageCount = 500, UniqueChannelsActive = 5,
                    PeriodStart = start.AddDays(10), User = user1 },
            new() { UserId = 111, MessageCount = 300, UniqueChannelsActive = 3,
                    PeriodStart = start.AddDays(5), User = user1 },
            new() { UserId = 222, MessageCount = 400, UniqueChannelsActive = 4,
                    PeriodStart = start.AddDays(8), User = user2 },
            new() { UserId = 333, MessageCount = 250, UniqueChannelsActive = 2,
                    PeriodStart = start.AddDays(3), User = user3 }
        };

        _mockMemberActivityRepository
            .Setup(r => r.GetTopActiveMembersAsync(guildId, start, end, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topMembers);

        // Act
        var result = await _service.GetTopContributorsAsync(guildId, start, end, limit);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 unique users");

        // User1 should be first with 800 messages (500 + 300)
        result[0].UserId.Should().Be(111);
        result[0].Username.Should().Be("User1");
        result[0].MessageCount.Should().Be(800);
        result[0].UniqueChannels.Should().Be(5, "max of 5 and 3");
        result[0].AvatarUrl.Should().Contain("abc123", "avatar URL includes hash");

        // User2 should be second with 400 messages
        result[1].UserId.Should().Be(222);
        result[1].Username.Should().Be("User2");
        result[1].DisplayName.Should().Be("Display2");
        result[1].MessageCount.Should().Be(400);

        // User3 should be third with 250 messages
        result[2].UserId.Should().Be(333);
        result[2].Username.Should().Be("User3");
        result[2].MessageCount.Should().Be(250);
    }

    [Fact]
    public async Task GetTopContributorsAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMemberActivityRepository
            .Setup(r => r.GetTopActiveMembersAsync(guildId, start, end, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberActivitySnapshot>());

        // Act
        var result = await _service.GetTopContributorsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no member activity data is available");
    }

    [Fact]
    public async Task GetTopContributorsAsync_WithDefaultLimit_ShouldUse10()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMemberActivityRepository
            .Setup(r => r.GetTopActiveMembersAsync(guildId, start, end, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberActivitySnapshot>());

        // Act
        await _service.GetTopContributorsAsync(guildId, start, end);

        // Assert
        _mockMemberActivityRepository.Verify(
            r => r.GetTopActiveMembersAsync(guildId, start, end, 10, It.IsAny<CancellationToken>()),
            Times.Once,
            "default limit should be 10");
    }

    [Fact]
    public async Task GetTopContributorsAsync_WithNullUser_ShouldHandleGracefully()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var topMembers = new List<MemberActivitySnapshot>
        {
            new() { UserId = 111, MessageCount = 500, UniqueChannelsActive = 5,
                    PeriodStart = start.AddDays(10), User = null } // User not loaded
        };

        _mockMemberActivityRepository
            .Setup(r => r.GetTopActiveMembersAsync(guildId, start, end, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topMembers);

        // Act
        var result = await _service.GetTopContributorsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(111);
        result[0].Username.Should().Be("Unknown", "fallback when user is null");
        result[0].AvatarUrl.Should().BeNull("no avatar when user is null");
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldUseMessageLogsForMessageCounts_NotSnapshots()
    {
        // Arrange - This test verifies the fix for issue #579
        // ServerAnalyticsService should use MessageLogRepository (real-time data)
        // instead of MemberActivitySnapshots (pre-aggregated, potentially stale data)
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        // Member activity snapshots show DIFFERENT message counts (pre-aggregated, stale)
        var activitySnapshots = new List<(DateTime Period, int TotalMessages, int ActiveMembers)>
        {
            (now.AddDays(-1), 0, 100),  // Snapshot shows 0 messages
            (now.AddDays(-5), 0, 80)    // Snapshot shows 0 messages
        };

        // MessageLogs show the ACTUAL message counts (source of truth)
        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(now.AddDays(-1)), 5),  // MessageLogs show 5 messages
            (DateOnly.FromDateTime(now.AddDays(-5)), 3)   // MessageLogs show 3 messages
        };

        _mockGuildMetricsRepository
            .Setup(r => r.GetLatestAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildMetricsSnapshot?)null);

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                SnapshotGranularity.Daily, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activitySnapshots);

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelActivitySnapshot>());

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert - Message counts should come from MessageLogs, not snapshots
        result.Should().NotBeNull();
        result.Messages24h.Should().Be(5, "message count should come from MessageLogs (5), not snapshots (0)");
        result.Messages7d.Should().Be(8, "message count should come from MessageLogs (5+3=8), not snapshots (0)");

        // Verify that MessageLogRepository was called
        _mockMessageLogRepository.Verify(
            r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "GetMessagesByDayAsync should be called to get real-time message counts");
    }

    [Fact]
    public async Task GetActivityTimeSeriesAsync_ShouldShowMessagesEvenWhenSnapshotsAreEmpty()
    {
        // Arrange - This test verifies that we can display message data even before
        // the daily aggregation runs (when MemberActivitySnapshots are empty)
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 3, 23, 59, 59, DateTimeKind.Utc);

        // No activity snapshots (aggregation hasn't run yet)
        var activitySnapshots = new List<(DateTime Period, int TotalMessages, int ActiveMembers)>();

        // But MessageLogs has actual messages
        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(start), 10),
            (DateOnly.FromDateTime(start.AddDays(1)), 15),
            (DateOnly.FromDateTime(start.AddDays(2)), 8)
        };

        _mockMemberActivityRepository
            .Setup(r => r.GetActivityTimeSeriesAsync(guildId, start, end, SnapshotGranularity.Daily,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activitySnapshots);

        _mockChannelActivityRepository
            .Setup(r => r.GetChannelRankingsAsync(guildId, start, end, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelActivitySnapshot>());

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetActivityTimeSeriesAsync(guildId, start, end);

        // Assert - Should have data points even without activity snapshots
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "should have entries for each day with messages");
        result[0].MessageCount.Should().Be(10, "message count from MessageLogs");
        result[1].MessageCount.Should().Be(15, "message count from MessageLogs");
        result[2].MessageCount.Should().Be(8, "message count from MessageLogs");
        result[0].ActiveMembers.Should().Be(0, "no activity snapshot data available");
    }
}
