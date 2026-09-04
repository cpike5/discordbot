using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Admin.Performance;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Admin.Performance;

/// <summary>
/// Unit tests for <see cref="CommandsModel"/> Razor Page.
/// Verifies the page model routes to <see cref="IPerformanceDashboardAggregator.BuildCommandPerformanceAsync"/>
/// and maps the result onto <see cref="CommandsModel.ViewModel"/> unchanged.
/// </summary>
public class CommandsModelTests
{
    private readonly Mock<IPerformanceDashboardAggregator> _mockAggregator;
    private readonly Mock<ILogger<CommandsModel>> _mockLogger;
    private readonly CommandsModel _commandsModel;

    public CommandsModelTests()
    {
        _mockAggregator = new Mock<IPerformanceDashboardAggregator>();
        _mockLogger = new Mock<ILogger<CommandsModel>>();

        _commandsModel = new CommandsModel(
            _mockAggregator.Object,
            _mockLogger.Object);

        // Setup PageContext
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "test-user") },
                "test"));

        var modelState = new ModelStateDictionary();
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _commandsModel.PageContext = pageContext;
    }

    [Fact]
    public async Task OnGetAsync_WithDefault24Hours_DelegatesToAggregatorAndMapsResult()
    {
        // Arrange
        var expected = new CommandPerformanceViewModel
        {
            TotalCommands = 150,
            AvgResponseTimeMs = 62.75,
            ErrorRate = 2.0,
            P99ResponseTimeMs = 180.0,
            P95Ms = 130.0,
            P50Ms = 57.5,
            SlowestCommands = new List<SlowestCommandDto>
            {
                new SlowestCommandDto { CommandName = "slow-command", ExecutedAt = DateTime.UtcNow, DurationMs = 2500.0, UserId = 123UL, GuildId = 456UL }
            },
            TimeoutCount = 0,
            RecentTimeouts = Array.Empty<CommandTimeoutDto>(),
            AvgResponseTimeTrend = 0,
            ErrorRateTrend = 0,
            P99Trend = 0
        };

        _mockAggregator
            .Setup(a => a.BuildCommandPerformanceAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _commandsModel.OnGetAsync();

        // Assert - the page model's ViewModel is exactly what the aggregator returned
        _commandsModel.ViewModel.Should().BeSameAs(expected);
        _commandsModel.ViewModel.TotalCommands.Should().Be(150);
        _commandsModel.ViewModel.AvgResponseTimeMs.Should().Be(62.75);
        _commandsModel.ViewModel.ErrorRate.Should().Be(2.0);
        _commandsModel.ViewModel.P99ResponseTimeMs.Should().Be(180.0);
        _commandsModel.ViewModel.P95Ms.Should().Be(130.0);
        _commandsModel.ViewModel.P50Ms.Should().Be(57.5);
        _commandsModel.ViewModel.SlowestCommands.Should().HaveCount(1);

        _mockAggregator.Verify(a => a.BuildCommandPerformanceAsync(24, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_With7Days_PassesHoursThrough()
    {
        // Arrange
        _commandsModel.Hours = 168; // 7 days

        _mockAggregator
            .Setup(a => a.BuildCommandPerformanceAsync(168, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandPerformanceViewModel());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockAggregator.Verify(a => a.BuildCommandPerformanceAsync(168, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_With30Days_PassesHoursThrough()
    {
        // Arrange
        _commandsModel.Hours = 720; // 30 days

        _mockAggregator
            .Setup(a => a.BuildCommandPerformanceAsync(720, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandPerformanceViewModel());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockAggregator.Verify(a => a.BuildCommandPerformanceAsync(720, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        // Arrange & Act
        var model = new CommandsModel(
            _mockAggregator.Object,
            _mockLogger.Object);

        // Assert
        model.ViewModel.Should().NotBeNull("ViewModel should be initialized");
        model.ViewModel.Should().BeOfType<CommandPerformanceViewModel>();
    }

    [Fact]
    public void Hours_DefaultsTo24()
    {
        // Arrange & Act
        var model = new CommandsModel(
            _mockAggregator.Object,
            _mockLogger.Object);

        // Assert
        model.Hours.Should().Be(24, "default hours should be 24");
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessage()
    {
        // Arrange
        _mockAggregator
            .Setup(a => a.BuildCommandPerformanceAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandPerformanceViewModel());

        // Act
        await _commandsModel.OnGetAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command Performance page accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when page is accessed");
    }
}
