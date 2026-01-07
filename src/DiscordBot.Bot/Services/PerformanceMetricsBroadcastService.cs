using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using static DiscordBot.Core.Interfaces.GatewayConnectionState;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that collects and broadcasts performance metrics to subscribed SignalR clients.
/// Broadcasts health metrics, command performance, and system metrics at configurable intervals.
/// Only broadcasts when clients are subscribed to the relevant groups.
/// </summary>
public class PerformanceMetricsBroadcastService : MonitoredBackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IPerformanceSubscriptionTracker _subscriptionTracker;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly IConnectionStateService _connectionStateService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly IOptions<PerformanceBroadcastOptions> _options;

    /// <inheritdoc />
    public override string ServiceName => "Performance Metrics Broadcast Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "performance_metrics_broadcast_service";

    public PerformanceMetricsBroadcastService(
        IServiceProvider serviceProvider,
        IHubContext<DashboardHub> hubContext,
        IPerformanceSubscriptionTracker subscriptionTracker,
        ILatencyHistoryService latencyHistoryService,
        IConnectionStateService connectionStateService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        IOptions<PerformanceBroadcastOptions> options,
        ILogger<PerformanceMetricsBroadcastService> logger)
        : base(serviceProvider, logger)
    {
        _hubContext = hubContext;
        _subscriptionTracker = subscriptionTracker;
        _latencyHistoryService = latencyHistoryService;
        _connectionStateService = connectionStateService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _options = options;
    }

    /// <inheritdoc />
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogInformation("Performance metrics broadcasting is disabled");
            return;
        }

        _logger.LogInformation(
            "Performance metrics broadcast service started. Intervals: Health={HealthInterval}s, Commands={CommandInterval}s, System={SystemInterval}s",
            options.HealthMetricsIntervalSeconds,
            options.CommandMetricsIntervalSeconds,
            options.SystemMetricsIntervalSeconds);

        // Use separate timers for each metric type
        using var healthTimer = new PeriodicTimer(TimeSpan.FromSeconds(options.HealthMetricsIntervalSeconds));
        using var commandTimer = new PeriodicTimer(TimeSpan.FromSeconds(options.CommandMetricsIntervalSeconds));
        using var systemTimer = new PeriodicTimer(TimeSpan.FromSeconds(options.SystemMetricsIntervalSeconds));

        // Track execution cycles for each broadcast type
        var healthCycle = 0;
        var commandCycle = 0;
        var systemCycle = 0;

        // Run all timers concurrently
        var healthTask = RunHealthMetricsBroadcastLoopAsync(healthTimer, () => ++healthCycle, stoppingToken);
        var commandTask = RunCommandMetricsBroadcastLoopAsync(commandTimer, () => ++commandCycle, stoppingToken);
        var systemTask = RunSystemMetricsBroadcastLoopAsync(systemTimer, () => ++systemCycle, stoppingToken);

        await Task.WhenAll(healthTask, commandTask, systemTask);

        _logger.LogInformation("Performance metrics broadcast service stopped");
    }

    private async Task RunHealthMetricsBroadcastLoopAsync(
        PeriodicTimer timer,
        Func<int> getCycle,
        CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var cycle = getCycle();
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                using var activity = BotActivitySource.StartBackgroundServiceActivity(
                    TracingServiceName,
                    cycle,
                    correlationId);
                activity?.SetTag("broadcast.type", "health_metrics");

                UpdateHeartbeat();

                try
                {
                    await BroadcastHealthMetricsAsync(stoppingToken);
                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BotActivitySource.RecordException(activity, ex);
                    _logger.LogError(ex, "Error broadcasting health metrics");
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Health metrics broadcast loop stopping");
        }
    }

    private async Task RunCommandMetricsBroadcastLoopAsync(
        PeriodicTimer timer,
        Func<int> getCycle,
        CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var cycle = getCycle();
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                using var activity = BotActivitySource.StartBackgroundServiceActivity(
                    TracingServiceName,
                    cycle,
                    correlationId);
                activity?.SetTag("broadcast.type", "command_performance");

                UpdateHeartbeat();

                try
                {
                    await BroadcastCommandPerformanceAsync(stoppingToken);
                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BotActivitySource.RecordException(activity, ex);
                    _logger.LogError(ex, "Error broadcasting command performance");
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Command metrics broadcast loop stopping");
        }
    }

    private async Task RunSystemMetricsBroadcastLoopAsync(
        PeriodicTimer timer,
        Func<int> getCycle,
        CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var cycle = getCycle();
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                using var activity = BotActivitySource.StartBackgroundServiceActivity(
                    TracingServiceName,
                    cycle,
                    correlationId);
                activity?.SetTag("broadcast.type", "system_metrics");

                UpdateHeartbeat();

                try
                {
                    await BroadcastSystemMetricsAsync(stoppingToken);
                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BotActivitySource.RecordException(activity, ex);
                    _logger.LogError(ex, "Error broadcasting system metrics");
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("System metrics broadcast loop stopping");
        }
    }

    private async Task BroadcastHealthMetricsAsync(CancellationToken stoppingToken)
    {
        // Skip if no clients are subscribed
        if (_subscriptionTracker.PerformanceGroupClientCount == 0)
        {
            _logger.LogTrace("Skipping health metrics broadcast - no subscribers");
            return;
        }

        using var activity = BotActivitySource.StartServiceActivity(
            TracingServiceName,
            "broadcast_health_metrics");

        try
        {
            var metrics = CollectHealthMetrics();

            await _hubContext.Clients
                .Group(DashboardHub.PerformanceGroupName)
                .SendAsync("HealthMetricsUpdate", metrics, stoppingToken);

            _logger.LogDebug(
                "Broadcast health metrics to {ClientCount} clients: Latency={LatencyMs}ms, Memory={MemoryMB}MB",
                _subscriptionTracker.PerformanceGroupClientCount,
                metrics.LatencyMs,
                metrics.WorkingSetMB);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    private async Task BroadcastCommandPerformanceAsync(CancellationToken stoppingToken)
    {
        // Skip if no clients are subscribed
        if (_subscriptionTracker.PerformanceGroupClientCount == 0)
        {
            _logger.LogTrace("Skipping command performance broadcast - no subscribers");
            return;
        }

        using var activity = BotActivitySource.StartServiceActivity(
            TracingServiceName,
            "broadcast_command_performance");

        try
        {
            var metrics = await CollectCommandMetricsAsync();

            await _hubContext.Clients
                .Group(DashboardHub.PerformanceGroupName)
                .SendAsync("CommandPerformanceUpdate", metrics, stoppingToken);

            _logger.LogDebug(
                "Broadcast command performance to {ClientCount} clients: Total={TotalCommands}, AvgMs={AvgMs}",
                _subscriptionTracker.PerformanceGroupClientCount,
                metrics.TotalCommands24h,
                metrics.AvgResponseTimeMs);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    private async Task BroadcastSystemMetricsAsync(CancellationToken stoppingToken)
    {
        // Skip if no clients are subscribed
        if (_subscriptionTracker.SystemHealthGroupClientCount == 0)
        {
            _logger.LogTrace("Skipping system metrics broadcast - no subscribers");
            return;
        }

        using var activity = BotActivitySource.StartServiceActivity(
            TracingServiceName,
            "broadcast_system_metrics");

        try
        {
            var metrics = CollectSystemMetrics();

            await _hubContext.Clients
                .Group(DashboardHub.SystemHealthGroupName)
                .SendAsync("SystemMetricsUpdate", metrics, stoppingToken);

            _logger.LogDebug(
                "Broadcast system metrics to {ClientCount} clients: AvgQueryMs={AvgQueryMs}, TotalQueries={TotalQueries}",
                _subscriptionTracker.SystemHealthGroupClientCount,
                metrics.AvgQueryTimeMs,
                metrics.TotalQueries);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    private HealthMetricsUpdateDto CollectHealthMetrics()
    {
        var currentLatency = _latencyHistoryService.GetCurrentLatency();
        var connectionState = _connectionStateService.GetCurrentState();

        // Get current process metrics - dispose immediately to prevent memory leak
        long workingSetMB;
        long privateMemoryMB;
        int threadCount;
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            workingSetMB = process.WorkingSet64 / 1024 / 1024;
            privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            threadCount = process.Threads.Count;
        }

        var gen2Collections = GC.CollectionCount(2);

        // CPU usage calculation would require tracking over time
        // This can be enhanced later with a proper CPU monitoring service
        var cpuUsagePercent = 0.0;

        return new HealthMetricsUpdateDto
        {
            LatencyMs = currentLatency,
            WorkingSetMB = workingSetMB,
            PrivateMemoryMB = privateMemoryMB,
            CpuUsagePercent = cpuUsagePercent,
            ThreadCount = threadCount,
            Gen2Collections = gen2Collections,
            ConnectionState = connectionState.ToString(),
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<CommandPerformanceUpdateDto> CollectCommandMetricsAsync()
    {
        const int hours = 24;
        var aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);

        // Calculate overall metrics from aggregates
        var totalCommands = aggregates.Sum(a => a.ExecutionCount);
        var avgResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.AvgMs) : 0;
        var p95ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P95Ms) : 0;
        var p99ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P99Ms) : 0;
        var errorRate = aggregates.Any() ? aggregates.Average(a => a.ErrorRate) : 0;

        // Calculate commands in the last hour (approximation)
        var commandsLastHour = hours > 0 ? totalCommands / hours : totalCommands;

        return new CommandPerformanceUpdateDto
        {
            TotalCommands24h = totalCommands,
            AvgResponseTimeMs = avgResponseTimeMs,
            P95ResponseTimeMs = p95ResponseTimeMs,
            P99ResponseTimeMs = p99ResponseTimeMs,
            ErrorRate = errorRate,
            CommandsLastHour = commandsLastHour,
            Timestamp = DateTime.UtcNow
        };
    }

    private SystemMetricsUpdateDto CollectSystemMetrics()
    {
        var dbMetrics = _databaseMetricsCollector.GetMetrics();
        var cacheStats = _instrumentedCache.GetStatistics();
        var serviceHealth = _backgroundServiceHealthRegistry.GetAllHealth();

        // Calculate queries per second
        var queriesPerSecond = dbMetrics.TotalQueries > 0
            ? dbMetrics.AvgQueryTimeMs > 0
                ? 1000.0 / dbMetrics.AvgQueryTimeMs
                : 0
            : 0;

        // Map cache statistics to dictionary by key prefix
        var cacheStatsDict = cacheStats.ToDictionary(
            c => c.KeyPrefix,
            c => new CacheStatsDto
            {
                KeyPrefix = c.KeyPrefix,
                Hits = c.Hits,
                Misses = c.Misses,
                HitRate = c.HitRate,
                Size = c.Size
            });

        // Map background service health to simplified DTOs
        var serviceStatusList = serviceHealth.Select(s => new BackgroundServiceStatusDto
        {
            ServiceName = s.ServiceName,
            Status = s.Status,
            LastHeartbeat = s.LastHeartbeat,
            LastError = s.LastError
        }).ToList();

        return new SystemMetricsUpdateDto
        {
            AvgQueryTimeMs = dbMetrics.AvgQueryTimeMs,
            TotalQueries = (int)dbMetrics.TotalQueries,
            QueriesPerSecond = queriesPerSecond,
            SlowQueryCount = dbMetrics.SlowQueryCount,
            CacheStats = cacheStatsDict,
            BackgroundServices = serviceStatusList,
            Timestamp = DateTime.UtcNow
        };
    }
}
