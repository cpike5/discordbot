using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// Unit tests for <see cref="DetailsModel"/> Razor Page. Data aggregation itself is
/// covered by <see cref="Services.Guilds.GuildDetailsAggregatorTests"/> — these tests
/// only exercise the page model's own routing and view-model assembly.
/// </summary>
public class DetailsModelTests
{
    private readonly Mock<IGuildDetailsAggregator> _mockAggregator;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<IGuildMembershipService> _mockGuildMembershipService;
    private readonly Mock<ILogger<DetailsModel>> _mockLogger;
    private readonly DetailsModel _detailsModel;

    public DetailsModelTests()
    {
        _mockAggregator = new Mock<IGuildDetailsAggregator>();
        _mockGuildService = new Mock<IGuildService>();
        _mockGuildMembershipService = new Mock<IGuildMembershipService>();
        _mockLogger = new Mock<ILogger<DetailsModel>>();

        _detailsModel = new DetailsModel(
            _mockAggregator.Object,
            _mockGuildService.Object,
            _mockGuildMembershipService.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1")
            }, "test"))
        };
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _detailsModel.PageContext = new PageContext(actionContext);
    }

    private static GuildDetailsAggregateDto BuildAggregate(ulong guildId, string name = "Test Guild")
    {
        return new GuildDetailsAggregateDto
        {
            Guild = new GuildDto
            {
                Id = guildId,
                Name = name,
                MemberCount = 150,
                IconUrl = null,
                IsActive = true,
                JoinedAt = DateTime.UtcNow.AddMonths(-3),
                Prefix = "!",
                Settings = null
            },
            RecentCommandLogs = new List<CommandLogDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    GuildId = guildId,
                    UserId = 111UL,
                    Username = "User1",
                    CommandName = "ping",
                    ExecutedAt = DateTime.UtcNow.AddMinutes(-10),
                    ResponseTimeMs = 50,
                    Success = true
                }
            }
        };
    }

    [Fact]
    public async Task OnGetAsync_WithValidGuildId_ReturnsPageWithViewModel()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var aggregate = BuildAggregate(guildId);

        _mockAggregator
            .Setup(a => a.BuildAsync(guildId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>("a valid guild should return PageResult");
        _detailsModel.ViewModel.Should().NotBeNull();
        _detailsModel.ViewModel.Id.Should().Be(guildId);
        _detailsModel.ViewModel.Name.Should().Be("Test Guild");
        _detailsModel.ViewModel.MemberCount.Should().Be(150);
        _detailsModel.ViewModel.RecentCommandLogs.Should().HaveCount(1);

        _mockAggregator.Verify(
            a => a.BuildAsync(guildId, 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WithInvalidGuildId_ReturnsNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockAggregator
            .Setup(a => a.BuildAsync(guildId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDetailsAggregateDto?)null);

        // Act
        var result = await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>("a non-existent guild should return NotFound");
    }

    [Fact]
    public async Task OnGetAsync_MapsAggregateWidgetDataOntoPageProperties()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var aggregate = BuildAggregate(guildId) with
        {
            WelcomeEnabled = true,
            RatWatchEnabled = true,
            RatWatchTotal = 5,
            AudioEnabled = true,
            TotalSoundCount = 12,
            AssistantLocallyEnabled = true,
            AssistantRateLimit = 20
        };

        _mockAggregator
            .Setup(a => a.BuildAsync(guildId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _detailsModel.WelcomeEnabled.Should().BeTrue();
        _detailsModel.RatWatchEnabled.Should().BeTrue();
        _detailsModel.RatWatchTotal.Should().Be(5);
        _detailsModel.AudioEnabled.Should().BeTrue();
        _detailsModel.TotalSoundCount.Should().Be(12);
        _detailsModel.AssistantLocallyEnabled.Should().BeTrue();
        _detailsModel.AssistantRateLimit.Should().Be(20);
    }

    [Fact]
    public async Task OnGetAsync_SetsCanEditWhenUserIsGuildAdmin()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var aggregate = BuildAggregate(guildId);

        _mockAggregator
            .Setup(a => a.BuildAsync(guildId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync("user-1", guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _detailsModel.OnGetAsync(guildId, CancellationToken.None);

        // Assert
        _detailsModel.ViewModel.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task OnPostSyncAsync_WhenSuccessful_ReturnsJsonWithSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _detailsModel.SuccessMessage.Should().Be("Guild synced successfully");
    }

    [Fact]
    public async Task OnPostSyncAsync_WhenGuildNotFound_DoesNotSetSuccessMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        _detailsModel.SuccessMessage.Should().BeNull();
    }
}
