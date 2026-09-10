using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CpuSamplingService"/>.
/// Tests cover service lifecycle, CPU sampling, error handling, and cancellation.
/// <para>
/// The service's sampling loop is driven entirely by an injected <see cref="TimeProvider"/> here
/// (a <see cref="FakeTimeProvider"/>), rather than real wall-clock delays. The service previously used
/// a real <c>PeriodicTimer</c> with a 1-second interval and tests slept for 1.5-3s hoping ticks fired
/// in time - under CPU load the thread pool could easily fail to schedule a tick within that window,
/// causing intermittent failures. Advancing the fake clock deterministically removes that race: a
/// sample is recorded exactly when the code awaits past the advanced time, never "maybe, if the
/// scheduler was fast enough".
/// </para>
/// </summary>
[Collection("Sequential")]
public class CpuSamplingServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ICpuHistoryService> _mockCpuHistoryService;
    private readonly Mock<IBackgroundServiceHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ILogger<CpuSamplingService>> _mockLogger;
    private readonly Mock<IOptions<PerformanceMetricsOptions>> _mockOptions;
    private readonly FakeTimeProvider _fakeTime;

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    public CpuSamplingServiceTests()
    {
        _mockCpuHistoryService = new Mock<ICpuHistoryService>();
        _mockHealthRegistry = new Mock<IBackgroundServiceHealthRegistry>();
        _mockLogger = new Mock<ILogger<CpuSamplingService>>();
        _mockOptions = new Mock<IOptions<PerformanceMetricsOptions>>();
        _fakeTime = new FakeTimeProvider();

        // Create a real ServiceProvider with mocked services
        var services = new ServiceCollection();
        services.AddSingleton(_mockCpuHistoryService.Object);
        services.AddSingleton(_mockHealthRegistry.Object);
        _serviceProvider = services.BuildServiceProvider();

        // Setup default options with the interval the fake clock will be advanced by
        _mockOptions.Setup(x => x.Value).Returns(new PerformanceMetricsOptions
        {
            CpuSampleIntervalSeconds = (int)SampleInterval.TotalSeconds,
            CpuRetentionHours = 24
        });
    }

    private CpuSamplingService CreateService()
    {
        return new CpuSamplingService(
            _serviceProvider,
            _mockCpuHistoryService.Object,
            _mockOptions.Object,
            _mockLogger.Object,
            _fakeTime);
    }

    /// <summary>
    /// Advances the fake clock in small steps until <paramref name="condition"/> is satisfied
    /// (or a generous real-time ceiling is hit, which only matters if the service is genuinely
    /// broken - it never gates correctness on how much real time elapsed).
    /// </summary>
    private static async Task<bool> AdvanceUntilAsync(
        FakeTimeProvider timeProvider,
        Func<bool> condition,
        TimeSpan? step = null,
        TimeSpan? ceiling = null)
    {
        var stepSize = step ?? TimeSpan.FromMilliseconds(50);
        // The ceiling only guards against a genuinely hung/broken service - it never gates
        // correctness on real elapsed time. It is intentionally generous (rather than the 10s
        // used in isolation) because the full test suite runs thousands of tests in parallel,
        // and thread-pool contention can significantly delay how quickly this loop and the fake
        // timer's callback actually get scheduled.
        var deadline = DateTime.UtcNow + (ceiling ?? TimeSpan.FromSeconds(30));

        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            timeProvider.Advance(stepSize);
            // Yield so the service's Task.Delay continuation (scheduled against the fake clock)
            // actually runs before we check the condition again.
            await Task.Delay(20);
        }

        return true;
    }

    private async Task<CpuSamplingService> StartServiceAsync(CancellationTokenSource cts)
    {
        var service = CreateService();
        await service.StartAsync(cts.Token);
        return service;
    }

    private async Task StopServiceAsync(CpuSamplingService service, CancellationTokenSource cts)
    {
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
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
        using var cts = new CancellationTokenSource();
        var service = await StartServiceAsync(cts);

        // Act - advance past the initial 100ms delay to record the first sample
        var recorded = await AdvanceUntilAsync(
            _fakeTime,
            () => _mockCpuHistoryService.Invocations.Any(i => i.Method.Name == nameof(ICpuHistoryService.RecordSample)));

        await StopServiceAsync(service, cts);

        // Assert
        recorded.Should().BeTrue("the initial sample should be recorded once the fake clock passes the startup delay");
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

        using var cts = new CancellationTokenSource();
        var service = await StartServiceAsync(cts);

        // Act
        await AdvanceUntilAsync(_fakeTime, () => recordedValues.Count > 0);
        await StopServiceAsync(service, cts);

        // Assert
        recordedValues.Should().NotBeEmpty("at least one sample should be recorded");
        recordedValues.Should().OnlyContain(v => v >= 0 && v <= 100,
            "CPU values should be clamped to 0-100 range");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_RegistersWithHealthMonitoring()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var service = await StartServiceAsync(cts);
        await LogTestHelper.WaitUntilAsync(
            () => _mockHealthRegistry.Invocations.Any(i => i.Method.Name == nameof(IBackgroundServiceHealthRegistry.Register)));
        await StopServiceAsync(service, cts);
        await LogTestHelper.WaitUntilAsync(
            () => _mockHealthRegistry.Invocations.Any(i => i.Method.Name == nameof(IBackgroundServiceHealthRegistry.Unregister)));

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
        using var cts = new CancellationTokenSource();
        var service = await StartServiceAsync(cts);

        // Act - advance one full interval past the initial sample so the loop's heartbeat update runs
        await AdvanceUntilAsync(
            _fakeTime,
            () => _mockCpuHistoryService.Invocations.Count(i => i.Method.Name == nameof(ICpuHistoryService.RecordSample)) >= 1);
        await AdvanceUntilAsync(_fakeTime, () => service.LastHeartbeat is not null);
        await StopServiceAsync(service, cts);

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
        using var cts = new CancellationTokenSource();

        // Act
        var service = await StartServiceAsync(cts);
        var logged = await LogTestHelper.WaitForLogAsync(
            _mockLogger,
            LogLevel.Information,
            m => m.Contains("CPU sampling started") && m.Contains(((int)SampleInterval.TotalSeconds).ToString()));
        await StopServiceAsync(service, cts);

        // Assert
        logged.Should().BeTrue();
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("CPU sampling started") &&
                    v.ToString()!.Contains(((int)SampleInterval.TotalSeconds).ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message with interval");
    }

    [Fact]
    public async Task ExecuteMonitoredAsync_LogsStopMessage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var service = await StartServiceAsync(cts);
        await LogTestHelper.WaitForLogAsync(_mockLogger, LogLevel.Information, m => m.Contains("CPU sampling started"));

        // Act
        await StopServiceAsync(service, cts);
        var logged = await LogTestHelper.WaitForLogAsync(_mockLogger, LogLevel.Information, m => m.Contains("stopping"));

        // Assert
        logged.Should().BeTrue();
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

        using var cts = new CancellationTokenSource();
        var service = await StartServiceAsync(cts);

        // Act - advance through the initial sample (fails) and at least one interval tick (recovers)
        await AdvanceUntilAsync(_fakeTime, () => callCount >= 1);
        await AdvanceUntilAsync(_fakeTime, () => callCount >= 2, step: SampleInterval);
        await StopServiceAsync(service, cts);

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
