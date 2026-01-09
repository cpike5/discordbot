using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ModerationAnalyticsService"/>.
/// </summary>
public class ModerationAnalyticsServiceTests
{
    private readonly Mock<IModerationCaseRepository> _mockModerationCaseRepository;
    private readonly Mock<ILogger<ModerationAnalyticsService>> _mockLogger;
    private readonly ModerationAnalyticsService _service;

    public ModerationAnalyticsServiceTests()
    {
        _mockModerationCaseRepository = new Mock<IModerationCaseRepository>();
        _mockLogger = new Mock<ILogger<ModerationAnalyticsService>>();
        _service = new ModerationAnalyticsService(
            _mockModerationCaseRepository.Object,
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

        var currentPeriodCases = new List<ModerationCase>
        {
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(1) },
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(2) },
            new() { GuildId = guildId, Type = CaseType.Mute, CreatedAt = start.AddDays(5) },
            new() { GuildId = guildId, Type = CaseType.Kick, CreatedAt = start.AddDays(10) },
            new() { GuildId = guildId, Type = CaseType.Ban, CreatedAt = start.AddDays(15) },
            new() { GuildId = guildId, Type = CaseType.Note, CreatedAt = start.AddDays(20) },
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = now.AddHours(-12) }, // Last 24h
            new() { GuildId = guildId, Type = CaseType.Mute, CreatedAt = now.AddDays(-3) }   // Last 7d
        };

        var previousPeriodCases = new List<ModerationCase>
        {
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(-5) },
            new() { GuildId = guildId, Type = CaseType.Kick, CreatedAt = start.AddDays(-10) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((currentPeriodCases, currentPeriodCases.Count));

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, It.Is<DateTime>(d => d < start), start, 1, int.MaxValue,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((previousPeriodCases, previousPeriodCases.Count));

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalCases.Should().Be(8, "there are 8 cases in current period");
        result.WarnCount.Should().Be(3, "there are 3 warn cases");
        result.MuteCount.Should().Be(2, "there are 2 mute cases");
        result.KickCount.Should().Be(1, "there is 1 kick case");
        result.BanCount.Should().Be(1, "there is 1 ban case");
        result.NoteCount.Should().Be(1, "there is 1 note case");
        result.Cases24h.Should().Be(1, "1 case in last 24h");
        result.Cases7d.Should().Be(2, "2 cases in last 7d");
        result.CasesPerDay.Should().BeGreaterThan(0);
        result.ChangeFromPreviousPeriod.Should().Be(300, "8 cases vs 2 previous = 300% increase");
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoCases_ShouldReturnZeroValues()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, int.MaxValue,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalCases.Should().Be(0);
        result.WarnCount.Should().Be(0);
        result.MuteCount.Should().Be(0);
        result.KickCount.Should().Be(0);
        result.BanCount.Should().Be(0);
        result.NoteCount.Should().Be(0);
        result.Cases24h.Should().Be(0);
        result.Cases7d.Should().Be(0);
        result.CasesPerDay.Should().Be(0);
        result.ChangeFromPreviousPeriod.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoPreviousPeriodCases_ShouldReturn100PercentChange()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var currentPeriodCases = new List<ModerationCase>
        {
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(1) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((currentPeriodCases, currentPeriodCases.Count));

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, It.Is<DateTime?>(d => d < start), start, 1, int.MaxValue,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetSummaryAsync(guildId, start, end);

        // Assert
        result.ChangeFromPreviousPeriod.Should().Be(100, "0 to 1 case = 100% increase");
    }

    [Fact]
    public async Task GetSummaryAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, int.MaxValue,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        await _service.GetSummaryAsync(guildId, start, end, cancellationToken);

        // Assert
        _mockModerationCaseRepository.Verify(
            r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository for current period");

        _mockModerationCaseRepository.Verify(
            r => r.GetByGuildAsync(guildId, null, null, null, It.IsAny<DateTime?>(), start, 1, int.MaxValue, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository for previous period");
    }

    [Fact]
    public async Task GetTrendsAsync_ShouldGroupCasesByDate()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var cases = new List<ModerationCase>
        {
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = new DateTime(2023, 1, 5, 10, 0, 0) },
            new() { GuildId = guildId, Type = CaseType.Mute, CreatedAt = new DateTime(2023, 1, 5, 15, 0, 0) },
            new() { GuildId = guildId, Type = CaseType.Kick, CreatedAt = new DateTime(2023, 1, 10, 10, 0, 0) },
            new() { GuildId = guildId, Type = CaseType.Ban, CreatedAt = new DateTime(2023, 1, 10, 12, 0, 0) },
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = new DateTime(2023, 1, 10, 14, 0, 0) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _service.GetTrendsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "there are 2 unique dates with cases");

        var jan5 = result.FirstOrDefault(x => x.Date.Day == 5);
        jan5.Should().NotBeNull();
        jan5!.TotalCases.Should().Be(2, "2 cases on Jan 5");
        jan5.WarnCount.Should().Be(1);
        jan5.MuteCount.Should().Be(1);
        jan5.KickCount.Should().Be(0);
        jan5.BanCount.Should().Be(0);

        var jan10 = result.FirstOrDefault(x => x.Date.Day == 10);
        jan10.Should().NotBeNull();
        jan10!.TotalCases.Should().Be(3, "3 cases on Jan 10");
        jan10.WarnCount.Should().Be(1);
        jan10.KickCount.Should().Be(1);
        jan10.BanCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTrendsAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetTrendsAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no cases available");
    }

    [Fact]
    public async Task GetRepeatOffendersAsync_ShouldReturnUsersWithMultipleCases()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var cases = new List<ModerationCase>
        {
            // User 111 - 3 cases (repeat offender)
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(1) },
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Mute,
                    CreatedAt = start.AddDays(5) },
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Kick,
                    CreatedAt = start.AddDays(10) },
            // User 222 - 2 cases (repeat offender)
            new() { GuildId = guildId, TargetUserId = 222, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(2) },
            new() { GuildId = guildId, TargetUserId = 222, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(8) },
            // User 333 - 1 case (not a repeat offender)
            new() { GuildId = guildId, TargetUserId = 333, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(3) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _service.GetRepeatOffendersAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "only users with 2+ cases are repeat offenders");

        // User 111 should be first with 3 cases
        result[0].UserId.Should().Be(111);
        result[0].TotalCases.Should().Be(3);
        result[0].WarnCount.Should().Be(1);
        result[0].MuteCount.Should().Be(1);
        result[0].KickCount.Should().Be(1);
        result[0].FirstIncident.Should().Be(start.AddDays(1));
        result[0].LastIncident.Should().Be(start.AddDays(10));
        result[0].EscalationPath.Should().Equal("Warn", "Mute", "Kick");

        // User 222 should be second with 2 cases
        result[1].UserId.Should().Be(222);
        result[1].TotalCases.Should().Be(2);
        result[1].WarnCount.Should().Be(2);
        result[1].EscalationPath.Should().Equal("Warn", "Warn");
    }

    [Fact]
    public async Task GetRepeatOffendersAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var limit = 1;

        var cases = new List<ModerationCase>
        {
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(1) },
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Mute,
                    CreatedAt = start.AddDays(5) },
            new() { GuildId = guildId, TargetUserId = 111, Type = CaseType.Kick,
                    CreatedAt = start.AddDays(10) },
            new() { GuildId = guildId, TargetUserId = 222, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(2) },
            new() { GuildId = guildId, TargetUserId = 222, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(8) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _service.GetRepeatOffendersAsync(guildId, start, end, limit);

        // Assert
        result.Should().HaveCount(1, "limit is 1");
        result[0].UserId.Should().Be(111, "user with most cases should be returned");
    }

    [Fact]
    public async Task GetRepeatOffendersAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetRepeatOffendersAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no cases available");
    }

    [Fact]
    public async Task GetModeratorWorkloadAsync_ShouldDistributeWorkloadByModerator()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var cases = new List<ModerationCase>
        {
            // Moderator 111 - 5 cases (50%)
            new() { GuildId = guildId, ModeratorUserId = 111, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(1) },
            new() { GuildId = guildId, ModeratorUserId = 111, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(2) },
            new() { GuildId = guildId, ModeratorUserId = 111, Type = CaseType.Mute,
                    CreatedAt = start.AddDays(3) },
            new() { GuildId = guildId, ModeratorUserId = 111, Type = CaseType.Kick,
                    CreatedAt = start.AddDays(4) },
            new() { GuildId = guildId, ModeratorUserId = 111, Type = CaseType.Ban,
                    CreatedAt = start.AddDays(5) },
            // Moderator 222 - 3 cases (30%)
            new() { GuildId = guildId, ModeratorUserId = 222, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(6) },
            new() { GuildId = guildId, ModeratorUserId = 222, Type = CaseType.Mute,
                    CreatedAt = start.AddDays(7) },
            new() { GuildId = guildId, ModeratorUserId = 222, Type = CaseType.Kick,
                    CreatedAt = start.AddDays(8) },
            // Moderator 333 - 2 cases (20%)
            new() { GuildId = guildId, ModeratorUserId = 333, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(9) },
            new() { GuildId = guildId, ModeratorUserId = 333, Type = CaseType.Warn,
                    CreatedAt = start.AddDays(10) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _service.GetModeratorWorkloadAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 moderators");

        // Moderator 111 should be first with 5 actions (50%)
        result[0].ModeratorId.Should().Be(111);
        result[0].TotalActions.Should().Be(5);
        result[0].WarnCount.Should().Be(2);
        result[0].MuteCount.Should().Be(1);
        result[0].KickCount.Should().Be(1);
        result[0].BanCount.Should().Be(1);
        result[0].Percentage.Should().Be(50);

        // Moderator 222 should be second with 3 actions (30%)
        result[1].ModeratorId.Should().Be(222);
        result[1].TotalActions.Should().Be(3);
        result[1].Percentage.Should().Be(30);

        // Moderator 333 should be third with 2 actions (20%)
        result[2].ModeratorId.Should().Be(333);
        result[2].TotalActions.Should().Be(2);
        result[2].Percentage.Should().Be(20);
    }

    [Fact]
    public async Task GetModeratorWorkloadAsync_WithEmptyData_ShouldReturnEmptyCollection()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetModeratorWorkloadAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no cases available");
    }

    [Fact]
    public async Task GetCaseDistributionAsync_ShouldCountCasesByType()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var cases = new List<ModerationCase>
        {
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(1) },
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(2) },
            new() { GuildId = guildId, Type = CaseType.Warn, CreatedAt = start.AddDays(3) },
            new() { GuildId = guildId, Type = CaseType.Mute, CreatedAt = start.AddDays(4) },
            new() { GuildId = guildId, Type = CaseType.Mute, CreatedAt = start.AddDays(5) },
            new() { GuildId = guildId, Type = CaseType.Kick, CreatedAt = start.AddDays(6) },
            new() { GuildId = guildId, Type = CaseType.Ban, CreatedAt = start.AddDays(7) },
            new() { GuildId = guildId, Type = CaseType.Note, CreatedAt = start.AddDays(8) },
            new() { GuildId = guildId, Type = CaseType.Note, CreatedAt = start.AddDays(9) }
        };

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((cases, cases.Count));

        // Act
        var result = await _service.GetCaseDistributionAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(9, "there are 9 total cases");
        result.WarnCount.Should().Be(3);
        result.MuteCount.Should().Be(2);
        result.KickCount.Should().Be(1);
        result.BanCount.Should().Be(1);
        result.NoteCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCaseDistributionAsync_WithEmptyData_ShouldReturnZeroValues()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        var result = await _service.GetCaseDistributionAsync(guildId, start, end);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.WarnCount.Should().Be(0);
        result.MuteCount.Should().Be(0);
        result.KickCount.Should().Be(0);
        result.BanCount.Should().Be(0);
        result.NoteCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCaseDistributionAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var guildId = 123456789UL;
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockModerationCaseRepository
            .Setup(r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModerationCase>(), 0));

        // Act
        await _service.GetCaseDistributionAsync(guildId, start, end, cancellationToken);

        // Assert
        _mockModerationCaseRepository.Verify(
            r => r.GetByGuildAsync(guildId, null, null, null, start, end, 1, int.MaxValue, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }
}
