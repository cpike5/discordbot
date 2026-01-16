using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CpuSamplingService"/>.
/// Tests cover service lifecycle, CPU sampling, error handling, and cancellation.
/// Uses tiny configurable delays (50ms) for fast test execution.
/// </summary>
[Collection("Sequential")]
public class CpuSamplingServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ICpuHistoryService> _mockCpuHistoryService;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ILogger<CpuSamplingService>> _mockLogger;
    private readonly Mock<IOptions<PerformanceMetricsOptions>> _mockOptions;

    // Tiny interval for fast testing
    private const int TinySampleIntervalSeconds = 1;

    public CpuSamplingServiceTests()
    {
        _mockCpuHistoryService = new Mock<ICpuHistoryService>();
        _mockHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockLogger = new Mock<ILogger<CpuSamplingService>>();
        _mockOptions = new Mock<IOptions<PerformanceMetricsOptions>>();

        // Create a real ServiceProvider with mocked services
        var services = new ServiceCollection();
        services.AddSingleton(_mockCpuHistoryService.Object);
        services.AddSingleton(_mockHealthRegistry.Object);
        _serviceProvider = services.BuildServiceProvider();

        // Setup default options with tiny interval
        _mockOptions.Setup(x => x.Value).Returns(new PerformanceMetricsOptions
        {
            CpuSampleIntervalSeconds = TinySampleIntervalSeconds,
            CpuRetentionHours = 24
        });
    }

    private CpuSamplingService CreateService()
    {
        return new CpuSamplingService(
            _serviceProvider,
            _mockCpuHistoryService.Object,
            _mockOptions.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Runs the service for a short time to allow one or more sampling cycles.
    /// </summary>
    private async Task RunServiceBrieflyAsync(CpuSamplingService service, int delayMs = 1500)
    {
        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(delayMs);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        try { await executeTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public void ServiceName_ReturnsExpectedValue()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        service.ServiceName.Should().Be("CPU Sampling Service");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_RecordsSamplesToHistoryService()
    {
        // Arrange
        var service = CreateService();

        // Act - Wait for at least one sample
        await RunServiceBrieflyAsync(service, 1500);

        // Assert
        _mockCpuHistoryService.Verify(
            h => h.RecordSample(It.IsAny<double>()),
            Times.AtLeastOnce,
            "should record CPU samples to history service");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_RecordsCpuValueInValidRange()
    {
        // Arrange
        var recordedValues = new List<double>();
        _mockCpuHistoryService
            .Setup(h => h.RecordSample(It.IsAny<double>()))
            .Callback<double>(v => recordedValues.Add(v));

        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 1500);

        // Assert
        recordedValues.Should().NotBeEmpty("at least one sample should be recorded");
        recordedValues.Should().OnlyContain(v => v >= 0 && v <= 100,
            "CPU values should be clamped to 0-100 range");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_RegistersWithHealthMonitoring()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 500);

        // Assert
        _mockHealthRegistry.Verify(
            r => r.Register("CPU Sampling Service", It.IsAny<IBackgroundServiceHealth>()),
            Times.Once,
            "service should register with health monitoring on startup");

        _mockHealthRegistry.Verify(
            r => r.Unregister("CPU Sampling Service"),
            Times.Once,
            "service should unregister from health monitoring on shutdown");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_UpdatesHeartbeatOnSuccess()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 1500);

        // Assert
        service.LastHeartbeat.Should().NotBeNull("heartbeat should be updated after successful sampling");
        service.Status.Should().BeOneOf("Running", "Stopped", "Initializing");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_RespectsCancellation()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act - Start and immediately cancel
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should complete without hanging
        var completedTask = await Task.WhenAny(executeTask, Task.Delay(5000));
        completedTask.Should().Be(executeTask, "service should stop promptly when cancelled");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_LogsStartupMessage()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 500);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("CPU sampling started") &&
                    v.ToString()!.Contains(TinySampleIntervalSeconds.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message with interval");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_LogsStopMessage()
    {
        // Arrange
        var service = CreateService();

        // Act
        await RunServiceBrieflyAsync(service, 500);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopping")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log when service is stopping");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_HandlesErrorAndRecovers()
    {
        // Arrange
        var callCount = 0;
        _mockCpuHistoryService
            .Setup(h => h.RecordSample(It.IsAny<double>()))
            .Callback<double>(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Test error");
                }
            });

        var service = CreateService();

        // Act - Give time for error + recovery
        await RunServiceBrieflyAsync(service, 3000);

        // Assert
        callCount.Should().BeGreaterThan(1, "service should continue after error");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CPU sampling error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log warning on error");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
        service.ServiceName.Should().Be("CPU Sampling Service");
    }
}
