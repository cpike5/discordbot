using Discord.WebSocket;
using DiscordBot.Bot.Pages.Guilds.Reminders;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
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

namespace DiscordBot.Tests.Bot.Pages.Guilds.Reminders;

/// <summary>
/// Unit tests for the <see cref="IndexModel"/> Razor Page (Guilds/Reminders).
/// Covers GET handler behaviour, layout view model population, and not-found handling.
/// </summary>
public class IndexModelTests
{
    private readonly Mock<IReminderRepository> _mockReminderRepository;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _indexModel;

    public IndexModelTests()
    {
        _mockReminderRepository = new Mock<IReminderRepository>();
        _mockGuildService = new Mock<IGuildService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(new DiscordSocketConfig());
        _mockLogger = new Mock<ILogger<IndexModel>>();

        // Default happy-path setup — empty reminder list
        _mockReminderRepository
            .Setup(r => r.GetByGuildAsync(
                It.IsAny<ulong>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ReminderStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Reminder>(), 0));

        _mockReminderRepository
            .Setup(r => r.GetGuildStatsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, 0, 0));

        // DiscordSocketClient.GetGuild returns null by default (no setup needed; mock returns null)

        _indexModel = new IndexModel(
            _mockReminderRepository.Object,
            _mockGuildService.Object,
            _mockDiscordClient.Object,
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
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

        // Act
        var result = await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("a found guild should return a PageResult");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesBreadcrumb()
    {
        // Arrange
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

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
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.Header.Should().NotBeNull("Header must be set for the guild layout");
        _indexModel.Header.GuildId.Should().Be((ulong)guildId, "Header must reference the correct guild");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesNavigation()
    {
        // Arrange
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.Navigation.Should().NotBeNull("Navigation must be set for the guild layout");
        _indexModel.Navigation.GuildId.Should().Be((ulong)guildId, "Navigation must reference the correct guild");
        _indexModel.Navigation.Tabs.Should().NotBeEmpty("Navigation must include tab definitions");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_PopulatesViewModel()
    {
        // Arrange
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Reminder Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _indexModel.ViewModel.Should().NotBeNull("ViewModel must be populated after OnGetAsync");
        _indexModel.ViewModel.GuildId.Should().Be((ulong)guildId, "ViewModel must reference the correct guild");
    }

    // -----------------------------------------------------------------
    // OnGetAsync — not-found path
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithUnknownGuildId_ReturnsNotFound()
    {
        // Arrange
        const long unknownGuildId = 999999999L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)unknownGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _indexModel.OnGetAsync(unknownGuildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("a missing guild should return NotFound");
    }

    [Fact]
    public async Task OnGetAsync_WithUnknownGuildId_DoesNotQueryReminderRepository()
    {
        // Arrange
        const long unknownGuildId = 999999999L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)unknownGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        await _indexModel.OnGetAsync(unknownGuildId, CancellationToken.None);

        // Assert
        _mockReminderRepository.Verify(
            r => r.GetByGuildAsync(
                It.IsAny<ulong>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ReminderStatus?>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "reminder repository must not be queried when guild is not found");
    }

    // -----------------------------------------------------------------
    // OnGetAsync — service interaction
    // -----------------------------------------------------------------

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_FetchesGuildStats()
    {
        // Arrange
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockReminderRepository.Verify(
            r => r.GetGuildStatsAsync((ulong)guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild stats must be fetched once for the reminders page");
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_FetchesReminders()
    {
        // Arrange
        const long guildId = 111222333L;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync((ulong)guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGuildDto((ulong)guildId, "Test Guild"));

        // Act
        await _indexModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _mockReminderRepository.Verify(
            r => r.GetByGuildAsync(
                (ulong)guildId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ReminderStatus?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "reminders must be fetched once for the guild");
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
