using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="PerformanceMetricsController"/> historical metrics endpoints.
/// Tests cover GetHistoricalMetrics, GetDatabaseHistory, and GetMemoryHistory endpoints.
/// </summary>
[Trait("Category", "Unit")]
public class PerformanceMetricsControllerTests
{
    private readonly Mock<IConnectionStateService> _mockConnectionStateService;
    private readonly Mock<ILatencyHistoryService> _mockLatencyHistoryService;
    private readonly Mock<ICpuHistoryService> _mockCpuHistoryService;
    private readonly Mock<ICommandPerformanceAggregator> _mockCommandPerformanceAggregator;
    private readonly Mock<IApiRequestTracker> _mockApiRequestTracker;
    private readonly Mock<IDatabaseMetricsCollector> _mockDatabaseMetricsCollector;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockBackgroundServiceHealthRegistry;
    private readonly Mock<IInstrumentedCache> _mockInstrumentedCache;
    private readonly Mock<IMetricSnapshotRepository> _mockMetricSnapshotRepository;
    private readonly Mock<ILogger<PerformanceMetricsController>> _mockLogger;
    private readonly PerformanceMetricsController _controller;

    public PerformanceMetricsControllerTests()
    {
        _mockConnectionStateService = new Mock<IConnectionStateService>();
        _mockLatencyHistoryService = new Mock<ILatencyHistoryService>();
        _mockCpuHistoryService = new Mock<ICpuHistoryService>();
        _mockCommandPerformanceAggregator = new Mock<ICommandPerformanceAggregator>();
        _mockApiRequestTracker = new Mock<IApiRequestTracker>();
        _mockDatabaseMetricsCollector = new Mock<IDatabaseMetricsCollector>();
        _mockBackgroundServiceHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockInstrumentedCache = new Mock<IInstrumentedCache>();
        _mockMetricSnapshotRepository = new Mock<IMetricSnapshotRepository>();
        _mockLogger = new Mock<ILogger<PerformanceMetricsController>>();

        _controller = new PerformanceMetricsController(
            _mockConnectionStateService.Object,
            _mockLatencyHistoryService.Object,
            _mockCpuHistoryService.Object,
            _mockCommandPerformanceAggregator.Object,
            _mockApiRequestTracker.Object,
            _mockDatabaseMetricsCollector.Object,
            _mockBackgroundServiceHealthRegistry.Object,
            _mockInstrumentedCache.Object,
            _mockMetricSnapshotRepository.Object,
            _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetHistoricalMetrics Tests

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnOkWithSnapshots_WhenDataExists()
    {
        // Arrange
        var snapshots = new List<MetricSnapshotDto>
        {
            CreateTestMetricSnapshot(DateTime.UtcNow.AddHours(-2)),
            CreateTestMetricSnapshot(DateTime.UtcNow.AddHours(-1)),
            CreateTestMetricSnapshot(DateTime.UtcNow)
        };

        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        // Act
        var result = await _controller.GetHistoricalMetrics(24, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as HistoricalMetricsResponseDto;

        response.Should().NotBeNull();
        response!.Snapshots.Should().HaveCount(3);
        response.Granularity.Should().Be("5m", "24 hours should use 5-minute aggregation");
        response.EndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        response.StartTime.Should().BeCloseTo(DateTime.UtcNow.AddHours(-24), TimeSpan.FromSeconds(5));

        _mockMetricSnapshotRepository.Verify(
            r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                5, // 5-minute aggregation for 24 hours
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnOkWithEmptySnapshots_WhenNoData()
    {
        // Arrange
        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricSnapshotDto>());

        // Act
        var result = await _controller.GetHistoricalMetrics(12, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as HistoricalMetricsResponseDto;

        response.Should().NotBeNull();
        response!.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnBadRequest_WhenHoursLessThanOne()
    {
        // Act
        var result = await _controller.GetHistoricalMetrics(0, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnBadRequest_WhenHoursGreaterThan720()
    {
        // Act
        var result = await _controller.GetHistoricalMetrics(721, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnBadRequest_WhenInvalidMetricParameter()
    {
        // Act
        var result = await _controller.GetHistoricalMetrics(24, "invalid", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid metric parameter");
        error.Detail.Should().Contain("Metric must be 'database', 'memory', 'cache', 'services', or 'all'");
    }

    [Fact]
    public async Task GetHistoricalMetrics_ShouldReturnInternalServerError_OnRepositoryException()
    {
        // Arrange
        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.GetHistoricalMetrics(24, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var error = objectResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Failed to retrieve historical metrics");
        error.Detail.Should().Contain("Database connection failed");
    }

    [Theory]
    [InlineData(6, 0, "raw")]        // 1-6 hours: raw samples
    [InlineData(24, 5, "5m")]        // 7-24 hours: 5-minute buckets
    [InlineData(168, 15, "15m")]     // 25-168 hours (7 days): 15-minute buckets
    [InlineData(720, 60, "1h")]      // 169-720 hours (30 days): 1-hour buckets
    public async Task GetHistoricalMetrics_ShouldUseCorrectAggregation_ForTimeRange(
        int hours,
        int expectedAggregation,
        string expectedGranularity)
    {
        // Arrange
        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricSnapshotDto>());

        // Act
        var result = await _controller.GetHistoricalMetrics(hours, "all", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as HistoricalMetricsResponseDto;

        response.Should().NotBeNull();
        response!.Granularity.Should().Be(expectedGranularity);

        _mockMetricSnapshotRepository.Verify(
            r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                expectedAggregation,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetDatabaseHistory Tests

    [Fact]
    public async Task GetDatabaseHistory_ShouldReturnOkWithSamplesAndStatistics_WhenDataExists()
    {
        // Arrange
        var snapshots = new List<MetricSnapshotDto>
        {
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-2),
                DatabaseAvgQueryTimeMs = 10.5,
                DatabaseTotalQueries = 1000,
                DatabaseSlowQueryCount = 5
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-1),
                DatabaseAvgQueryTimeMs = 15.2,
                DatabaseTotalQueries = 2000,
                DatabaseSlowQueryCount = 8
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow,
                DatabaseAvgQueryTimeMs = 12.8,
                DatabaseTotalQueries = 3000,
                DatabaseSlowQueryCount = 3
            }
        };

        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        // Act
        var result = await _controller.GetDatabaseHistory(24, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as DatabaseHistoryResponseDto;

        response.Should().NotBeNull();
        response!.Samples.Should().HaveCount(3);
        response.Samples[0].AvgQueryTimeMs.Should().Be(10.5);
        response.Samples[1].TotalQueries.Should().Be(2000);
        response.Samples[2].SlowQueryCount.Should().Be(3);

        // Verify statistics calculations
        response.Statistics.Should().NotBeNull();
        response.Statistics.AvgQueryTimeMs.Should().BeApproximately((10.5 + 15.2 + 12.8) / 3, 0.01);
        response.Statistics.MinQueryTimeMs.Should().Be(10.5);
        response.Statistics.MaxQueryTimeMs.Should().Be(15.2);
        response.Statistics.TotalSlowQueries.Should().Be(16); // 5 + 8 + 3
    }

    [Fact]
    public async Task GetDatabaseHistory_ShouldReturnOkWithEmptySamples_WhenNoData()
    {
        // Arrange
        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricSnapshotDto>());

        // Act
        var result = await _controller.GetDatabaseHistory(12, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as DatabaseHistoryResponseDto;

        response.Should().NotBeNull();
        response!.Samples.Should().BeEmpty();
        response.Statistics.AvgQueryTimeMs.Should().Be(0);
        response.Statistics.MinQueryTimeMs.Should().Be(0);
        response.Statistics.MaxQueryTimeMs.Should().Be(0);
        response.Statistics.TotalSlowQueries.Should().Be(0);
    }

    [Fact]
    public async Task GetDatabaseHistory_ShouldReturnBadRequest_WhenHoursLessThanOne()
    {
        // Act
        var result = await _controller.GetDatabaseHistory(0, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetDatabaseHistory_ShouldReturnBadRequest_WhenHoursGreaterThan720()
    {
        // Act
        var result = await _controller.GetDatabaseHistory(721, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetDatabaseHistory_ShouldCalculateCorrectStatistics()
    {
        // Arrange
        var snapshots = new List<MetricSnapshotDto>
        {
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-4),
                DatabaseAvgQueryTimeMs = 5.0,
                DatabaseTotalQueries = 100,
                DatabaseSlowQueryCount = 1
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-3),
                DatabaseAvgQueryTimeMs = 20.0,
                DatabaseTotalQueries = 200,
                DatabaseSlowQueryCount = 10
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-2),
                DatabaseAvgQueryTimeMs = 10.0,
                DatabaseTotalQueries = 300,
                DatabaseSlowQueryCount = 5
            }
        };

        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        // Act
        var result = await _controller.GetDatabaseHistory(6, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as DatabaseHistoryResponseDto;

        response!.Statistics.AvgQueryTimeMs.Should().BeApproximately(11.666, 0.01); // (5 + 20 + 10) / 3
        response.Statistics.MinQueryTimeMs.Should().Be(5.0);
        response.Statistics.MaxQueryTimeMs.Should().Be(20.0);
        response.Statistics.TotalSlowQueries.Should().Be(16); // 1 + 10 + 5
    }

    #endregion

    #region GetMemoryHistory Tests

    [Fact]
    public async Task GetMemoryHistory_ShouldReturnOkWithSamplesAndStatistics_WhenDataExists()
    {
        // Arrange
        var snapshots = new List<MetricSnapshotDto>
        {
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-2),
                WorkingSetMB = 256,
                HeapSizeMB = 128,
                PrivateMemoryMB = 300
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-1),
                WorkingSetMB = 320,
                HeapSizeMB = 160,
                PrivateMemoryMB = 380
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow,
                WorkingSetMB = 288,
                HeapSizeMB = 144,
                PrivateMemoryMB = 340
            }
        };

        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        // Act
        var result = await _controller.GetMemoryHistory(24, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryHistoryResponseDto;

        response.Should().NotBeNull();
        response!.Samples.Should().HaveCount(3);
        response.Samples[0].WorkingSetMB.Should().Be(256);
        response.Samples[1].HeapSizeMB.Should().Be(160);
        response.Samples[2].PrivateMemoryMB.Should().Be(340);

        // Verify statistics calculations
        response.Statistics.Should().NotBeNull();
        response.Statistics.AvgWorkingSetMB.Should().BeApproximately((256 + 320 + 288) / 3.0, 0.01);
        response.Statistics.MaxWorkingSetMB.Should().Be(320);
        response.Statistics.AvgHeapSizeMB.Should().BeApproximately((128 + 160 + 144) / 3.0, 0.01);
    }

    [Fact]
    public async Task GetMemoryHistory_ShouldReturnOkWithEmptySamples_WhenNoData()
    {
        // Arrange
        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricSnapshotDto>());

        // Act
        var result = await _controller.GetMemoryHistory(12, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryHistoryResponseDto;

        response.Should().NotBeNull();
        response!.Samples.Should().BeEmpty();
        response.Statistics.AvgWorkingSetMB.Should().Be(0);
        response.Statistics.MaxWorkingSetMB.Should().Be(0);
        response.Statistics.AvgHeapSizeMB.Should().Be(0);
    }

    [Fact]
    public async Task GetMemoryHistory_ShouldReturnBadRequest_WhenHoursLessThanOne()
    {
        // Act
        var result = await _controller.GetMemoryHistory(0, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetMemoryHistory_ShouldReturnBadRequest_WhenHoursGreaterThan720()
    {
        // Act
        var result = await _controller.GetMemoryHistory(721, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;

        error.Should().NotBeNull();
        error!.Message.Should().Be("Invalid hours parameter");
        error.Detail.Should().Contain("Hours must be between 1 and 720");
    }

    [Fact]
    public async Task GetMemoryHistory_ShouldCalculateCorrectStatistics()
    {
        // Arrange
        var snapshots = new List<MetricSnapshotDto>
        {
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-4),
                WorkingSetMB = 200,
                HeapSizeMB = 100,
                PrivateMemoryMB = 250
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-3),
                WorkingSetMB = 400,
                HeapSizeMB = 200,
                PrivateMemoryMB = 450
            },
            new MetricSnapshotDto
            {
                Timestamp = DateTime.UtcNow.AddHours(-2),
                WorkingSetMB = 300,
                HeapSizeMB = 150,
                PrivateMemoryMB = 350
            }
        };

        _mockMetricSnapshotRepository
            .Setup(r => r.GetRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        // Act
        var result = await _controller.GetMemoryHistory(6, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryHistoryResponseDto;

        response!.Statistics.AvgWorkingSetMB.Should().BeApproximately(300.0, 0.01); // (200 + 400 + 300) / 3
        response.Statistics.MaxWorkingSetMB.Should().Be(400);
        response.Statistics.AvgHeapSizeMB.Should().BeApproximately(150.0, 0.01); // (100 + 200 + 150) / 3
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test metric snapshot with sample data.
    /// </summary>
    private static MetricSnapshotDto CreateTestMetricSnapshot(DateTime timestamp)
    {
        return new MetricSnapshotDto
        {
            Timestamp = timestamp,
            DatabaseAvgQueryTimeMs = 12.5,
            DatabaseTotalQueries = 5000,
            DatabaseSlowQueryCount = 10,
            WorkingSetMB = 256,
            PrivateMemoryMB = 300,
            HeapSizeMB = 128,
            CacheHitRatePercent = 85.5,
            ServicesRunningCount = 5,
            Gen0Collections = 10,
            Gen1Collections = 5,
            Gen2Collections = 2
        };
    }

    #endregion
}
