using Discord.WebSocket;
using DiscordBot.Bot.Pages.Guilds.Analytics;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.Analytics;

/// <summary>
/// Unit tests for the <see cref="IndexModel"/> Razor Page (Guilds/Analytics).
/// Covers GET handler behaviour, layout view model population, and not-found handling.
/// Note: <see cref="DiscordSocketClient"/> is used by the page to resolve channel names.
/// When the mock returns null for GetGuild (default), the page falls back to "Unknown Channel".
/// </summary>
public class IndexModelTests
{
    private readonly Mock<IServerAnalyticsService> _mockAnalyticsService;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<IMessageLogRepository> _mockMessageLogRepository;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IDiscordChannelResolver> _mockChannelResolver;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _indexModel;

    public IndexModelTests()
    {
        _mockAnalyticsService = new Mock<IServerAnalyticsService>();
        _mockGuildService = new Mock<IGuildService>();
        _mockMessageLogRepository = new Mock<IMessageLogRepository>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(new DiscordSocketConfig());
        _mockChannelResolver = new Mock<IDiscordChannelResolver>();
        _mockLogger = new Mock<ILogger<IndexModel>>();

        // Default happy-path setup — return empty analytics data
        _mockAnalyticsService
            .Setup(s => s.GetSummaryAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerAnalyticsSummaryDto());

        _mockAnalyticsService
            .Setup(s => s.GetActivityTimeSeriesAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActivityTimeSeriesDto>());

        _mockAnalyticsService
            .Setup(s => s.GetActivityHeatmapAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServerActivityHeatmapDto>());

        _mockAnalyticsService
            .Setup(s => s.GetTopContributorsAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopContributorDto>());

        // Message log repository returns empty collection (used when fetching top channels)
        _mockMessageLogRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageLog>());

        _indexModel = new IndexModel(
            _mockAnalyticsService.Object,
            _mockGuildService.Object,
            _mockMessageLogRepository.Object,
            _mockChannelResolver.Object,
            _mockLogger.Object);

        // Wire up a minimal PageContext so RazorPage helper methods work
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _indexModel.PageContext = new PageContext(actionContext);

        // TempData is required by the catch block in OnGetAsync when analytics loading fails
        _indexModel.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    // -----------------------------------------------------------------
    // OnGetAsync — happy path
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_ReturnsPageResult()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        var result = await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("a found guild should return a PageResult");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesBreadcrumb()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.Breadcrumb.Should().NotBeNull("Breadcrumb must be set for the guild layout");
        _indexModel.Breadcrumb.Items.Should().NotBeEmpty("Breadcrumb must contain navigation items");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesHeader()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.Header.Should().NotBeNull("Header must be set for the guild layout");
        _indexModel.Header.GuildId.Should().Be(guildId, "Header must reference the correct guild");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesNavigation()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.Navigation.Should().NotBeNull("Navigation must be set for the guild layout");
        _indexModel.Navigation.GuildId.Should().Be(guildId, "Navigation must reference the correct guild");
        _indexModel.Navigation.Tabs.Should().NotBeEmpty("Navigation must include tab definitions");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesViewModel()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Analytics Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.ViewModel.Should().NotBeNull("ViewModel must be populated after OnGetAsync");
        _indexModel.ViewModel.GuildId.Should().Be(guildId, "ViewModel must reference the correct guild");
    }

    // -----------------------------------------------------------------
    // OnGetAsync — not-found path
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithUnknownGuildId_ReturnsNotFound()
    {
        // Arrange
        const ulong unknownGuildId = 999999999UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(unknownGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _indexModel.OnGetAsync(unknownGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("a missing guild should return NotFound");
    }

    [Fact]
    public async Task OnGetAsync_WithUnknownGuildId_DoesNotCallAnalyticsService()
    {
        // Arrange
        const ulong unknownGuildId = 999999999UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(unknownGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        await _indexModel.OnGetAsync(unknownGuildId, CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetSummaryAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "analytics service must not be called when guild is not found");
    }

    // -----------------------------------------------------------------
    // OnGetAsync — service interaction
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_FetchesAnalyticsSummary()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetSummaryAsync(guildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "analytics summary must be fetched once for the guild");
    }

    [Fact]
    public async Task OnGetAsync_WhenAnalyticsServiceThrows_StillReturnsPageResult()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        _mockAnalyticsService
            .Setup(s => s.GetSummaryAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Analytics unavailable"));

        // Act
        // The page catches all exceptions in OnGetAsync and returns Page() with empty data
        var result = await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("the page should handle analytics exceptions gracefully and still return PageResult");
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static GuildDto CreateGuildDto(ulong id, string name) => new()
    {
        Id = id,
        Name = name,
        IsActive = true,
        JoinedAt = DateTime.UtcNow.AddMonths(-6),
        IconUrl = null
    };
}
