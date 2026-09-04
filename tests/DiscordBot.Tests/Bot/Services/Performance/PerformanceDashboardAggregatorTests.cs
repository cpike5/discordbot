using DiscordBot.Bot.Services.Performance;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace DiscordBot.Tests.Bot.Services.Performance;

/// <summary>
/// Unit tests for <see cref="PerformanceDashboardAggregator"/>.
/// </summary>
public class PerformanceDashboardAggregatorTests
{
    private readonly Mock<IConnectionStateService> _mockConnectionStateService = new();
    private readonly Mock<ILatencyHistoryService> _mockLatencyHistoryService = new();
    private readonly Mock<ICommandPerformanceAggregator> _mockCommandPerformanceAggregator = new();
    private readonly Mock<IApiRequestTracker> _mockApiRequestTracker = new();
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockBackgroundServiceHealthRegistry = new();
    private readonly Mock<IPerformanceAlertService> _mockAlertService = new();
    private readonly Mock<ICpuHistoryService> _mockCpuHistoryService = new();
    private readonly Mock<IMemoryDiagnosticsService> _mockMemoryDiagnosticsService = new();
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector = new();
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache = new();
    private readonly Mock<IAuthorizationService> _mockAuthorizationService = new();
    private readonly PerformanceDashboardAggregator _aggregator;

    public PerformanceDashboardAggregatorTests()
    {
        _aggregator = new PerformanceDashboardAggregator(
            _mockConnectionStateService.Object,
            _mockLatencyHistoryService.Object,
            _mockCommandPerformanceAggregator.Object,
            _mockApiRequestTracker.Object,
            _mockBackgroundServiceHealthRegistry.Object,
            _mockAlertService.Object,
            _mockCpuHistoryService.Object,
            _mockMemoryDiagnosticsService.Object,
            _mockDatabaseMetricsCollector.Object,
            _mockInstrumentedCache.Object,
            _mockAuthorizationService.Object,
            Mock.Of<ILogger<PerformanceDashboardAggregator>>());

        _mockConnectionStateService.Setup(s => s.GetCurrentState()).Returns(GatewayConnectionState.Connected);
        _mockConnectionStateService.Setup(s => s.GetCurrentSessionDuration()).Returns(TimeSpan.FromHours(2));
        _mockConnectionStateService.Setup(s => s.GetUptimePercentage(It.IsAny<TimeSpan>())).Returns(99.9);
        _mockLatencyHistoryService.Setup(s => s.GetCurrentLatency()).Returns(42);
        _mockBackgroundServiceHealthRegistry.Setup(s => s.GetOverallStatus()).Returns("Healthy");
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<CommandPerformanceAggregateDto>());
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetThroughputAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<CommandThroughputDto>());
        _mockAlertService
            .Setup(a => a.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PerformanceIncidentDto>());
        _mockApiRequestTracker.Setup(t => t.GetUsageStatistics(It.IsAny<int>())).Returns(Array.Empty<ApiUsageDto>());
        _mockApiRequestTracker.Setup(t => t.GetRateLimitEvents(It.IsAny<int>())).Returns(Array.Empty<RateLimitEventDto>());
        _mockCpuHistoryService.Setup(s => s.GetCurrentCpu()).Returns(5.0);
    }

    [Fact]
    public async Task BuildOverviewAsync_WhenDataAvailable_ReturnsHealthyOverviewAndShell()
    {
        // Act
        var result = await _aggregator.BuildOverviewAsync();

        // Assert
        result.Overview.OverallStatus.Should().Be("Healthy");
        result.Shell.OverallStatus.Should().Be("Healthy");
        result.Shell.IsLive.Should().BeTrue();
        result.Shell.ActiveTab.Should().Be("overview");
    }

    [Fact]
    public async Task BuildOverviewAsync_ForwardsHoursToCommandAggregatesAndShellTimeRange()
    {
        // Act
        var result = await _aggregator.BuildOverviewAsync(168);

        // Assert
        _mockCommandPerformanceAggregator.Verify(a => a.GetAggregatesAsync(168), Times.Once);
        result.Shell.TimeRangeHours.Should().Be(168);
    }

    [Fact]
    public async Task BuildOverviewAsync_WhenAnUpstreamCallThrows_ReturnsCriticalFallback()
    {
        // Arrange
        _mockAlertService
            .Setup(a => a.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var result = await _aggregator.BuildOverviewAsync();

        // Assert
        result.Overview.OverallStatus.Should().Be("Critical");
        result.Shell.OverallStatus.Should().Be("Critical");
        result.Shell.IsLive.Should().BeFalse();
    }

    [Fact]
    public async Task BuildAlertsPageAsync_WhenUserIsAdmin_SetsCanEditTrue()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(user, null, "RequireAdmin"))
            .ReturnsAsync(AuthorizationResult.Success());
        _mockAlertService
            .Setup(a => a.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PerformanceIncidentDto>());
        _mockAlertService
            .Setup(a => a.GetAllConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AlertConfigDto>());
        _mockAlertService
            .Setup(a => a.GetIncidentHistoryAsync(It.IsAny<IncidentQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncidentPagedResultDto { Items = new List<PerformanceIncidentDto>() });
        _mockAlertService
            .Setup(a => a.GetAutoRecoveryEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AutoRecoveryEventDto>());
        _mockAlertService
            .Setup(a => a.GetAlertFrequencyDataAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AlertFrequencyDataDto>());
        _mockAlertService
            .Setup(a => a.GetActiveAlertSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActiveAlertSummaryDto());

        // Act
        var result = await _aggregator.BuildAlertsPageAsync(user);

        // Assert
        result.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAlertsPageAsync_WhenAlertServiceThrows_ReturnsEmptyViewModel()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(user, null, "RequireAdmin"))
            .ReturnsAsync(AuthorizationResult.Success());
        _mockAlertService
            .Setup(a => a.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var result = await _aggregator.BuildAlertsPageAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.CanEdit.Should().BeFalse();
        result.LoadFailed.Should().BeTrue("the page model surfaces this as a TempData error banner");
    }

    [Fact]
    public void BuildSystemHealth_WhenDataAvailable_ComputesOverallCacheStats()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(d => d.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockDatabaseMetricsCollector.Setup(d => d.GetSlowQueries(It.IsAny<int>())).Returns(Array.Empty<SlowQueryDto>());
        _mockBackgroundServiceHealthRegistry.Setup(r => r.GetAllHealth()).Returns(Array.Empty<BackgroundServiceHealthDto>());
        _mockInstrumentedCache.Setup(c => c.GetStatistics()).Returns(new List<CacheStatisticsDto>
        {
            new() { KeyPrefix = "guilds", Hits = 8, Misses = 2, Size = 10 }
        });

        // Act
        var result = _aggregator.BuildSystemHealth();

        // Assert
        result.OverallCacheStats.Hits.Should().Be(8);
        result.OverallCacheStats.Misses.Should().Be(2);
        result.OverallCacheStats.HitRate.Should().BeApproximately(80.0, 0.01);
    }

    [Fact]
    public void BuildSystemHealth_WhenCacheStatisticsThrows_ReturnsErrorFallback()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(d => d.GetMetrics()).Throws(new InvalidOperationException("boom"));

        // Act
        var result = _aggregator.BuildSystemHealth();

        // Assert
        result.SystemStatus.Should().Be("Error Loading Data");
    }
}
