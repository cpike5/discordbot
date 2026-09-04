using DiscordBot.Bot.Services.Dashboard;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Dashboard;

/// <summary>
/// Unit tests for <see cref="DashboardMetricsService"/>.
/// Moved out of DashboardHubTests when DashboardHub was split into per-feature services.
/// </summary>
public class DashboardMetricsServiceTests
{
    private readonly Mock<IBotService> _mockBotService;
    private readonly Mock<IConnectionStateService> _mockConnectionStateService;
    private readonly Mock<ILatencyHistoryService> _mockLatencyHistoryService;
    private readonly Mock<IPerformanceAlertService> _mockAlertService;
    private readonly Mock<ICommandPerformanceAggregator> _mockCommandPerformanceAggregator;
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockBackgroundServiceHealthRegistry;
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache;
    private readonly Mock<ICpuHistoryService> _mockCpuHistoryService;
    private readonly Mock<ILogger<DashboardMetricsService>> _mockLogger;
    private readonly DashboardMetricsService _service;

    private const string ConnectionId = "test-connection-id-123";
    private const string UserName = "testuser";

    public DashboardMetricsServiceTests()
    {
        _mockBotService = new Mock<IBotService>();
        _mockConnectionStateService = new Mock<IConnectionStateService>();
        _mockLatencyHistoryService = new Mock<ILatencyHistoryService>();
        _mockAlertService = new Mock<IPerformanceAlertService>();
        _mockCommandPerformanceAggregator = new Mock<ICommandPerformanceAggregator>();
        _mockDatabaseMetricsCollector = new Mock<IDatabaseMetricsCollector>();
        _mockBackgroundServiceHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockInstrumentedCache = new Mock<IInstrumentedCache>();
        _mockCpuHistoryService = new Mock<ICpuHistoryService>();
        _mockLogger = new Mock<ILogger<DashboardMetricsService>>();

        _service = new DashboardMetricsService(
            _mockBotService.Object,
            _mockConnectionStateService.Object,
            _mockLatencyHistoryService.Object,
            _mockAlertService.Object,
            _mockCommandPerformanceAggregator.Object,
            _mockDatabaseMetricsCollector.Object,
            _mockBackgroundServiceHealthRegistry.Object,
            _mockInstrumentedCache.Object,
            _mockCpuHistoryService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void GetCurrentStatus_ShouldReturnBotStatus()
    {
        // Arrange
        var expectedStatus = new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(2),
            GuildCount = 5,
            LatencyMs = 42,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-2)
        };

        _mockBotService
            .Setup(b => b.GetStatus())
            .Returns(expectedStatus);

        // Act
        var result = _service.GetCurrentStatus(ConnectionId, UserName);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedStatus, "Should return the exact status from bot service");

        _mockBotService.Verify(
            b => b.GetStatus(),
            Times.Once,
            "Should call bot service to get status");
    }

    [Fact]
    public void GetCurrentStatus_ShouldLogDebugMessage()
    {
        // Arrange
        var status = new BotStatusDto
        {
            GuildCount = 3,
            ConnectionState = "Connected"
        };

        _mockBotService.Setup(b => b.GetStatus()).Returns(status);

        // Act
        _service.GetCurrentStatus(ConnectionId, UserName);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Status requested by client")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when status is requested");
    }

    // ============================================================================
    // GetCurrentPerformanceMetrics Tests
    // ============================================================================

    [Fact]
    public void GetCurrentPerformanceMetrics_ShouldReturnPopulatedDto()
    {
        // Arrange
        const int latencyMs = 45;
        const string connectionState = "Connected";

        _mockLatencyHistoryService
            .Setup(s => s.GetCurrentLatency())
            .Returns(latencyMs);

        _mockConnectionStateService
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connected);

        // Act
        var result = _service.GetCurrentPerformanceMetrics(ConnectionId, UserName);

        // Assert
        result.Should().NotBeNull();
        result.LatencyMs.Should().Be(latencyMs, "Should return current latency");
        result.ConnectionState.Should().Be(connectionState, "Should return connection state");
        result.WorkingSetMB.Should().BeGreaterThanOrEqualTo(0, "Working set should be non-negative");
        result.PrivateMemoryMB.Should().BeGreaterThanOrEqualTo(0, "Private memory should be non-negative");
        result.ThreadCount.Should().BeGreaterThan(0, "Thread count should be positive");
        result.Gen2Collections.Should().BeGreaterThanOrEqualTo(0, "Gen2 collections should be non-negative");
        result.CpuUsagePercent.Should().Be(0.0, "CPU usage is currently hardcoded to 0");
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "Timestamp should be current");
    }

    [Fact]
    public void GetCurrentPerformanceMetrics_ShouldCallLatencyService()
    {
        // Arrange
        _mockLatencyHistoryService.Setup(s => s.GetCurrentLatency()).Returns(50);
        _mockConnectionStateService.Setup(s => s.GetCurrentState()).Returns(GatewayConnectionState.Connected);

        // Act
        _service.GetCurrentPerformanceMetrics(ConnectionId, UserName);

        // Assert
        _mockLatencyHistoryService.Verify(
            s => s.GetCurrentLatency(),
            Times.Once,
            "Should call latency history service");
    }

    [Fact]
    public void GetCurrentPerformanceMetrics_ShouldCallConnectionStateService()
    {
        // Arrange
        _mockLatencyHistoryService.Setup(s => s.GetCurrentLatency()).Returns(50);
        _mockConnectionStateService.Setup(s => s.GetCurrentState()).Returns(GatewayConnectionState.Disconnected);

        // Act
        _service.GetCurrentPerformanceMetrics(ConnectionId, UserName);

        // Assert
        _mockConnectionStateService.Verify(
            s => s.GetCurrentState(),
            Times.Once,
            "Should call connection state service");
    }

    [Fact]
    public void GetCurrentPerformanceMetrics_ShouldLogDebugMessage()
    {
        // Arrange
        _mockLatencyHistoryService.Setup(s => s.GetCurrentLatency()).Returns(50);
        _mockConnectionStateService.Setup(s => s.GetCurrentState()).Returns(GatewayConnectionState.Connected);

        // Act
        _service.GetCurrentPerformanceMetrics(ConnectionId, UserName);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performance metrics requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message");
    }

    [Fact]
    public void GetCurrentPerformanceMetrics_WithDisconnectedState_ShouldIncludeConnectionState()
    {
        // Arrange
        _mockLatencyHistoryService.Setup(s => s.GetCurrentLatency()).Returns(100);
        _mockConnectionStateService.Setup(s => s.GetCurrentState()).Returns(GatewayConnectionState.Disconnected);

        // Act
        var result = _service.GetCurrentPerformanceMetrics(ConnectionId, UserName);

        // Assert
        result.ConnectionState.Should().Be("Disconnected", "Should reflect disconnected state");
    }

    // ============================================================================
    // GetCurrentSystemHealth Tests
    // ============================================================================

    [Fact]
    public void GetCurrentSystemHealth_ShouldReturnPopulatedDto()
    {
        // Arrange
        var databaseMetrics = new DatabaseMetricsDto
        {
            TotalQueries = 1500,
            AvgQueryTimeMs = 5.5,
            SlowQueryCount = 3
        };

        var cacheStats = new List<CacheStatisticsDto>
        {
            new()
            {
                KeyPrefix = "guild:",
                Hits = 1000,
                Misses = 200,
                HitRate = 83.3,
                Size = 50
            }
        };

        var serviceHealth = new List<BackgroundServiceHealthDto>
        {
            new()
            {
                ServiceName = "CommandPerformanceService",
                Status = "Running",
                LastHeartbeat = DateTime.UtcNow.AddSeconds(-5)
            }
        };

        _mockDatabaseMetricsCollector
            .Setup(m => m.GetMetrics())
            .Returns(databaseMetrics);

        _mockInstrumentedCache
            .Setup(m => m.GetStatistics())
            .Returns(cacheStats.AsReadOnly());

        _mockBackgroundServiceHealthRegistry
            .Setup(m => m.GetAllHealth())
            .Returns(serviceHealth.AsReadOnly());

        // Act
        var result = _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        result.Should().NotBeNull();
        result.AvgQueryTimeMs.Should().Be(5.5, "Should return average query time");
        result.TotalQueries.Should().Be(1500, "Should return total queries");
        result.SlowQueryCount.Should().Be(3, "Should return slow query count");
        result.CacheStats.Should().NotBeEmpty("Should include cache statistics");
        result.CacheStats.Should().ContainKey("guild:", "Should map cache stats by prefix");
        result.BackgroundServices.Should().HaveCount(1, "Should include background services");
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "Timestamp should be current");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldReturnDatabaseMetrics()
    {
        // Arrange
        var databaseMetrics = new DatabaseMetricsDto
        {
            TotalQueries = 5000,
            AvgQueryTimeMs = 2.5,
            SlowQueryCount = 10
        };

        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(databaseMetrics);
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        var result = _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        result.TotalQueries.Should().Be(5000, "Should include total queries");
        result.AvgQueryTimeMs.Should().Be(2.5, "Should include average query time");
        result.SlowQueryCount.Should().Be(10, "Should include slow query count");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldReturnCacheStatistics()
    {
        // Arrange
        var cacheStats = new List<CacheStatisticsDto>
        {
            new()
            {
                KeyPrefix = "user:",
                Hits = 5000,
                Misses = 500,
                HitRate = 90.9,
                Size = 100
            },
            new()
            {
                KeyPrefix = "guild:",
                Hits = 2000,
                Misses = 800,
                HitRate = 71.4,
                Size = 75
            }
        };

        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(cacheStats.AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        var result = _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        result.CacheStats.Should().HaveCount(2, "Should include all cache prefixes");
        result.CacheStats.Should().ContainKey("user:");
        result.CacheStats.Should().ContainKey("guild:");
        result.CacheStats["user:"].HitRate.Should().Be(90.9, "Should map cache stats correctly");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldReturnBackgroundServiceHealth()
    {
        // Arrange
        var serviceHealth = new List<BackgroundServiceHealthDto>
        {
            new()
            {
                ServiceName = "CommandPerformanceService",
                Status = "Running",
                LastHeartbeat = DateTime.UtcNow.AddSeconds(-5),
                LastError = null
            },
            new()
            {
                ServiceName = "MessageLogCleanupService",
                Status = "Error",
                LastHeartbeat = DateTime.UtcNow.AddMinutes(-2),
                LastError = "Database connection timeout"
            }
        };

        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(serviceHealth.AsReadOnly());

        // Act
        var result = _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        result.BackgroundServices.Should().HaveCount(2, "Should include all services");
        result.BackgroundServices[0].ServiceName.Should().Be("CommandPerformanceService");
        result.BackgroundServices[0].Status.Should().Be("Running");
        result.BackgroundServices[1].ServiceName.Should().Be("MessageLogCleanupService");
        result.BackgroundServices[1].Status.Should().Be("Error");
        result.BackgroundServices[1].LastError.Should().Be("Database connection timeout");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldCallDatabaseMetricsCollector()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        _mockDatabaseMetricsCollector.Verify(
            m => m.GetMetrics(),
            Times.Once,
            "Should call database metrics collector");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldCallInstrumentedCache()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        _mockInstrumentedCache.Verify(
            m => m.GetStatistics(),
            Times.Once,
            "Should call instrumented cache for statistics");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldCallBackgroundServiceHealthRegistry()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        _mockBackgroundServiceHealthRegistry.Verify(
            m => m.GetAllHealth(),
            Times.Once,
            "Should call background service health registry");
    }

    [Fact]
    public void GetCurrentSystemHealth_ShouldLogDebugMessage()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System health requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message");
    }

    [Fact]
    public void GetCurrentSystemHealth_WithEmptyData_ShouldReturnEmptyCollections()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(m => m.GetMetrics()).Returns(new DatabaseMetricsDto());
        _mockInstrumentedCache.Setup(m => m.GetStatistics()).Returns(new List<CacheStatisticsDto>().AsReadOnly());
        _mockBackgroundServiceHealthRegistry.Setup(m => m.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>().AsReadOnly());

        // Act
        var result = _service.GetCurrentSystemHealth(ConnectionId, UserName);

        // Assert
        result.CacheStats.Should().BeEmpty("Should have empty cache stats when none provided");
        result.BackgroundServices.Should().BeEmpty("Should have empty services when none provided");
    }

    // ============================================================================
    // GetCurrentCommandPerformance Tests
    // ============================================================================

    [Fact]
    public async Task GetCurrentCommandPerformance_ShouldReturnAggregatedMetrics()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new()
            {
                CommandName = "/ping",
                ExecutionCount = 1000,
                AvgMs = 15.5,
                P95Ms = 25.0,
                P99Ms = 35.0,
                ErrorRate = 0.5
            },
            new()
            {
                CommandName = "/verify",
                ExecutionCount = 500,
                AvgMs = 250.0,
                P95Ms = 500.0,
                P99Ms = 750.0,
                ErrorRate = 2.0
            }
        };

        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(24))
            .ReturnsAsync(aggregates.AsReadOnly());

        // Act
        var result = await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 24);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands24h.Should().Be(1500, "Should sum execution counts");
        result.AvgResponseTimeMs.Should().BeApproximately(132.75, 0.01, "Should average response times");
        result.P95ResponseTimeMs.Should().BeApproximately(262.5, 0.01, "Should average P95 times");
        result.P99ResponseTimeMs.Should().BeApproximately(392.5, 0.01, "Should average P99 times");
        result.ErrorRate.Should().BeApproximately(1.25, 0.01, "Should average error rates");
        result.CommandsLastHour.Should().Be(62, "Should calculate commands last hour (1500 / 24 = 62)");
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "Timestamp should be current");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_WithCustomHours_ShouldPassHoursToAggregator()
    {
        // Arrange
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        // Act
        await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 12);

        // Assert
        _mockCommandPerformanceAggregator.Verify(
            a => a.GetAggregatesAsync(12),
            Times.Once,
            "Should pass custom hours parameter to aggregator");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_WithNoData_ShouldReturnZeroValues()
    {
        // Arrange
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        // Act
        var result = await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 24);

        // Assert
        result.TotalCommands24h.Should().Be(0, "Should return zero commands when no data");
        result.AvgResponseTimeMs.Should().Be(0, "Should return zero average response time");
        result.P95ResponseTimeMs.Should().Be(0, "Should return zero P95 response time");
        result.P99ResponseTimeMs.Should().Be(0, "Should return zero P99 response time");
        result.ErrorRate.Should().Be(0, "Should return zero error rate");
        result.CommandsLastHour.Should().Be(0, "Should return zero commands last hour");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_ShouldCallAggregatorService()
    {
        // Arrange
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        // Act
        await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 24);

        // Assert
        _mockCommandPerformanceAggregator.Verify(
            a => a.GetAggregatesAsync(24),
            Times.Once,
            "Should call aggregator service with default 24 hours");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_ShouldLogDebugMessage()
    {
        // Arrange
        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CommandPerformanceAggregateDto>().AsReadOnly());

        // Act
        await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 24);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command performance requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_WithMultipleHours_ShouldCalculateCommandsLastHourAccurately()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new()
            {
                CommandName = "/ping",
                ExecutionCount = 240,
                AvgMs = 10.0,
                P95Ms = 20.0,
                P99Ms = 30.0,
                ErrorRate = 0.0
            }
        };

        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(aggregates.AsReadOnly());

        // Act
        var result = await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 6);

        // Assert
        result.CommandsLastHour.Should().Be(40, "Should calculate 240 commands / 6 hours = 40 per hour");
    }

    [Fact]
    public async Task GetCurrentCommandPerformance_WithSingleCommand_ShouldCalculateMetrics()
    {
        // Arrange
        var aggregates = new List<CommandPerformanceAggregateDto>
        {
            new()
            {
                CommandName = "/test",
                ExecutionCount = 100,
                AvgMs = 50.0,
                P95Ms = 100.0,
                P99Ms = 150.0,
                ErrorRate = 1.0
            }
        };

        _mockCommandPerformanceAggregator
            .Setup(a => a.GetAggregatesAsync(It.IsAny<int>()))
            .ReturnsAsync(aggregates.AsReadOnly());

        // Act
        var result = await _service.GetCurrentCommandPerformanceAsync(ConnectionId, UserName, 24);

        // Assert
        result.TotalCommands24h.Should().Be(100);
        result.AvgResponseTimeMs.Should().Be(50.0);
        result.ErrorRate.Should().Be(1.0);
    }
}
