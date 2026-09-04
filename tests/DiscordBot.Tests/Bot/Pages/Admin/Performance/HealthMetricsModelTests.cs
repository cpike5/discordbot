using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Admin.Performance;
using DiscordBot.Bot.ViewModels.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Admin.Performance;

/// <summary>
/// Unit tests for <see cref="HealthMetricsModel"/> Razor Page.
/// Verifies the page model routes to <see cref="IPerformanceDashboardAggregator.BuildHealthMetricsAsync"/>
/// and maps the result onto <see cref="HealthMetricsModel.ViewModel"/> unchanged.
/// </summary>
public class HealthMetricsModelTests
{
    private readonly Mock<IPerformanceDashboardAggregator> _mockAggregator;
    private readonly Mock<ILogger<HealthMetricsModel>> _mockLogger;
    private readonly HealthMetricsModel _model;

    public HealthMetricsModelTests()
    {
        _mockAggregator = new Mock<IPerformanceDashboardAggregator>();
        _mockLogger = new Mock<ILogger<HealthMetricsModel>>();

        _model = new HealthMetricsModel(_mockAggregator.Object, _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "test-user") },
                "test"));

        var modelState = new ModelStateDictionary();
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _model.PageContext = new PageContext(actionContext);
    }

    [Fact]
    public async Task OnGetAsync_DelegatesToAggregatorAndMapsResult()
    {
        // Arrange
        var expected = new HealthMetricsViewModel
        {
            UptimeFormatted = "2h 0m",
            Uptime24HFormatted = "99.9%",
            Uptime7DFormatted = "99.5%",
            Uptime30DFormatted = "99.0%",
            ConnectionStateClass = "health-status-ok",
            LatencyHealthClass = "gauge-fill-ok",
            SessionStartFormatted = "Sep 04, 2026 at 12:00 UTC",
            WorkingSetMB = 256,
            ThreadCount = 12
        };

        _mockAggregator.Setup(a => a.BuildHealthMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.ViewModel.Should().BeSameAs(expected);
        _model.ViewModel.UptimeFormatted.Should().Be("2h 0m");
        _model.ViewModel.WorkingSetMB.Should().Be(256);
        _model.ViewModel.ThreadCount.Should().Be(12);

        _mockAggregator.Verify(a => a.BuildHealthMetricsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessage()
    {
        // Arrange
        _mockAggregator.Setup(a => a.BuildHealthMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HealthMetricsViewModel());

        // Act
        await _model.OnGetAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Health Metrics page accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        var model = new HealthMetricsModel(_mockAggregator.Object, _mockLogger.Object);

        model.ViewModel.Should().NotBeNull();
        model.ViewModel.Should().BeOfType<HealthMetricsViewModel>();
    }
}
