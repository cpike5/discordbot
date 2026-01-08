using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for <see cref="MetricsCollectionService"/>.
/// Uses tiny configurable delays (50ms) for fast test execution.
/// Runs sequentially to avoid timing issues with background service execution.
/// </summary>
[Collection("Sequential")]
public class MetricsCollectionServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IMetricSnapshotRepository> _mockRepository;
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector;
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ILogger<MetricsCollectionService>> _mockLogger;
    private readonly Mock<IOptions<HistoricalMetricsOptions>> _mockOptions;

    // Tiny delays for fast testing (50ms instead of 10-30 seconds)
    private const double TinyInitialDelay = 0.05;
    private const double TinySampleInterval = 0.05;
    private const double TinyErrorRetryDelay = 0.05;

    public MetricsCollectionServiceTests()
    {
        _mockRepository = new Mock<IMetricSnapshotRepository>();
        _mockDatabaseMetricsCollector = new Mock<IDatabaseMetricsCollector>();
        _mockInstrumentedCache = new Mock<IInstrumentedCache>();
        _mockHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockLogger = new Mock<ILogger<MetricsCollectionService>>();
        _mockOptions = new Mock<IOptions<HistoricalMetricsOptions>>();

        // Create a real ServiceProvider with mocked services
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDatabaseMetricsCollector.Object);
        services.AddSingleton(_mockInstrumentedCache.Object);
        services.AddSingleton(_mockHealthRegistry.Object);
        _serviceProvider = services.BuildServiceProvider();

        // Setup default options with tiny delays
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions());

        // Setup default database metrics
        _mockDatabaseMetricsCollector.Setup(c => c.GetMetrics())
            .Returns(new DatabaseMetricsDto
            {
                AvgQueryTimeMs = 10.5,
                TotalQueries = 1000,
                SlowQueryCount = 5
            });

        // Setup default cache statistics
        _mockInstrumentedCache.Setup(c => c.GetStatistics())
            .Returns(new List<CacheStatisticsDto>
            {
                new CacheStatisticsDto
                {
                    KeyPrefix = "test",
                    Hits = 850,
                    Misses = 150,
                    Size = 100,
                    HitRate = 85.0
                }
            });

        // Setup default service health
        _mockHealthRegistry.Setup(r => r.GetAllHealth())
            .Returns(new List<BackgroundServiceHealthDto>
            {
                new BackgroundServiceHealthDto { ServiceName = "Service1", Status = "Running", LastHeartbeat = DateTime.UtcNow },
                new BackgroundServiceHealthDto { ServiceName = "Service2", Status = "Running", LastHeartbeat = DateTime.UtcNow },
                new BackgroundServiceHealthDto { ServiceName = "Service3", Status = "Running", LastHeartbeat = DateTime.UtcNow }
            });
    }

    #region Helper Methods

    private static HistoricalMetricsOptions CreateTestOptions(
        bool enabled = true,
        int sampleIntervalSeconds = 60,
        int retentionDays = 30,
        int cleanupIntervalHours = 6)
    {
        return new HistoricalMetricsOptions
        {
            Enabled = enabled,
            SampleIntervalSeconds = sampleIntervalSeconds,
            RetentionDays = retentionDays,
            CleanupIntervalHours = cleanupIntervalHours,
            InitialDelaySeconds = TinyInitialDelay,
            ErrorRetryDelaySeconds = TinyErrorRetryDelay
        };
    }

    private MetricsCollectionService CreateService()
    {
        return new MetricsCollectionService(
            _serviceProvider,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    /// <summary>
    /// Runs the service for a short time to allow one collection cycle.
    /// </summary>
    private async Task RunServiceBrieflyAsync(MetricsCollectionService service, int delayMs = 200)
    {
        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(delayMs);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotCollectMetrics()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(enabled: false));
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 100);

        // Assert
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository should not be called when metrics collection is disabled");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log that metrics collection is disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabled_LogsStartupMessage()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 100);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("MetricsCollectionService starting") &&
                    v.ToString()!.Contains("60") &&
                    v.ToString()!.Contains("30")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message with configuration details");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomSampleInterval_UsesSampleInterval()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(sampleIntervalSeconds: 5));
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 100);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("5")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should use custom sample interval from configuration");
    }

    #endregion

    #region Metrics Collection Tests

    [Fact]
    public void ServiceName_ShouldReturnCorrectName()
    {
        var service = CreateService();
        service.ServiceName.Should().Be("MetricsCollectionService");
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_CollectsAllMetricTypes()
    {
        // Arrange
        _mockDatabaseMetricsCollector.Setup(c => c.GetMetrics())
            .Returns(new DatabaseMetricsDto
            {
                AvgQueryTimeMs = 15.75,
                TotalQueries = 5000,
                SlowQueryCount = 10
            });

        _mockInstrumentedCache.Setup(c => c.GetStatistics())
            .Returns(new List<CacheStatisticsDto>
            {
                new CacheStatisticsDto
                {
                    KeyPrefix = "cache1",
                    Hits = 900,
                    Misses = 100,
                    Size = 250,
                    HitRate = 90.0
                }
            });

        _mockHealthRegistry.Setup(r => r.GetAllHealth())
            .Returns(new List<BackgroundServiceHealthDto>
            {
                new BackgroundServiceHealthDto { ServiceName = "Service1", Status = "Running" },
                new BackgroundServiceHealthDto { ServiceName = "Service2", Status = "Running" },
                new BackgroundServiceHealthDto { ServiceName = "Service3", Status = "Error" }
            });

        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should persist at least one metric snapshot");

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.DatabaseAvgQueryTimeMs.Should().Be(15.75);
        capturedSnapshot.DatabaseTotalQueries.Should().Be(5000);
        capturedSnapshot.DatabaseSlowQueryCount.Should().Be(10);
        capturedSnapshot.WorkingSetMB.Should().BeGreaterThan(0);
        capturedSnapshot.CacheHitRatePercent.Should().Be(90.0);
        capturedSnapshot.CacheTotalEntries.Should().Be(250);
        capturedSnapshot.ServicesTotalCount.Should().Be(3);
        capturedSnapshot.ServicesRunningCount.Should().Be(2);
        capturedSnapshot.ServicesErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_StoresTimestampInUtc()
    {
        // Arrange
        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var beforeUtc = DateTime.UtcNow;

        // Act
        await RunServiceBrieflyAsync(service, 200);
        var afterUtc = DateTime.UtcNow;

        // Assert
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.Timestamp.Should().BeOnOrAfter(beforeUtc);
        capturedSnapshot.Timestamp.Should().BeOnOrBefore(afterUtc);
        capturedSnapshot.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_WithNoCacheStatistics_SetsDefaultValues()
    {
        // Arrange
        _mockInstrumentedCache.Setup(c => c.GetStatistics())
            .Returns(new List<CacheStatisticsDto>());

        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.CacheHitRatePercent.Should().Be(0.0);
        capturedSnapshot.CacheTotalEntries.Should().Be(0);
        capturedSnapshot.CacheTotalHits.Should().Be(0);
        capturedSnapshot.CacheTotalMisses.Should().Be(0);
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_WithMultipleCachePrefixes_AggregatesStatistics()
    {
        // Arrange
        _mockInstrumentedCache.Setup(c => c.GetStatistics())
            .Returns(new List<CacheStatisticsDto>
            {
                new CacheStatisticsDto { KeyPrefix = "cache1", Hits = 500, Misses = 100, Size = 150, HitRate = 83.33 },
                new CacheStatisticsDto { KeyPrefix = "cache2", Hits = 300, Misses = 50, Size = 100, HitRate = 85.71 },
                new CacheStatisticsDto { KeyPrefix = "cache3", Hits = 200, Misses = 50, Size = 50, HitRate = 80.0 }
            });

        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.CacheTotalHits.Should().Be(1000);
        capturedSnapshot.CacheTotalMisses.Should().Be(200);
        capturedSnapshot.CacheTotalEntries.Should().Be(300);
        capturedSnapshot.CacheHitRatePercent.Should().BeApproximately(83.33, 0.01);
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task PerformCleanup_DeletesOldSnapshots()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(retentionDays: 7, cleanupIntervalHours: 1));

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should delete snapshots older than retention period");
    }

    [Fact]
    public async Task PerformCleanup_LogsWhenSnapshotsDeleted()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(cleanupIntervalHours: 1));

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted") && v.ToString()!.Contains("100")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log the number of deleted snapshots");
    }

    [Fact]
    public async Task PerformCleanup_LogsTraceWhenNoSnapshotsDeleted()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(cleanupIntervalHours: 1));

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log trace message when no snapshots were deleted");
    }

    [Fact]
    public async Task PerformCleanup_RespectsCleanupIntervalHours()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions(cleanupIntervalHours: 24));

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();

        // Act - Run briefly (cleanup should happen once at first cycle but not repeat within 24hrs)
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtMostOnce,
            "cleanup should respect the configured interval");
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that repository errors are logged and the service continues.
    ///
    /// KNOWN ISSUE: This test passes in isolation but fails when run in the full test suite
    /// due to test parallelization causing the background service to not get enough CPU time
    /// to execute before the timeout. The [Collection("Sequential")] attribute doesn't fully
    /// solve the issue because other test classes still run in parallel.
    ///
    /// Possible solutions to investigate:
    /// 1. Use a mock TimeProvider to control time progression (attempted but Microsoft.Extensions.TimeProvider.Testing
    ///    didn't work well with async delays)
    /// 2. Redesign the test to not rely on real Task.Delay timing
    /// 3. Create a testable abstraction for the delay mechanism in the service
    ///
    /// See GitHub issue #636 for tracking.
    /// </summary>
    [Fact(Skip = "Flaky in parallel test execution - see GitHub issue #636")]
    public async Task ExecuteAsync_WithRepositoryError_LogsErrorAndContinues()
    {
        // Arrange - Explicitly set tiny delays to ensure fast test execution
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 60,
            RetentionDays = 30,
            CleanupIntervalHours = 6,
            InitialDelaySeconds = 0.01,
            ErrorRetryDelaySeconds = 0.01
        });

        // Use a signal to know when AddAsync has been called (avoids race conditions)
        var addAsyncCalled = new TaskCompletionSource<bool>();
        var callCount = 0;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                addAsyncCalled.TrySetResult(true);
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Database error");
                }
            })
            .Returns(Task.CompletedTask);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act - Start service and wait for AddAsync to be called
        var executeTask = service.StartAsync(cts.Token);

        // Wait for AddAsync to be called (with timeout for safety)
        var completed = await Task.WhenAny(addAsyncCalled.Task, Task.Delay(5000));
        completed.Should().Be(addAsyncCalled.Task, "AddAsync should be called within timeout");

        // Give time for the error to be logged
        await Task.Delay(100);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in metrics collection loop")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log the error");
    }

    /// <summary>
    /// Tests that database metrics collector errors are logged and the service continues.
    /// Same timing issue as ExecuteAsync_WithRepositoryError_LogsErrorAndContinues.
    /// See GitHub issue #636 for tracking.
    /// </summary>
    [Fact(Skip = "Flaky in parallel test execution - see GitHub issue #636")]
    public async Task ExecuteAsync_WithDatabaseMetricsCollectorError_LogsErrorAndContinues()
    {
        // Arrange - Explicitly set tiny delays
        _mockOptions.Setup(x => x.Value).Returns(CreateTestOptions());

        _mockDatabaseMetricsCollector.Setup(c => c.GetMetrics())
            .Throws(new InvalidOperationException("Database metrics error"));

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in metrics collection loop")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log errors from metrics collection");
    }

    [Fact]
    public async Task ExecuteAsync_AfterTransientError_Retries()
    {
        // Arrange
        var callCount = 0;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Transient error");
                }
            })
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act - Give enough time for error + retry delay (50ms) + retry
        // Using 1500ms to provide sufficient headroom for slow CI runners
        await RunServiceBrieflyAsync(service, 1500);

        // Assert
        callCount.Should().BeGreaterThan(1, "service should retry after error");
    }

    #endregion

    #region Service Registration Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var service = CreateService();
        service.Should().NotBeNull();
        service.ServiceName.Should().Be("MetricsCollectionService");
    }

    [Fact]
    public async Task ExecuteAsync_RegistersWithHealthMonitoring()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 100);

        // Assert
        _mockHealthRegistry.Verify(
            r => r.Register("MetricsCollectionService", It.IsAny<IBackgroundServiceHealth>()),
            Times.Once,
            "service should register with health monitoring on startup");

        _mockHealthRegistry.Verify(
            r => r.Unregister("MetricsCollectionService"),
            Times.Once,
            "service should unregister from health monitoring on shutdown");
    }

    #endregion

    #region Integration with MonitoredBackgroundService Tests

    [Fact]
    public async Task ExecuteAsync_UpdatesHeartbeatOnSuccessfulIteration()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 200);

        // Assert
        service.LastHeartbeat.Should().NotBeNull("heartbeat should be updated after successful iteration");
    }

    [Fact]
    public async Task ExecuteAsync_ClearsErrorAfterSuccessfulIteration()
    {
        // Arrange
        var callCount = 0;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Temporary error");
                }
            })
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act - Give enough time for initial delay + error + retry (with headroom for CI load)
        await RunServiceBrieflyAsync(service, 750);

        // Assert
        callCount.Should().BeGreaterThan(1, "should have retried");
        service.Status.Should().BeOneOf("Running", "Stopped");
    }

    #endregion
}
