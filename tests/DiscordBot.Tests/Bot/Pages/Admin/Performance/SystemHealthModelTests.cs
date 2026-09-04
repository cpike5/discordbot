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
/// Unit tests for <see cref="SystemHealthModel"/> Razor Page.
/// Verifies the page model routes to <see cref="IPerformanceDashboardAggregator.BuildSystemHealth"/>
/// and maps the result onto <see cref="SystemHealthModel.ViewModel"/> unchanged.
/// </summary>
public class SystemHealthModelTests
{
    private readonly Mock<IPerformanceDashboardAggregator> _mockAggregator;
    private readonly Mock<ILogger<SystemHealthModel>> _mockLogger;
    private readonly SystemHealthModel _model;

    public SystemHealthModelTests()
    {
        _mockAggregator = new Mock<IPerformanceDashboardAggregator>();
        _mockLogger = new Mock<ILogger<SystemHealthModel>>();

        _model = new SystemHealthModel(_mockAggregator.Object, _mockLogger.Object);

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
    public void OnGet_DelegatesToAggregatorAndMapsResult()
    {
        // Arrange
        var expected = new SystemHealthViewModel
        {
            SystemStatus = "Healthy",
            SystemStatusClass = "health-status-ok",
            DatabaseMetrics = new DatabaseMetricsDto { AvgQueryTimeMs = 5.0 },
            OverallCacheStats = new CacheStatisticsDto { Hits = 8, Misses = 2, HitRate = 80.0 },
            WorkingSetMB = 300
        };

        _mockAggregator.Setup(a => a.BuildSystemHealth()).Returns(expected);

        // Act
        _model.OnGet();

        // Assert
        _model.ViewModel.Should().BeSameAs(expected);
        _model.ViewModel.SystemStatus.Should().Be("Healthy");
        _model.ViewModel.OverallCacheStats.HitRate.Should().Be(80.0);
        _model.ViewModel.WorkingSetMB.Should().Be(300);

        _mockAggregator.Verify(a => a.BuildSystemHealth(), Times.Once);
    }

    [Fact]
    public void OnGet_LogsDebugMessage()
    {
        // Arrange
        _mockAggregator.Setup(a => a.BuildSystemHealth()).Returns(new SystemHealthViewModel());

        // Act
        _model.OnGet();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System Health page accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        var model = new SystemHealthModel(_mockAggregator.Object, _mockLogger.Object);

        model.ViewModel.Should().NotBeNull();
        model.ViewModel.Should().BeOfType<SystemHealthViewModel>();
    }
}
