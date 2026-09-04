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
/// Unit tests for <see cref="ApiMetricsModel"/> Razor Page.
/// Verifies the page model routes to <see cref="IPerformanceDashboardAggregator.BuildApiRateLimits"/>
/// and maps the result onto <see cref="ApiMetricsModel.ViewModel"/> unchanged.
/// </summary>
public class ApiMetricsModelTests
{
    private readonly Mock<IPerformanceDashboardAggregator> _mockAggregator;
    private readonly Mock<ILogger<ApiMetricsModel>> _mockLogger;
    private readonly ApiMetricsModel _model;

    public ApiMetricsModelTests()
    {
        _mockAggregator = new Mock<IPerformanceDashboardAggregator>();
        _mockLogger = new Mock<ILogger<ApiMetricsModel>>();

        _model = new ApiMetricsModel(_mockAggregator.Object, _mockLogger.Object);

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
    public async Task OnGetAsync_WithDefault24Hours_DelegatesToAggregatorAndMapsResult()
    {
        // Arrange
        var expected = new ApiRateLimitsViewModel
        {
            TotalRequests = 500,
            RateLimitHits = 3,
            AvgLatencyMs = 120.0,
            P95LatencyMs = 300.0,
            UsageByCategory = Array.Empty<ApiUsageDto>(),
            RecentRateLimitEvents = Array.Empty<RateLimitEventDto>(),
            Hours = 24
        };

        _mockAggregator.Setup(a => a.BuildApiRateLimits(24)).Returns(expected);

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.ViewModel.Should().BeSameAs(expected);
        _model.ViewModel.TotalRequests.Should().Be(500);
        _model.ViewModel.RateLimitHits.Should().Be(3);
        _model.ViewModel.Hours.Should().Be(24);

        _mockAggregator.Verify(a => a.BuildApiRateLimits(24), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WithCustomHours_PassesHoursThrough()
    {
        // Arrange
        _model.Hours = 168;
        _mockAggregator.Setup(a => a.BuildApiRateLimits(168)).Returns(new ApiRateLimitsViewModel());

        // Act
        await _model.OnGetAsync();

        // Assert
        _mockAggregator.Verify(a => a.BuildApiRateLimits(168), Times.Once);
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        var model = new ApiMetricsModel(_mockAggregator.Object, _mockLogger.Object);

        model.ViewModel.Should().NotBeNull();
        model.ViewModel.Should().BeOfType<ApiRateLimitsViewModel>();
    }

    [Fact]
    public void Hours_DefaultsTo24()
    {
        var model = new ApiMetricsModel(_mockAggregator.Object, _mockLogger.Object);

        model.Hours.Should().Be(24);
    }
}
