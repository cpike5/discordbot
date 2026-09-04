using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Services.Guilds;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Guilds;

/// <summary>
/// Unit tests for <see cref="GuildDetailsAggregator"/>.
/// </summary>
public class GuildDetailsAggregatorTests
{
    private readonly Mock<IGuildService> _mockGuildService = new();
    private readonly Mock<ICommandLogService> _mockCommandLogService = new();
    private readonly Mock<IWelcomeService> _mockWelcomeService = new();
    private readonly Mock<IScheduledMessageService> _mockScheduledMessageService = new();
    private readonly Mock<IRatWatchService> _mockRatWatchService = new();
    private readonly Mock<IReminderRepository> _mockReminderRepository = new();
    private readonly Mock<IGuildMemberService> _mockGuildMemberService = new();
    private readonly Mock<IGuildAudioSettingsService> _mockGuildAudioSettingsService = new();
    private readonly Mock<ISoundRepository> _mockSoundRepository = new();
    private readonly Mock<ITtsMessageRepository> _mockTtsMessageRepository = new();
    private readonly Mock<IAssistantGuildSettingsService> _mockAssistantGuildSettingsService = new();
    private readonly GuildDetailsAggregator _aggregator;

    public GuildDetailsAggregatorTests()
    {
        var assistantOptions = Options.Create(new AssistantOptions
        {
            GloballyEnabled = true,
            DefaultRateLimit = 10,
            RateLimitWindowMinutes = 5
        });

        _aggregator = new GuildDetailsAggregator(
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _mockWelcomeService.Object,
            _mockScheduledMessageService.Object,
            _mockRatWatchService.Object,
            _mockReminderRepository.Object,
            _mockGuildMemberService.Object,
            _mockGuildAudioSettingsService.Object,
            _mockSoundRepository.Object,
            _mockTtsMessageRepository.Object,
            _mockAssistantGuildSettingsService.Object,
            assistantOptions,
            Mock.Of<ILogger<GuildDetailsAggregator>>());
    }

    private void SetupHappyPathDefaults(ulong guildId)
    {
        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto> { Items = new List<CommandLogDto>(), Page = 1, PageSize = 10, TotalCount = 0 });

        _mockWelcomeService
            .Setup(s => s.GetConfigurationAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfigurationDto?)null);

        _mockScheduledMessageService
            .Setup(s => s.GetByGuildIdAsync(guildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ScheduledMessageDto>(), 0));

        _mockRatWatchService
            .Setup(s => s.GetGuildSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { IsEnabled = true, Timezone = "Eastern Standard Time" });
        _mockRatWatchService
            .Setup(s => s.GetByGuildAsync(guildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<RatWatchDto>(), 0));
        _mockRatWatchService
            .Setup(s => s.GetLeaderboardAsync(guildId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RatLeaderboardEntryDto>());

        _mockReminderRepository
            .Setup(r => r.GetGuildStatsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, 0, 0));
        _mockReminderRepository
            .Setup(r => r.GetUpcomingAsync(guildId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<UpcomingReminderDto>());

        _mockGuildMemberService
            .Setup(s => s.GetMemberCountAsync(guildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockGuildMemberService
            .Setup(s => s.GetMembersAsync(guildId, It.IsAny<GuildMemberQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<GuildMemberDto> { Items = new List<GuildMemberDto>(), Page = 1, PageSize = 5, TotalCount = 0 });

        _mockGuildAudioSettingsService
            .Setup(s => s.GetSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAudioSettings?)null);

        _mockSoundRepository
            .Setup(s => s.GetSoundCountAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockSoundRepository
            .Setup(s => s.GetTopSoundsByPlayCountAsync(guildId, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(string Name, int PlayCount)>());

        _mockTtsMessageRepository
            .Setup(s => s.GetMostUsedVoiceAsync(guildId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAssistantGuildSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantGuildSettings());
    }

    [Fact]
    public async Task BuildAsync_WhenGuildExists_ReturnsAggregateWithGuildAndWidgetData()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupHappyPathDefaults(guildId);

        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            MemberCount = 150,
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddMonths(-3),
            Prefix = "!"
        };
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        // Act
        var result = await _aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Guild.Should().BeSameAs(guildDto);
        result.AssistantGloballyEnabled.Should().BeTrue();
        result.AssistantRateLimit.Should().Be(10);
        result.AssistantRateLimitWindowMinutes.Should().Be(5);
        result.RatWatchEnabled.Should().BeTrue();

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.Is<CommandLogQueryDto>(q => q.GuildId == guildId && q.PageSize == 10), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAsync_WhenGuildDoesNotExist_ReturnsNullWithoutFetchingWidgetData()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "widget data should not be fetched when the guild does not exist");
        _mockWelcomeService.Verify(
            s => s.GetConfigurationAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BuildAsync_FetchesRecentCommandsForGuild()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupHappyPathDefaults(guildId);

        var guildDto = new GuildDto { Id = guildId, Name = "Test Guild", MemberCount = 100, IsActive = true, JoinedAt = DateTime.UtcNow };
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        var commandLogs = new List<CommandLogDto>
        {
            new() { Id = Guid.NewGuid(), GuildId = guildId, UserId = 111UL, Username = "User1", CommandName = "ping", ExecutedAt = DateTime.UtcNow.AddMinutes(-5), ResponseTimeMs = 50, Success = true },
            new() { Id = Guid.NewGuid(), GuildId = guildId, UserId = 222UL, Username = "User2", CommandName = "status", ExecutedAt = DateTime.UtcNow.AddMinutes(-10), ResponseTimeMs = 75, Success = true }
        };
        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.Is<CommandLogQueryDto>(q => q.GuildId == guildId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto> { Items = commandLogs, Page = 1, PageSize = 10, TotalCount = 2 });

        // Act
        var result = await _aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RecentCommandLogs.Should().HaveCount(2, "recent command logs should be fetched for the guild");
        result.RecentCommandLogs[0].CommandName.Should().Be("ping");
        result.RecentCommandLogs[1].CommandName.Should().Be("status");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.Is<CommandLogQueryDto>(q => q.GuildId == guildId), It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called with the guild ID filter");
    }

    [Fact]
    public async Task BuildAsync_LimitsRecentCommandsToRequestedLimit()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupHappyPathDefaults(guildId);

        var guildDto = new GuildDto { Id = guildId, Name = "Test Guild", MemberCount = 100, IsActive = true, JoinedAt = DateTime.UtcNow };
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        // Act
        await _aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.GuildId == guildId && q.Page == 1 && q.PageSize == 10),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "command log service should be called with PageSize = 10 to limit recent commands");
    }

    [Fact]
    public async Task BuildAsync_LogsWarningWhenGuildNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var mockLogger = new Mock<ILogger<GuildDetailsAggregator>>();
        var assistantOptions = Options.Create(new AssistantOptions { GloballyEnabled = true, DefaultRateLimit = 10, RateLimitWindowMinutes = 5 });
        var aggregator = new GuildDetailsAggregator(
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _mockWelcomeService.Object,
            _mockScheduledMessageService.Object,
            _mockRatWatchService.Object,
            _mockReminderRepository.Object,
            _mockGuildMemberService.Object,
            _mockGuildAudioSettingsService.Object,
            _mockSoundRepository.Object,
            _mockTtsMessageRepository.Object,
            _mockAssistantGuildSettingsService.Object,
            assistantOptions,
            mockLogger.Object);

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        await aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Guild") &&
                    v.ToString()!.Contains("not found") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning log should be written when guild is not found");
    }

    [Fact]
    public async Task BuildAsync_WithCancellationToken_PassesToServices()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        SetupHappyPathDefaults(guildId);
        var guildDto = new GuildDto { Id = guildId, Name = "Test Guild", MemberCount = 100, IsActive = true, JoinedAt = DateTime.UtcNow };
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        // Act
        await _aggregator.BuildAsync(guildId, 10, cancellationToken);

        // Assert
        _mockGuildService.Verify(
            s => s.GetGuildByIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to guild service");

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), cancellationToken),
            Times.Once,
            "cancellation token should be passed to command log service");
    }

    [Fact]
    public async Task BuildAsync_WithNoRecentCommands_ReturnsEmptyCommandList()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupHappyPathDefaults(guildId);

        var guildDto = new GuildDto { Id = guildId, Name = "Test Guild", MemberCount = 100, IsActive = true, JoinedAt = DateTime.UtcNow };
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        // Act
        var result = await _aggregator.BuildAsync(guildId, 10, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.RecentCommandLogs.Should().NotBeNull();
        result.RecentCommandLogs.Should().BeEmpty("guild with no command history should have empty command list");
    }
}
