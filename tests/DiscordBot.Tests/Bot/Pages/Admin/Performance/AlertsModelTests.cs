using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Pages.Admin.Performance;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Admin.Performance;

/// <summary>
/// Unit tests for <see cref="AlertsModel"/> Razor Page.
/// Verifies the page model routes to <see cref="IPerformanceDashboardAggregator.BuildAlertsPageAsync"/>
/// and maps the result (including CanEdit) onto <see cref="AlertsModel.ViewModel"/> unchanged.
/// </summary>
public class AlertsModelTests
{
    private readonly Mock<IPerformanceDashboardAggregator> _mockAggregator;
    private readonly Mock<ILogger<AlertsModel>> _mockLogger;
    private readonly AlertsModel _model;
    private readonly System.Security.Claims.ClaimsPrincipal _user;

    public AlertsModelTests()
    {
        _mockAggregator = new Mock<IPerformanceDashboardAggregator>();
        _mockLogger = new Mock<ILogger<AlertsModel>>();

        _model = new AlertsModel(_mockAggregator.Object, _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        _user = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "test-user") },
                "test"));
        httpContext.User = _user;

        var modelState = new ModelStateDictionary();
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        _model.PageContext = new PageContext(actionContext);
        _model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGetAsync_DelegatesToAggregatorAndMapsResultIncludingCanEdit()
    {
        // Arrange
        var expected = new AlertsPageViewModel
        {
            ActiveIncidents = new List<PerformanceIncidentDto>
            {
                new PerformanceIncidentDto { Id = Guid.NewGuid() }
            },
            AlertConfigs = Array.Empty<AlertConfigDto>(),
            RecentIncidents = Array.Empty<PerformanceIncidentDto>(),
            AutoRecoveryEvents = Array.Empty<AutoRecoveryEventDto>(),
            AlertFrequencyData = Array.Empty<AlertFrequencyDataDto>(),
            AlertSummary = new ActiveAlertSummaryDto(),
            CanEdit = true
        };

        _mockAggregator
            .Setup(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.ViewModel.Should().BeSameAs(expected);
        _model.ViewModel.ActiveIncidents.Should().HaveCount(1);
        _model.CanEdit.Should().BeTrue();

        _mockAggregator.Verify(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WhenAggregatorReturnsCanEditFalse_SetsCanEditFalse()
    {
        // Arrange
        _mockAggregator
            .Setup(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertsPageViewModel { CanEdit = false });

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task OnGetAsync_LogsDebugMessage()
    {
        // Arrange
        _mockAggregator
            .Setup(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertsPageViewModel());

        // Act
        await _model.OnGetAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Alerts page accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WhenAggregatorReportsLoadFailed_SetsErrorMessageInTempData()
    {
        // Arrange
        _mockAggregator
            .Setup(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertsPageViewModel { LoadFailed = true });

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.TempData["ErrorMessage"].Should().Be("Failed to load alerts data. Please try again.");
    }

    [Fact]
    public async Task OnGetAsync_WhenAggregatorSucceeds_DoesNotSetErrorMessageInTempData()
    {
        // Arrange
        _mockAggregator
            .Setup(a => a.BuildAlertsPageAsync(_user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertsPageViewModel { LoadFailed = false });

        // Act
        await _model.OnGetAsync();

        // Assert
        _model.TempData.Should().NotContainKey("ErrorMessage");
    }

    [Fact]
    public void Constructor_InitializesViewModelProperty()
    {
        var model = new AlertsModel(_mockAggregator.Object, _mockLogger.Object);

        model.ViewModel.Should().NotBeNull();
        model.ViewModel.Should().BeOfType<AlertsPageViewModel>();
    }
}
