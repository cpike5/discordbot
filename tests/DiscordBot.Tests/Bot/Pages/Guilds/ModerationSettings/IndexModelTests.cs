using Discord.WebSocket;
using DiscordBot.Bot.Pages.Guilds.ModerationSettings;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.ModerationSettings;

/// <summary>
/// Unit tests for <see cref="IndexModel"/> Razor Page.
/// Tests the Guild Moderation Settings page handlers including GET and AJAX POST operations.
/// </summary>
public class IndexModelTests
{
    private readonly Mock<IGuildModerationConfigService> _mockConfigService;
    private readonly Mock<IModTagService> _mockModTagService;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<IFlaggedEventService> _mockFlaggedEventService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _indexModel;

    public IndexModelTests()
    {
        _mockConfigService = new Mock<IGuildModerationConfigService>();
        _mockModTagService = new Mock<IModTagService>();
        _mockGuildService = new Mock<IGuildService>();
        _mockFlaggedEventService = new Mock<IFlaggedEventService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(new DiscordSocketConfig());
        _mockLogger = new Mock<ILogger<IndexModel>>();

        _indexModel = new IndexModel(
            _mockConfigService.Object,
            _mockModTagService.Object,
            _mockGuildService.Object,
            _mockFlaggedEventService.Object,
            _mockDiscordClient.Object,
            _mockLogger.Object);

        // Setup PageContext
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _indexModel.PageContext = pageContext;
    }

    #region OnGetAsync Tests

    [Fact]
    public async Task OnGetAsync_WithValidGuild_ReturnsPageResult()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = CreateGuildDto(guildId, "Test Guild");
        var config = CreateModerationConfig(guildId);
        var tags = new List<ModTagDto> { CreateModTagDto("Helpful", "#00FF00") };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockModTagService
            .Setup(s => s.GetGuildTagsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        SetupFlaggedEventService(guildId, 0, 0, 0);

        _indexModel.GuildId = guildId;

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("valid guild should return PageResult");
        _indexModel.ViewModel.Should().NotBeNull();
        _indexModel.ViewModel.GuildId.Should().Be(guildId);
        _indexModel.ViewModel.Mode.Should().Be(ConfigMode.Simple);
        _indexModel.ViewModel.Tags.Should().HaveCount(1);
        _indexModel.GuildName.Should().Be("Test Guild");
    }

    [Fact]
    public async Task OnGetAsync_WithNonExistentGuild_ReturnsNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        _indexModel.GuildId = guildId;

        // Act
        var result = await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("non-existent guild should return NotFound");

        _mockConfigService.Verify(
            s => s.GetConfigAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "config service should not be called when guild is not found");
    }

    [Fact]
    public async Task OnGetAsync_LoadsGuildInformation()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = CreateGuildDto(guildId, "Test Guild", "https://cdn.discord.com/icons/123/icon.png");
        var config = CreateModerationConfig(guildId);

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockModTagService
            .Setup(s => s.GetGuildTagsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModTagDto>());

        SetupFlaggedEventService(guildId, 0, 0, 0);

        _indexModel.GuildId = guildId;

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _indexModel.GuildName.Should().Be("Test Guild");
        _indexModel.GuildIconUrl.Should().Be("https://cdn.discord.com/icons/123/icon.png");
    }

    [Fact]
    public async Task OnGetAsync_LoadsStatisticsForLast24Hours()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = CreateGuildDto(guildId, "Test Guild");
        var config = CreateModerationConfig(guildId);

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockModTagService
            .Setup(s => s.GetGuildTagsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModTagDto>());

        SetupFlaggedEventService(guildId, 10, 3, 2);

        _indexModel.GuildId = guildId;

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _indexModel.EventsFlagged.Should().Be(10, "should show flagged events from last 24 hours");
        _indexModel.AutoActions.Should().Be(3, "should show auto-actions from last 24 hours");
        _indexModel.FalsePositives.Should().Be(2, "should show false positives from last 24 hours");
    }

    [Fact]
    public async Task OnGetAsync_CalculatesActiveRulesCount()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = CreateGuildDto(guildId, "Test Guild");
        var config = CreateModerationConfig(guildId);
        config.SpamConfig.Enabled = true;
        config.ContentFilterConfig.Enabled = true;
        config.RaidProtectionConfig.Enabled = false;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockModTagService
            .Setup(s => s.GetGuildTagsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModTagDto>());

        SetupFlaggedEventService(guildId, 0, 0, 0);

        _indexModel.GuildId = guildId;

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _indexModel.ActiveRules.Should().Be(2, "only spam and content filter are enabled");
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var guild = CreateGuildDto(guildId, "Test Guild");
        var config = CreateModerationConfig(guildId);

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockModTagService
            .Setup(s => s.GetGuildTagsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModTagDto>());

        SetupFlaggedEventService(guildId, 0, 0, 0);

        _indexModel.GuildId = guildId;

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Moderation settings page accessed") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when accessing moderation settings page");
    }

    [Fact]
    public async Task OnGetAsync_LogsWarningWhenGuildNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        _indexModel.GuildId = guildId;

        // Act
        await _indexModel.OnGetAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
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

    #endregion


    #region Helper Methods

    private static GuildDto CreateGuildDto(ulong guildId, string name, string? iconUrl = null)
    {
        return new GuildDto
        {
            Id = guildId,
            Name = name,
            IconUrl = iconUrl,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            MemberCount = 100
        };
    }

    private static GuildModerationConfigDto CreateModerationConfig(ulong guildId)
    {
        return new GuildModerationConfigDto
        {
            GuildId = guildId,
            Mode = ConfigMode.Simple,
            SimplePreset = "Moderate",
            SpamConfig = new SpamDetectionConfigDto
            {
                Enabled = true,
                MaxMessagesPerWindow = 5,
                WindowSeconds = 5,
                MaxMentionsPerMessage = 5,
                DuplicateMessageThreshold = 0.8,
                AutoAction = AutoAction.Delete
            },
            ContentFilterConfig = new ContentFilterConfigDto
            {
                Enabled = true,
                ProhibitedWords = new List<string>(),
                AllowedLinkDomains = new List<string>(),
                BlockUnlistedLinks = false,
                BlockInviteLinks = false,
                AutoAction = AutoAction.Delete
            },
            RaidProtectionConfig = new RaidProtectionConfigDto
            {
                Enabled = true,
                MaxJoinsPerWindow = 10,
                WindowSeconds = 10,
                MinAccountAgeHours = 0,
                AutoAction = RaidAutoAction.AlertOnly
            },
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ModTagDto CreateModTagDto(string name, string color)
    {
        return new ModTagDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            Name = name,
            Color = color,
            Category = TagCategory.Positive,
            Description = $"{name} tag",
            IsFromTemplate = false,
            CreatedAt = DateTime.UtcNow,
            UserCount = 0
        };
    }

    private void SetupFlaggedEventService(ulong guildId, int eventsFlagged, int autoActions, int falsePositives)
    {
        var now = DateTime.UtcNow;
        var events = new List<FlaggedEventDto>();

        // Add pending flagged events (these count toward eventsFlagged)
        // Note: eventsFlagged includes false positives in the implementation
        int pendingEvents = eventsFlagged - falsePositives;
        for (int i = 0; i < pendingEvents; i++)
        {
            events.Add(new FlaggedEventDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 111UL,
                Username = "User1",
                RuleType = RuleType.Spam,
                Severity = Severity.Medium,
                Description = "Spam detected",
                Evidence = "{}",
                Status = FlaggedEventStatus.Pending,
                CreatedAt = now.AddMinutes(-10),
                ActionTaken = i < autoActions ? "Message deleted" : null
            });
        }

        // Add false positives (dismissed events - these also count toward eventsFlagged)
        for (int i = 0; i < falsePositives; i++)
        {
            events.Add(new FlaggedEventDto
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 222UL,
                Username = "User2",
                RuleType = RuleType.Content,
                Severity = Severity.Low,
                Description = "False positive",
                Evidence = "{}",
                Status = FlaggedEventStatus.Dismissed,
                CreatedAt = now.AddMinutes(-5),
                ReviewedByUserId = 333UL,
                ReviewedByUsername = "Moderator",
                ReviewedAt = now.AddMinutes(-4)
            });
        }

        _mockFlaggedEventService
            .Setup(s => s.GetPendingEventsAsync(guildId, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((events, events.Count));
    }

    #endregion
}
