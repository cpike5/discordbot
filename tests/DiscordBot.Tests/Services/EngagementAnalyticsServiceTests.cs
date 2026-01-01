using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EngagementAnalyticsService"/>.
/// </summary>
public class EngagementAnalyticsServiceTests
{
    private readonly Mock<IMessageLogRepository> _mockMessageLogRepository;
    private readonly Mock<IGuildMetricsRepository> _mockGuildMetricsRepository;
    private readonly Mock<IGuildMemberRepository> _mockGuildMemberRepository;
    private readonly Mock<ILogger<EngagementAnalyticsService>> _mockLogger;
    private readonly EngagementAnalyticsService _service;

    public EngagementAnalyticsServiceTests()
    {
        _mockMessageLogRepository = new Mock<IMessageLogRepository>();
        _mockGuildMetricsRepository = new Mock<IGuildMetricsRepository>();
        _mockGuildMemberRepository = new Mock<IGuildMemberRepository>();
        _mockLogger = new Mock<ILogger<EngagementAnalyticsService>>();
        _service = new EngagementAnalyticsService(
            _mockMessageLogRepository.Object,
            _mockGuildMetricsRepository.Object,
            _mockGuildMemberRepository.Object,
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

        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(start), 100L),
            (DateOnly.FromDateTime(start.AddDays(1)), 150L),
            (DateOnly.FromDateTime(start.AddDays(2)), 200L),
            (DateOnly.FromDateTime(now.AddHours(-12)), 50L) // Last 24h
        };

        var metricsLast7d = new List<GuildMetricsSnapshot>
        {
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(now.AddDays(-6)),
                    MembersJoined = 5 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(now.AddDays(-3)),
                    MembersJoined = 3 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(now.AddDays(-1)),
                    MembersJoined = 2 }
        };

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metricsLast7d);

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalMessages.Should().BeGreaterThan(0);
        result.MessagesPerDay.Should().BeGreaterThan(0);
        result.NewMembers7d.Should().Be(10, "5 + 3 + 2 = 10 new members");
        result.NewMemberRetentionRate.Should().Be(0, "retention calculation is simplified");
        result.ReactionCount.Should().Be(0, "not currently tracked");
        result.VoiceMinutes.Should().Be(0, "not currently tracked");
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoMessages_ShouldReturnZeroValues()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalMessages.Should().Be(0);
        result.Messages24h.Should().Be(0);
        result.Messages7d.Should().Be(0);
        result.MessagesPerDay.Should().Be(0);
        result.ActiveMembers.Should().Be(0);
        result.NewMembers7d.Should().Be(0);
        result.NewMemberRetentionRate.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_WithCancellationToken_ShouldPassToRepositories()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        await _service.GetSummaryAsync(guildId, start, end, cancellationToken);

        // Assert
        _mockMessageLogRepository.Verify(
            r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, cancellationToken),
            Times.AtLeastOnce,
            "cancellation token should be passed to GetMessagesByDayAsync");

        _mockGuildMetricsRepository.Verify(
            r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), cancellationToken),
            Times.AtLeastOnce,
            "cancellation token should be passed to GetByDateRangeAsync");
    }

    [Fact]
    public async Task GetMessageTrendsAsync_ShouldReturnDailyMessageCounts()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(start), 100L),
            (DateOnly.FromDateTime(start.AddDays(1)), 150L),
            (DateOnly.FromDateTime(start.AddDays(2)), 200L),
            (DateOnly.FromDateTime(start.AddDays(3)), 175L)
        };

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetMessageTrendsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4, "there are 4 days with messages");
        result[0].Date.Should().Be(start.Date);
        result[0].MessageCount.Should().Be(100);
        result[0].UniqueAuthors.Should().Be(0, "not calculated by simplified implementation");
        result[0].AvgMessageLength.Should().Be(0, "not calculated by simplified implementation");
        result[1].Date.Should().Be(start.AddDays(1).Date);
        result[1].MessageCount.Should().Be(150);
    }

    [Fact]
    public async Task GetMessageTrendsAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        // Act
        var result = await _service.GetMessageTrendsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no message data is available");
    }

    [Fact]
    public async Task GetMessageTrendsAsync_ShouldFilterToRequestedDateRange()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 10, 23, 59, 59, DateTimeKind.Utc);

        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (new DateOnly(2023, 1, 1), 100L),  // Before range
            (new DateOnly(2023, 1, 5), 150L),  // In range
            (new DateOnly(2023, 1, 8), 200L),  // In range
            (new DateOnly(2023, 1, 10), 175L), // In range
            (new DateOnly(2023, 1, 15), 50L)   // After range
        };

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        var result = await _service.GetMessageTrendsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "only 3 dates are within the range");
        result.Should().OnlyContain(x => x.Date >= start && x.Date <= end,
            "all results should be within the requested date range");
    }

    [Fact]
    public async Task GetMessageTrendsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        // Act
        await _service.GetMessageTrendsAsync(guildId, start, end, cancellationToken);

        // Assert
        _mockMessageLogRepository.Verify(
            r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetNewMemberRetentionAsync_ShouldReturnRetentionMetrics()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var metrics = new List<GuildMetricsSnapshot>
        {
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(start),
                    MembersJoined = 10, MembersLeft = 2 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(start.AddDays(5)),
                    MembersJoined = 5, MembersLeft = 1 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(start.AddDays(10)),
                    MembersJoined = 8, MembersLeft = 0 }
        };

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _service.GetNewMemberRetentionAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 dates with members joined");
        result[0].JoinDate.Should().Be(start.Date);
        result[0].NewMembers.Should().Be(10);
        result[0].SentFirstMessage.Should().Be(0, "detailed tracking not implemented");
        result[0].StillActive7d.Should().Be(0, "detailed tracking not implemented");
        result[0].StillActive30d.Should().Be(0, "detailed tracking not implemented");
        result[1].JoinDate.Should().Be(start.AddDays(5).Date);
        result[1].NewMembers.Should().Be(5);
    }

    [Fact]
    public async Task GetNewMemberRetentionAsync_WithNoNewMembers_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var metrics = new List<GuildMetricsSnapshot>
        {
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(start),
                    MembersJoined = 0, MembersLeft = 2 },
            new() { GuildId = guildId, SnapshotDate = DateOnly.FromDateTime(start.AddDays(5)),
                    MembersJoined = 0, MembersLeft = 1 }
        };

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _service.GetNewMemberRetentionAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no new members joined during the period");
    }

    [Fact]
    public async Task GetNewMemberRetentionAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        var result = await _service.GetNewMemberRetentionAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no metrics data is available");
    }

    [Fact]
    public async Task GetNewMemberRetentionAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        await _service.GetNewMemberRetentionAsync(guildId, start, end, cancellationToken);

        // Assert
        _mockGuildMetricsRepository.Verify(
            r => r.GetByDateRangeAsync(guildId, DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end), cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetMessageTrendsAsync_ShouldCalculateDaySpanCorrectly()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 10, 23, 59, 59, DateTimeKind.Utc); // 10 days

        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (new DateOnly(2023, 1, 5), 100L)
        };

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        // Act
        await _service.GetMessageTrendsAsync(guildId, start, end);

        // Assert
        _mockMessageLogRepository.Verify(
            r => r.GetMessagesByDayAsync(It.Is<int>(days => days >= 10), guildId,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "day span should be calculated from start to end dates");
    }

    [Fact]
    public async Task GetSummaryAsync_WithZeroDaySpan_ShouldHandleGracefully()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 1, 23, 59, 59, DateTimeKind.Utc); // Same day

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(DateOnly Date, long Count)>());

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.MessagesPerDay.Should().Be(0, "zero day span should result in 0 messages per day");
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCalculateMessagesPerDayCorrectly()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 11, 0, 0, 0, DateTimeKind.Utc); // 10 days

        var messagesByDay = new List<(DateOnly Date, long Count)>
        {
            (DateOnly.FromDateTime(start), 100L),
            (DateOnly.FromDateTime(start.AddDays(1)), 100L),
            (DateOnly.FromDateTime(start.AddDays(2)), 100L),
            (DateOnly.FromDateTime(start.AddDays(3)), 100L),
            (DateOnly.FromDateTime(start.AddDays(4)), 100L) // 500 total messages over 10 days
        };

        _mockMessageLogRepository
            .Setup(r => r.GetMessagesByDayAsync(It.IsAny<int>(), guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messagesByDay);

        _mockGuildMetricsRepository
            .Setup(r => r.GetByDateRangeAsync(guildId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildMetricsSnapshot>());

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.TotalMessages.Should().Be(500, "sum of all messages");
        result.MessagesPerDay.Should().Be(50, "500 messages / 10 days = 50 per day");
    }
}
