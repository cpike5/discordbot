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

    #region OnPostSaveOverviewAsync Tests

    [Fact]
    public async Task OnPostSaveOverviewAsync_WithValidRequest_SavesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new OverviewUpdateDto
        {
            Mode = ConfigMode.Advanced,
            SimplePreset = null
        };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(guildId, It.IsAny<GuildModerationConfigDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _indexModel.OnPostSaveOverviewAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>("should return JsonResult");
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("Overview settings saved successfully");

        _mockConfigService.Verify(
            s => s.UpdateConfigAsync(
                guildId,
                It.Is<GuildModerationConfigDto>(c => c.Mode == ConfigMode.Advanced && c.SimplePreset == null),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "config should be updated with new mode and preset");
    }

    [Fact]
    public async Task OnPostSaveOverviewAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new OverviewUpdateDto { Mode = ConfigMode.Simple, SimplePreset = "Moderate" };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _indexModel.OnPostSaveOverviewAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeFalse();
    }

    [Fact]
    public async Task OnPostSaveOverviewAsync_LogsInformationMessages()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new OverviewUpdateDto { Mode = ConfigMode.Simple, SimplePreset = "Strict" };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(guildId, It.IsAny<GuildModerationConfigDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        await _indexModel.OnPostSaveOverviewAsync(request, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Saving overview settings") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Overview settings saved successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region OnPostSaveSpamAsync Tests

    [Fact]
    public async Task OnPostSaveSpamAsync_WithValidRequest_SavesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new SpamDetectionConfigDto
        {
            Enabled = true,
            MaxMessagesPerWindow = 10,
            WindowSeconds = 5,
            MaxMentionsPerMessage = 3,
            DuplicateMessageThreshold = 0.9,
            AutoAction = AutoAction.Warn
        };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(guildId, It.IsAny<GuildModerationConfigDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _indexModel.OnPostSaveSpamAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("Spam detection settings saved successfully");

        _mockConfigService.Verify(
            s => s.UpdateConfigAsync(
                guildId,
                It.Is<GuildModerationConfigDto>(c =>
                    c.SpamConfig.Enabled &&
                    c.SpamConfig.MaxMessagesPerWindow == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostSaveSpamAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new SpamDetectionConfigDto();

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _indexModel.OnPostSaveSpamAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region OnPostSaveContentAsync Tests

    [Fact]
    public async Task OnPostSaveContentAsync_WithValidRequest_SavesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new ContentFilterConfigDto
        {
            Enabled = true,
            ProhibitedWords = new List<string> { "spam", "scam" },
            AllowedLinkDomains = new List<string> { "example.com" },
            BlockUnlistedLinks = true,
            BlockInviteLinks = true,
            AutoAction = AutoAction.Delete
        };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(guildId, It.IsAny<GuildModerationConfigDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _indexModel.OnPostSaveContentAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();

        _mockConfigService.Verify(
            s => s.UpdateConfigAsync(
                guildId,
                It.Is<GuildModerationConfigDto>(c =>
                    c.ContentFilterConfig.Enabled &&
                    c.ContentFilterConfig.BlockInviteLinks &&
                    c.ContentFilterConfig.ProhibitedWords.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region OnPostSaveRaidAsync Tests

    [Fact]
    public async Task OnPostSaveRaidAsync_WithValidRequest_SavesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new RaidProtectionConfigDto
        {
            Enabled = true,
            MaxJoinsPerWindow = 20,
            WindowSeconds = 15,
            MinAccountAgeHours = 24,
            AutoAction = RaidAutoAction.LockInvites
        };

        _mockConfigService
            .Setup(s => s.GetConfigAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _mockConfigService
            .Setup(s => s.UpdateConfigAsync(guildId, It.IsAny<GuildModerationConfigDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _indexModel.OnPostSaveRaidAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();

        _mockConfigService.Verify(
            s => s.UpdateConfigAsync(
                guildId,
                It.Is<GuildModerationConfigDto>(c =>
                    c.RaidProtectionConfig.Enabled &&
                    c.RaidProtectionConfig.MinAccountAgeHours == 24),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region OnPostApplyPresetAsync Tests

    [Fact]
    public async Task OnPostApplyPresetAsync_WithValidPreset_AppliesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var config = CreateModerationConfig(guildId);
        var request = new ApplyPresetDto { PresetName = "Strict" };

        _mockConfigService
            .Setup(s => s.ApplyPresetAsync(guildId, "Strict", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _indexModel.OnPostApplyPresetAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("Preset 'Strict' applied successfully");

        _mockConfigService.Verify(
            s => s.ApplyPresetAsync(guildId, "Strict", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostApplyPresetAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new ApplyPresetDto { PresetName = "Invalid" };

        _mockConfigService
            .Setup(s => s.ApplyPresetAsync(guildId, "Invalid", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid preset name"));

        // Act
        var result = await _indexModel.OnPostApplyPresetAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region OnPostCreateTagAsync Tests

    [Fact]
    public async Task OnPostCreateTagAsync_WithValidRequest_CreatesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new ModTagCreateDto
        {
            Name = "Helpful",
            Color = "#00FF00",
            Category = TagCategory.Positive,
            Description = "User is helpful"
        };

        var createdTag = CreateModTagDto("Helpful", "#00FF00");

        _mockModTagService
            .Setup(s => s.CreateTagAsync(guildId, It.IsAny<ModTagCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTag);

        // Act
        var result = await _indexModel.OnPostCreateTagAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("Tag created successfully");

        _mockModTagService.Verify(
            s => s.CreateTagAsync(
                guildId,
                It.Is<ModTagCreateDto>(dto => dto.Name == "Helpful" && dto.GuildId == guildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostCreateTagAsync_SetsGuildIdInRequest()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new ModTagCreateDto
        {
            GuildId = 0, // Will be overridden
            Name = "Test"
        };

        var createdTag = CreateModTagDto("Test", "#FFFFFF");

        _mockModTagService
            .Setup(s => s.CreateTagAsync(It.IsAny<ulong>(), It.IsAny<ModTagCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTag);

        // Act
        await _indexModel.OnPostCreateTagAsync(request, CancellationToken.None);

        // Assert
        request.GuildId.Should().Be(guildId, "guild ID should be set from route parameter");
    }

    [Fact]
    public async Task OnPostCreateTagAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var request = new ModTagCreateDto { Name = "Test" };

        _mockModTagService
            .Setup(s => s.CreateTagAsync(guildId, It.IsAny<ModTagCreateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tag already exists"));

        // Act
        var result = await _indexModel.OnPostCreateTagAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region OnPostDeleteTagAsync Tests

    [Fact]
    public async Task OnPostDeleteTagAsync_WithExistingTag_DeletesAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const string tagName = "Helpful";
        _indexModel.GuildId = guildId;

        _mockModTagService
            .Setup(s => s.DeleteTagAsync(guildId, tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _indexModel.OnPostDeleteTagAsync(tagName, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("Tag deleted successfully");

        _mockModTagService.Verify(
            s => s.DeleteTagAsync(guildId, tagName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostDeleteTagAsync_WithNonExistentTag_Returns404()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const string tagName = "NonExistent";
        _indexModel.GuildId = guildId;

        _mockModTagService
            .Setup(s => s.DeleteTagAsync(guildId, tagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _indexModel.OnPostDeleteTagAsync(tagName, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(404);
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeFalse();
        ((string)response.message).Should().Contain("Tag not found");
    }

    [Fact]
    public async Task OnPostDeleteTagAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const string tagName = "Test";
        _indexModel.GuildId = guildId;

        _mockModTagService
            .Setup(s => s.DeleteTagAsync(guildId, tagName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _indexModel.OnPostDeleteTagAsync(tagName, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region OnPostImportTemplatesAsync Tests

    [Fact]
    public async Task OnPostImportTemplatesAsync_WithValidTemplates_ImportsAndReturnsSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var templateNames = new[] { "Spam Warning", "Helpful User", "Toxic Behavior" };

        _mockModTagService
            .Setup(s => s.ImportTemplateTagsAsync(guildId, templateNames, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _indexModel.OnPostImportTemplatesAsync(templateNames, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var response = jsonResult.Value as dynamic;
        ((bool)response!.success).Should().BeTrue();
        ((string)response.message).Should().Contain("3 template tags imported successfully");
        ((int)response.count).Should().Be(3);

        _mockModTagService.Verify(
            s => s.ImportTemplateTagsAsync(guildId, templateNames, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostImportTemplatesAsync_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _indexModel.GuildId = guildId;

        var templateNames = new[] { "Invalid" };

        _mockModTagService
            .Setup(s => s.ImportTemplateTagsAsync(guildId, templateNames, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Template not found"));

        // Act
        var result = await _indexModel.OnPostImportTemplatesAsync(templateNames, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        jsonResult.StatusCode.Should().Be(500);
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
