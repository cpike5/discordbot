using System.Diagnostics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that samples CPU usage at regular intervals using processor time delta calculation.
/// Records samples to ICpuHistoryService for historical analysis and alerting.
/// </summary>
public class CpuSamplingService : MonitoredBackgroundService
{
    private readonly ICpuHistoryService _cpuHistoryService;
    private readonly PerformanceMetricsOptions _options;

    private DateTime _lastSampleTime;
    private TimeSpan _lastProcessorTime;

    /// <inheritdoc/>
    public override string ServiceName => "CPU Sampling Service";

    /// <summary>
    /// Tracing service name used for activity/span naming.
    /// </summary>
    private const string TracingServiceName = "cpu_sampling_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuSamplingService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cpuHistoryService">The CPU history service to record samples to.</param>
    /// <param name="options">The performance metrics configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public CpuSamplingService(
        IServiceProvider serviceProvider,
        ICpuHistoryService cpuHistoryService,
        IOptions<PerformanceMetricsOptions> options,
        ILogger<CpuSamplingService> logger)
        : base(serviceProvider, logger)
    {
        _cpuHistoryService = cpuHistoryService;
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var sampleInterval = TimeSpan.FromSeconds(_options.CpuSampleIntervalSeconds);

        _logger.LogInformation(
            "CPU sampling started with {Interval}s interval",
            _options.CpuSampleIntervalSeconds);

        // Initialize baseline for delta calculation
        InitializeBaseline();

        // Wait a brief moment then take initial sample so we have data immediately
        await Task.Delay(100, stoppingToken);
        var initialCpu = SampleCpuUsage();
        _cpuHistoryService.RecordSample(initialCpu);
        _logger.LogDebug("Initial CPU sample recorded: {Cpu:F1}%", initialCpu);

        using var timer = new PeriodicTimer(sampleInterval);
        var executionCycle = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                executionCycle++;
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                // Use the APM-enabled activity scope for visibility in Elastic APM
                using var scope = BotActivitySource.StartBackgroundServiceActivityWithApm(
                    TracingServiceName,
                    executionCycle,
                    correlationId);

                try
                {
                    var cpuPercent = SampleCpuUsage();
                    _cpuHistoryService.RecordSample(cpuPercent);

                    UpdateHeartbeat();
                    scope.SetSuccess();
                    ClearError();

                    _logger.LogTrace(
                        "CPU sample: {Cpu:F1}% (cycle {Cycle})",
                        cpuPercent,
                        executionCycle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CPU sampling error in cycle {Cycle}", executionCycle);
                    scope.RecordException(ex);
                    RecordError(ex);

                    // Re-initialize baseline on error to recover
                    InitializeBaseline();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CPU sampling service stopping");
        }
    }

    /// <summary>
    /// Initializes the baseline for delta-based CPU calculation.
    /// </summary>
    private void InitializeBaseline()
    {
        using var process = Process.GetCurrentProcess();
        _lastSampleTime = DateTime.UtcNow;
        _lastProcessorTime = process.TotalProcessorTime;
    }

    /// <summary>
    /// Samples current CPU usage using processor time delta calculation.
    /// This method calculates the percentage of CPU time used by this process
    /// relative to the elapsed wall-clock time since the last sample.
    /// </summary>
    /// <returns>CPU usage percentage (0-100), normalized for processor count.</returns>
    private double SampleCpuUsage()
    {
        using var process = Process.GetCurrentProcess();
        var currentTime = DateTime.UtcNow;
        var currentProcessorTime = process.TotalProcessorTime;

        // Calculate deltas
        var elapsedTime = currentTime - _lastSampleTime;
        var elapsedProcessorTime = currentProcessorTime - _lastProcessorTime;

        // Avoid division by zero
        if (elapsedTime.TotalMilliseconds <= 0)
        {
            return 0;
        }

        // Calculate CPU percentage
        // TotalProcessorTime is the sum across all cores, so divide by processor count
        var processorCount = Environment.ProcessorCount;
        var cpuPercent = (elapsedProcessorTime.TotalMilliseconds / elapsedTime.TotalMilliseconds / processorCount) * 100.0;

        // Update baseline for next sample
        _lastSampleTime = currentTime;
        _lastProcessorTime = currentProcessorTime;

        // Clamp to valid range (0-100)
        return Math.Clamp(cpuPercent, 0.0, 100.0);
    }
}
