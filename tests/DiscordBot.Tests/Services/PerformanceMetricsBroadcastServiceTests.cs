using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using static DiscordBot.Core.Interfaces.GatewayConnectionState;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PerformanceMetricsBroadcastService"/>.
/// Tests broadcast behavior, subscription-based skipping, and configuration options.
/// </summary>
public class PerformanceMetricsBroadcastServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly Mock<IPerformanceSubscriptionTracker> _mockSubscriptionTracker;
    private readonly Mock<ILatencyHistoryService> _mockLatencyHistoryService;
    private readonly Mock<IConnectionStateService> _mockConnectionStateService;
    private readonly Mock<ICommandPerformanceAggregator> _mockCommandPerformanceAggregator;
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockBackgroundServiceHealthRegistry;
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache;
    private readonly Mock<ILogger<PerformanceMetricsBroadcastService>> _mockLogger;

    public PerformanceMetricsBroadcastServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _mockSubscriptionTracker = new Mock<IPerformanceSubscriptionTracker>();
        _mockLatencyHistoryService = new Mock<ILatencyHistoryService>();
        _mockConnectionStateService = new Mock<IConnectionStateService>();
        _mockCommandPerformanceAggregator = new Mock<ICommandPerformanceAggregator>();
        _mockDatabaseMetricsCollector = new Mock<IDatabaseMetricsCollector>();
        _mockBackgroundServiceHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockInstrumentedCache = new Mock<IInstrumentedCache>();
        _mockLogger = new Mock<ILogger<PerformanceMetricsBroadcastService>>();

        // Default setup for metrics services
        _mockLatencyHistoryService.Setup(x => x.GetCurrentLatency()).Returns(50);
        _mockConnectionStateService.Setup(x => x.GetCurrentState())
            .Returns(GatewayConnectionState.Connected);
        _mockCommandPerformanceAggregator.Setup(x => x.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<CommandPerformanceAggregateDto>());
        _mockDatabaseMetricsCollector.Setup(x => x.GetMetrics())
            .Returns(new DatabaseMetricsDto());
        _mockBackgroundServiceHealthRegistry.Setup(x => x.GetAllHealth())
            .Returns(Array.Empty<BackgroundServiceHealthDto>());
        _mockInstrumentedCache.Setup(x => x.GetStatistics())
            .Returns(Array.Empty<CacheStatisticsDto>());
    }

    private PerformanceMetricsBroadcastService CreateService(PerformanceBroadcastOptions? options = null)
    {
        options ??= new PerformanceBroadcastOptions
        {
            Enabled = true,
            HealthMetricsIntervalSeconds = 1,
            CommandMetricsIntervalSeconds = 1,
            SystemMetricsIntervalSeconds = 1
        };

        return new PerformanceMetricsBroadcastService(
            _mockServiceProvider.Object,
            _mockHubContext.Object,
            _mockSubscriptionTracker.Object,
            _mockLatencyHistoryService.Object,
            _mockConnectionStateService.Object,
            _mockCommandPerformanceAggregator.Object,
            _mockDatabaseMetricsCollector.Object,
            _mockBackgroundServiceHealthRegistry.Object,
            _mockInstrumentedCache.Object,
            Options.Create(options),
            _mockLogger.Object);
    }

    [Fact]
    public void ServiceName_ShouldReturnExpectedName()
    {
        // Arrange
        var service = CreateService();

        // Assert
        service.ServiceName.Should().Be("Performance Metrics Broadcast Service");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ShouldNotBroadcast()
    {
        // Arrange
        var options = new PerformanceBroadcastOptions { Enabled = false };
        var service = CreateService(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token).ContinueWith(_ => { }); // Give time to not broadcast

        // Assert - No SignalR calls should be made
        _mockHubContext.Verify(
            x => x.Clients,
            Times.Never,
            "Should not access SignalR clients when disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPerformanceSubscribers_ShouldNotBroadcastHealthMetrics()
    {
        // Arrange
        _mockSubscriptionTracker.Setup(x => x.PerformanceGroupClientCount).Returns(0);
        _mockSubscriptionTracker.Setup(x => x.SystemHealthGroupClientCount).Returns(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Setup hub context mock
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for at least one tick
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should not broadcast when no subscribers
        mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "HealthMetricsUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not broadcast health metrics when no subscribers");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPerformanceSubscribersExist_ShouldBroadcastHealthMetrics()
    {
        // Arrange
        _mockSubscriptionTracker.Setup(x => x.PerformanceGroupClientCount).Returns(1);
        _mockSubscriptionTracker.Setup(x => x.SystemHealthGroupClientCount).Returns(0);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(x => x.Group(DashboardHub.PerformanceGroupName)).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for at least one tick
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "HealthMetricsUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Should broadcast health metrics when subscribers exist");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSystemHealthSubscribersExist_ShouldBroadcastSystemMetrics()
    {
        // Arrange
        _mockSubscriptionTracker.Setup(x => x.PerformanceGroupClientCount).Returns(0);
        _mockSubscriptionTracker.Setup(x => x.SystemHealthGroupClientCount).Returns(1);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(x => x.Group(DashboardHub.SystemHealthGroupName)).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for at least one tick
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "SystemMetricsUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Should broadcast system metrics when subscribers exist");
    }

    [Fact]
    public void Options_ShouldHaveSensibleDefaults()
    {
        // Arrange
        var options = new PerformanceBroadcastOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.HealthMetricsIntervalSeconds.Should().Be(5);
        options.CommandMetricsIntervalSeconds.Should().Be(30);
        options.SystemMetricsIntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void Options_SectionName_ShouldBeCorrect()
    {
        // Assert
        PerformanceBroadcastOptions.SectionName.Should().Be("PerformanceBroadcast");
    }
}
