using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using static DiscordBot.Core.Interfaces.GatewayConnectionState;

namespace DiscordBot.Bot.Services.Dashboard;

/// <summary>
/// Default implementation of <see cref="IDashboardMetricsService"/>.
/// Provides bot status, health, alert, and performance metrics for the dashboard hub.
/// </summary>
public class DashboardMetricsService : IDashboardMetricsService
{
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IBotService _botService;
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly IPerformanceAlertService _alertService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly ICpuHistoryService _cpuHistoryService;
    private readonly ILogger<DashboardMetricsService> _logger;

    public DashboardMetricsService(
        IBotService botService,
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        IPerformanceAlertService alertService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        ICpuHistoryService cpuHistoryService,
        ILogger<DashboardMetricsService> logger)
    {
        _botService = botService;
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _alertService = alertService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _cpuHistoryService = cpuHistoryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public BotStatusDto GetCurrentStatus(string? connectionId, string? userName)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_status");

        activity?.SetTag(TracingConstants.Attributes.UserId, userName);
        activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

        try
        {
            _logger.LogDebug(
                "Status requested by client: ConnectionId={ConnectionId}",
                connectionId);

            BotStatusDto result;
            using (BotActivitySource.StartServiceActivity("bot_service", "get_status"))
            {
                result = _botService.GetStatus();
            }

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public PerformanceHealthDto GetHealthStatus(string? connectionId, string? userName)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_health_status");

        activity?.SetTag(TracingConstants.Attributes.UserId, userName);
        activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

        try
        {
            _logger.LogDebug(
                "Health status requested by client: ConnectionId={ConnectionId}",
                connectionId);

            GatewayConnectionState connectionState;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_state"))
            {
                connectionState = _connectionStateService.GetCurrentState();
            }

            TimeSpan sessionDuration;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_session_duration"))
            {
                sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            }

            int currentLatency;
            using (BotActivitySource.StartServiceActivity("latency_history_service", "get_current_latency"))
            {
                currentLatency = _latencyHistoryService.GetCurrentLatency();
            }

            var health = new PerformanceHealthDto
            {
                Status = connectionState == GatewayConnectionState.Connected ? "Healthy" : "Unhealthy",
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Health status retrieved: Status={Status}, Uptime={Uptime}, Latency={LatencyMs}ms",
                health.Status,
                health.Uptime,
                health.LatencyMs);

            BotActivitySource.SetSuccess(activity);
            return health;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ActiveAlertSummaryDto> GetActiveAlertCountAsync(string? connectionId, string? userName)
    {
        return await ServiceActivityHelper.ExecuteAsync<ActiveAlertSummaryDto>(
            "dashboard_hub", "get_active_alert_count",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userName);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

                _logger.LogDebug(
                    "Active alert count requested by client: ConnectionId={ConnectionId}",
                    connectionId);

                ActiveAlertSummaryDto summary;
                using (BotActivitySource.StartServiceActivity("alert_service", "get_active_alert_summary"))
                {
                    summary = await _alertService.GetActiveAlertSummaryAsync();
                }

                _logger.LogTrace(
                    "Active alert count retrieved: ActiveCount={ActiveCount}, Critical={CriticalCount}, Warning={WarningCount}",
                    summary.ActiveCount,
                    summary.CriticalCount,
                    summary.WarningCount);

                return summary;
            });
    }

    /// <inheritdoc />
    public HealthMetricsUpdateDto GetCurrentPerformanceMetrics(string? connectionId, string? userName)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_performance_metrics");

        activity?.SetTag(TracingConstants.Attributes.UserId, userName);
        activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

        try
        {
            _logger.LogDebug(
                "Performance metrics requested by client: ConnectionId={ConnectionId}",
                connectionId);

            int currentLatency;
            using (BotActivitySource.StartServiceActivity("latency_history_service", "get_current_latency"))
            {
                currentLatency = _latencyHistoryService.GetCurrentLatency();
            }

            GatewayConnectionState connectionState;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_state"))
            {
                connectionState = _connectionStateService.GetCurrentState();
            }

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

            var cpuUsagePercent = _cpuHistoryService.GetCurrentCpu();

            var metrics = new HealthMetricsUpdateDto
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

            _logger.LogTrace(
                "Performance metrics retrieved: Latency={LatencyMs}ms, WorkingSet={WorkingSetMB}MB, PrivateMemory={PrivateMemoryMB}MB, Threads={ThreadCount}",
                metrics.LatencyMs,
                metrics.WorkingSetMB,
                metrics.PrivateMemoryMB,
                metrics.ThreadCount);

            BotActivitySource.SetSuccess(activity);
            return metrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public SystemMetricsUpdateDto GetCurrentSystemHealth(string? connectionId, string? userName)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_system_health");

        activity?.SetTag(TracingConstants.Attributes.UserId, userName);
        activity?.SetTag(SignalRConnectionIdAttribute, connectionId);

        try
        {
            _logger.LogDebug(
                "System health requested by client: ConnectionId={ConnectionId}",
                connectionId);

            DatabaseMetricsDto dbMetrics;
            using (BotActivitySource.StartServiceActivity("database_metrics_collector", "get_metrics"))
            {
                dbMetrics = _databaseMetricsCollector.GetMetrics();
            }

            IReadOnlyList<CacheStatisticsDto> cacheStats;
            using (BotActivitySource.StartServiceActivity("instrumented_cache", "get_statistics"))
            {
                cacheStats = _instrumentedCache.GetStatistics();
            }

            IReadOnlyList<BackgroundServiceHealthDto> serviceHealth;
            using (BotActivitySource.StartServiceActivity("background_service_health_registry", "get_all_health"))
            {
                serviceHealth = _backgroundServiceHealthRegistry.GetAllHealth();
            }

            // Calculate queries per second (simple approximation based on total queries)
            var queriesPerSecond = dbMetrics.TotalQueries > 0 ? dbMetrics.AvgQueryTimeMs > 0 ? 1000.0 / dbMetrics.AvgQueryTimeMs : 0 : 0;

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

            var systemMetrics = new SystemMetricsUpdateDto
            {
                AvgQueryTimeMs = dbMetrics.AvgQueryTimeMs,
                TotalQueries = (int)dbMetrics.TotalQueries,
                QueriesPerSecond = queriesPerSecond,
                SlowQueryCount = dbMetrics.SlowQueryCount,
                CacheStats = cacheStatsDict,
                BackgroundServices = serviceStatusList,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "System health retrieved: AvgQueryTime={AvgQueryTimeMs}ms, TotalQueries={TotalQueries}, SlowQueries={SlowQueryCount}, CacheCount={CacheCount}, ServicesCount={ServicesCount}",
                systemMetrics.AvgQueryTimeMs,
                systemMetrics.TotalQueries,
                systemMetrics.SlowQueryCount,
                systemMetrics.CacheStats.Count,
                systemMetrics.BackgroundServices.Count);

            BotActivitySource.SetSuccess(activity);
            return systemMetrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommandPerformanceUpdateDto> GetCurrentCommandPerformanceAsync(string? connectionId, string? userName, int hours)
    {
        return await ServiceActivityHelper.ExecuteAsync<CommandPerformanceUpdateDto>(
            "dashboard_hub", "get_current_command_performance",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, userName);
                activity?.SetTag(SignalRConnectionIdAttribute, connectionId);
                activity?.SetTag("hours", hours);

                _logger.LogDebug(
                    "Command performance requested by client: ConnectionId={ConnectionId}, Hours={Hours}",
                    connectionId,
                    hours);

                IReadOnlyList<CommandPerformanceAggregateDto> aggregates;
                using (BotActivitySource.StartServiceActivity("command_performance_aggregator", "get_aggregates"))
                {
                    aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);
                }

                // Calculate overall metrics from aggregates
                var totalCommands = aggregates.Sum(a => a.ExecutionCount);
                var avgResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.AvgMs) : 0;
                var p95ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P95Ms) : 0;
                var p99ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P99Ms) : 0;
                var errorRate = aggregates.Any() ? aggregates.Average(a => a.ErrorRate) : 0;

                // Calculate commands in the last hour (approximation: total / hours)
                var commandsLastHour = hours > 0 ? totalCommands / hours : totalCommands;

                var commandMetrics = new CommandPerformanceUpdateDto
                {
                    TotalCommands24h = totalCommands,
                    AvgResponseTimeMs = avgResponseTimeMs,
                    P95ResponseTimeMs = p95ResponseTimeMs,
                    P99ResponseTimeMs = p99ResponseTimeMs,
                    ErrorRate = errorRate,
                    CommandsLastHour = commandsLastHour,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogTrace(
                    "Command performance retrieved: TotalCommands={TotalCommands}, AvgResponseTime={AvgResponseTimeMs}ms, P95={P95ResponseTimeMs}ms, ErrorRate={ErrorRate}%",
                    commandMetrics.TotalCommands24h,
                    commandMetrics.AvgResponseTimeMs,
                    commandMetrics.P95ResponseTimeMs,
                    commandMetrics.ErrorRate);

                return commandMetrics;
            });
    }
}
