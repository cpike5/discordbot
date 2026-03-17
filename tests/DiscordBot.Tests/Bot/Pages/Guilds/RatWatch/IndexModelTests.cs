using DiscordBot.Bot.Pages.Guilds.RatWatch;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// Unit tests for the <see cref="IndexModel"/> Razor Page (Guilds/RatWatch).
/// Covers GET handler behaviour, layout view model population, and not-found handling.
/// </summary>
public class IndexModelTests
{
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<IRatWatchRepository> _mockRatWatchRepository;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _indexModel;

    public IndexModelTests()
    {
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockGuildService = new Mock<IGuildService>();
        _mockRatWatchRepository = new Mock<IRatWatchRepository>();
        _mockLogger = new Mock<ILogger<IndexModel>>();

        // Default happy-path setup
        _mockRatWatchService
            .Setup(s => s.GetGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { IsEnabled = true, Timezone = "UTC" });

        _mockRatWatchService
            .Setup(s => s.GetByGuildAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<RatWatchDto>(), 0));

        _mockRatWatchService
            .Setup(s => s.GetLeaderboardAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RatLeaderboardEntryDto>());

        _mockRatWatchRepository
            .Setup(r => r.GetAnalyticsSummaryAsync(
                It.IsAny<ulong?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatWatchAnalyticsSummaryDto());

        _indexModel = new IndexModel(
            _mockRatWatchService.Object,
            _mockGuildService.Object,
            _mockRatWatchRepository.Object,
            _mockLogger.Object);

        // Wire up a minimal PageContext so RazorPage helper methods work
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _indexModel.PageContext = new PageContext(actionContext);
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
        var result = await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

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
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

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
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

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
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

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
            .ReturnsAsync(CreateGuildDto(guildId, "Rat Watch Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

        // Assert
        _indexModel.ViewModel.Should().NotBeNull("ViewModel must be populated after OnGetAsync");
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
        var result = await _indexModel.OnGetAsync(unknownGuildId, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("a missing guild should return NotFound");
    }

    [Fact]
    public async Task OnGetAsync_WithUnknownGuildId_DoesNotCallRatWatchService()
    {
        // Arrange
        const ulong unknownGuildId = 999999999UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(unknownGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        await _indexModel.OnGetAsync(unknownGuildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockRatWatchService.Verify(
            s => s.GetGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "rat watch service must not be called when guild is not found");
    }

    // -----------------------------------------------------------------
    // OnGetAsync — service interaction
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_FetchesRatWatchSettings()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockRatWatchService.Verify(
            s => s.GetGuildSettingsAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "rat watch settings must be fetched once for the guild");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_FetchesAnalyticsSummary()
    {
        // Arrange
        const ulong guildId = 111222333UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto(guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockRatWatchRepository.Verify(
            r => r.GetAnalyticsSummaryAsync(guildId, null, null, It.IsAny<CancellationToken>()),
            Times.Once,
            "analytics summary must be fetched for the guild");
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
