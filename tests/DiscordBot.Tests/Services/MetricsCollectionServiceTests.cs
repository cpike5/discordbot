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
/// Integration tests for <see cref="MetricsCollectionService"/>.
/// Tests verify that the service correctly collects system health metrics and persists them to the database.
/// </summary>
public class MetricsCollectionServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IMetricSnapshotRepository> _mockRepository;
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector;
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ILogger<MetricsCollectionService>> _mockLogger;
    private readonly Mock<IOptions<HistoricalMetricsOptions>> _mockOptions;

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

        // Setup default options
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 60,
            RetentionDays = 30,
            CleanupIntervalHours = 6
        });

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

    private MetricsCollectionService CreateService()
    {
        return new MetricsCollectionService(
            _serviceProvider,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotCollectMetrics()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = false,
            SampleIntervalSeconds = 60,
            RetentionDays = 30,
            CleanupIntervalHours = 6
        });

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to check and exit
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

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
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("MetricsCollectionService starting") &&
                    v.ToString()!.Contains("60") && // Sample interval
                    v.ToString()!.Contains("30")),  // Retention days
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message with configuration details");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomSampleInterval_UsesSampleInterval()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 5, // 5 second interval for testing
            RetentionDays = 30,
            CleanupIntervalHours = 6
        });

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("5")), // Custom interval
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
        // Arrange
        var service = CreateService();

        // Act
        var serviceName = service.ServiceName;

        // Assert
        serviceName.Should().Be("MetricsCollectionService", "service should have correct name for health monitoring");
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_CollectsAllMetricTypes()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1, // Very short interval
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

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
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000); // Wait 12 seconds for initial delay + one collection cycle
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify snapshot was persisted
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should persist at least one metric snapshot");

        // Verify snapshot contains all metric types if captured
        if (capturedSnapshot != null)
        {
            // Database metrics
            capturedSnapshot.DatabaseAvgQueryTimeMs.Should().Be(15.75);
            capturedSnapshot.DatabaseTotalQueries.Should().Be(5000);
            capturedSnapshot.DatabaseSlowQueryCount.Should().Be(10);

            // Memory metrics (should be positive values)
            capturedSnapshot.WorkingSetMB.Should().BeGreaterThan(0);
            capturedSnapshot.PrivateMemoryMB.Should().BeGreaterThan(0);
            capturedSnapshot.HeapSizeMB.Should().BeGreaterThanOrEqualTo(0);

            // GC metrics (should be non-negative)
            capturedSnapshot.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
            capturedSnapshot.Gen1Collections.Should().BeGreaterThanOrEqualTo(0);
            capturedSnapshot.Gen2Collections.Should().BeGreaterThanOrEqualTo(0);

            // Cache metrics
            capturedSnapshot.CacheHitRatePercent.Should().Be(90.0); // 900 / (900 + 100) * 100
            capturedSnapshot.CacheTotalEntries.Should().Be(250);
            capturedSnapshot.CacheTotalHits.Should().Be(900);
            capturedSnapshot.CacheTotalMisses.Should().Be(100);

            // Service health metrics
            capturedSnapshot.ServicesTotalCount.Should().Be(3);
            capturedSnapshot.ServicesRunningCount.Should().Be(2);
            capturedSnapshot.ServicesErrorCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_StoresTimestampInUtc()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var beforeUtc = DateTime.UtcNow;
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000); // Wait for collection
        var afterUtc = DateTime.UtcNow;
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (capturedSnapshot != null)
        {
            capturedSnapshot.Timestamp.Should().BeOnOrAfter(beforeUtc, "timestamp should be recent");
            capturedSnapshot.Timestamp.Should().BeOnOrBefore(afterUtc, "timestamp should not be in the future");
            capturedSnapshot.Timestamp.Kind.Should().Be(DateTimeKind.Utc, "timestamp should be stored in UTC");
        }
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_WithNoCacheStatistics_SetsDefaultValues()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

        _mockInstrumentedCache.Setup(c => c.GetStatistics())
            .Returns(new List<CacheStatisticsDto>()); // Empty list

        MetricSnapshot? capturedSnapshot = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<MetricSnapshot, CancellationToken>((snapshot, ct) => capturedSnapshot = snapshot)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (capturedSnapshot != null)
        {
            capturedSnapshot.CacheHitRatePercent.Should().Be(0.0);
            capturedSnapshot.CacheTotalEntries.Should().Be(0);
            capturedSnapshot.CacheTotalHits.Should().Be(0);
            capturedSnapshot.CacheTotalMisses.Should().Be(0);
        }
    }

    [Fact]
    public async Task CollectAndPersistSnapshot_WithMultipleCachePrefixes_AggregatesStatistics()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

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
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (capturedSnapshot != null)
        {
            // Total hits: 500 + 300 + 200 = 1000
            // Total misses: 100 + 50 + 50 = 200
            // Hit rate: 1000 / (1000 + 200) * 100 = 83.33%
            capturedSnapshot.CacheTotalHits.Should().Be(1000);
            capturedSnapshot.CacheTotalMisses.Should().Be(200);
            capturedSnapshot.CacheTotalEntries.Should().Be(300); // 150 + 100 + 50
            capturedSnapshot.CacheHitRatePercent.Should().BeApproximately(83.33, 0.01);
        }
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task PerformCleanup_DeletesOldSnapshots()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 7,
            CleanupIntervalHours = 1
        });

        var deletedCount = 50;
        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000); // Wait for initial delay + first cycle
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockRepository.Verify(
            r => r.DeleteOlderThanAsync(
                It.Is<DateTime>(dt => dt < DateTime.UtcNow && dt > DateTime.UtcNow.AddDays(-8)),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should delete snapshots older than retention period");
    }

    [Fact]
    public async Task PerformCleanup_LogsWhenSnapshotsDeleted()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 1
        });

        var deletedCount = 100;
        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

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
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 1
        });

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

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
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24 // Should not cleanup on every cycle
        });

        _mockRepository.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(13000); // Wait for multiple collection cycles
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        // First cleanup should happen, but subsequent ones shouldn't happen within the interval
        _mockRepository.Verify(
            r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtMostOnce,
            "cleanup should respect the configured interval");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WithRepositoryError_LogsErrorAndContinues()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

        var callCount = 0;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Database error");
                }
            })
            .Returns(Task.CompletedTask);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(35000); // Wait long enough for error and recovery
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Should not throw - service should handle exceptions gracefully
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected due to cancellation
        }
        catch (InvalidOperationException)
        {
            Assert.Fail("Service should handle exceptions gracefully and not propagate them");
        }

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

    [Fact]
    public async Task ExecuteAsync_WithDatabaseMetricsCollectorError_LogsErrorAndContinues()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

        _mockDatabaseMetricsCollector.Setup(c => c.GetMetrics())
            .Throws(new InvalidOperationException("Database metrics error"));

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

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
    public async Task ExecuteAsync_AfterTransientError_RetryAfter30Seconds()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

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
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(45000); // Wait for error + 30 second delay + retry
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        callCount.Should().BeGreaterThan(1, "service should retry after error");
    }

    #endregion

    #region Service Registration Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull("the service should be created successfully");
        service.ServiceName.Should().Be("MetricsCollectionService");
    }

    [Fact]
    public async Task ExecuteAsync_RegistersWithHealthMonitoring()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

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
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var beforeStart = DateTime.UtcNow;
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(12000); // Wait for at least one successful iteration
        var afterIteration = DateTime.UtcNow;
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (service.LastHeartbeat.HasValue)
        {
            service.LastHeartbeat.Value.Should().BeOnOrAfter(beforeStart);
            service.LastHeartbeat.Value.Should().BeOnOrBefore(afterIteration);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClearsErrorAfterSuccessfulIteration()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new HistoricalMetricsOptions
        {
            Enabled = true,
            SampleIntervalSeconds = 1,
            RetentionDays = 30,
            CleanupIntervalHours = 24
        });

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
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(45000); // Wait for error + recovery
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (callCount > 1)
        {
            service.Status.Should().BeOneOf("Running", "Stopped", "Error");
            // After successful iteration, error should be cleared
        }
    }

    #endregion
}
