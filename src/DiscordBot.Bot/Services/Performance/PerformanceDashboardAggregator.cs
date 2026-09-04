using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using IAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace DiscordBot.Bot.Services.Performance;

/// <summary>
/// Default implementation of <see cref="IPerformanceDashboardAggregator"/>. Logic moved
/// verbatim from <c>DiscordBot.Bot.Pages.Admin.Performance.IndexModel</c>.
/// </summary>
public class PerformanceDashboardAggregator : IPerformanceDashboardAggregator
{
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IApiRequestTracker _apiRequestTracker;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IPerformanceAlertService _alertService;
    private readonly ICpuHistoryService _cpuHistoryService;
    private readonly IMemoryDiagnosticsService _memoryDiagnosticsService;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<PerformanceDashboardAggregator> _logger;

    public PerformanceDashboardAggregator(
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IApiRequestTracker apiRequestTracker,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IPerformanceAlertService alertService,
        ICpuHistoryService cpuHistoryService,
        IMemoryDiagnosticsService memoryDiagnosticsService,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IInstrumentedCache instrumentedCache,
        IAuthorizationService authorizationService,
        ILogger<PerformanceDashboardAggregator> logger)
    {
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _apiRequestTracker = apiRequestTracker;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _alertService = alertService;
        _cpuHistoryService = cpuHistoryService;
        _memoryDiagnosticsService = memoryDiagnosticsService;
        _databaseMetricsCollector = databaseMetricsCollector;
        _instrumentedCache = instrumentedCache;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<PerformanceDashboardOverview> BuildOverviewAsync()
    {
        try
        {
            // Get bot health and connection state
            var connectionState = _connectionStateService.GetCurrentState();
            var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            var currentLatency = _latencyHistoryService.GetCurrentLatency();
            var overallStatus = _backgroundServiceHealthRegistry.GetOverallStatus();

            var botHealth = new PerformanceHealthDto
            {
                Status = overallStatus,
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            // Get uptime percentage
            var uptime30d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(30));

            // Start async data retrieval in parallel
            var aggregatesTask = _commandPerformanceAggregator.GetAggregatesAsync(24);
            var throughputTask = _commandPerformanceAggregator.GetThroughputAsync(1, "hour"); // Last hour for "today"
            var alertsTask = _alertService.GetActiveIncidentsAsync();

            await Task.WhenAll(aggregatesTask, throughputTask, alertsTask);

            var commandAggregates = await aggregatesTask;
            var throughputData = await throughputTask;
            var activeAlerts = await alertsTask;

            // Process command metrics
            var commandsToday = throughputData.Sum(t => t.Count);

            var totalCommands = commandAggregates.Sum(a => a.ExecutionCount);
            var totalErrors = commandAggregates.Sum(a => (int)(a.ExecutionCount * (a.ErrorRate / 100.0)));
            var overallErrorRate = totalCommands > 0 ? (totalErrors * 100.0 / totalCommands) : 0;
            var avgResponseTime = commandAggregates.Any() ? commandAggregates.Average(a => a.AvgMs) : 0;

            // Get recent alerts
            var recentAlerts = activeAlerts.OrderByDescending(a => a.TriggeredAt).Take(5).ToList();

            // Get system metrics
            long workingSetMB;
            using (var process = Process.GetCurrentProcess())
            {
                workingSetMB = process.WorkingSet64 / 1024 / 1024;
            }
            var maxMemoryMB = (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
            var memoryUsagePercent = (workingSetMB * 100.0) / maxMemoryMB;

            // Get API metrics
            var apiUsage = _apiRequestTracker.GetUsageStatistics(1); // Last hour
            var totalApiRequests = apiUsage.Sum(u => u.RequestCount);
            var rateLimitEvents = _apiRequestTracker.GetRateLimitEvents(1);
            var rateLimitHits = rateLimitEvents.Count;

            // Determine overall status based on alerts and health
            var overallHealthStatus = DetermineOverallStatus(overallStatus, activeAlerts.Count);

            var overview = new PerformanceOverviewViewModel
            {
                OverallStatus = overallHealthStatus,
                BotHealth = botHealth,
                Uptime30DaysPercent = uptime30d,
                AvgCommandResponseMs = avgResponseTime,
                CommandsToday = commandsToday,
                ErrorRate = overallErrorRate,
                ActiveAlertCount = activeAlerts.Count,
                RecentAlerts = recentAlerts,
                MemoryUsageMB = workingSetMB,
                MemoryUsagePercent = memoryUsagePercent,
                MemoryUsageFormatted = $"{workingSetMB} MB / {maxMemoryMB} MB",
                CpuUsagePercent = _cpuHistoryService.GetCurrentCpu(),
                ApiRateLimitFormatted = rateLimitHits > 0
                    ? $"{totalApiRequests} requests ({rateLimitHits} rate limited)"
                    : $"{totalApiRequests} requests (last hour)",
                ApiRateLimitPercent = rateLimitHits > 0 ? Math.Min(rateLimitHits * 10.0, 100) : 0
            };

            var shell = new PerformanceShellViewModel
            {
                OverallStatus = overallHealthStatus,
                ActiveAlertCount = activeAlerts.Count,
                ActiveTab = "overview",
                TimeRangeHours = 24,
                IsLive = true
            };

            _logger.LogDebug(
                "Performance Overview ViewModel loaded: OverallStatus={OverallStatus}, Uptime={Uptime:F1}%, ActiveAlerts={ActiveAlerts}",
                overallHealthStatus,
                uptime30d,
                activeAlerts.Count);

            return new PerformanceDashboardOverview { Overview = overview, Shell = shell };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Performance Overview ViewModel");

            var overview = new PerformanceOverviewViewModel
            {
                OverallStatus = "Critical",
                Uptime30DaysPercent = 0,
                MemoryUsageFormatted = "Unknown",
                ApiRateLimitFormatted = "Unknown"
            };

            var shell = new PerformanceShellViewModel
            {
                OverallStatus = "Critical",
                ActiveAlertCount = 0,
                ActiveTab = "overview",
                TimeRangeHours = 24,
                IsLive = false
            };

            return new PerformanceDashboardOverview { Overview = overview, Shell = shell };
        }
    }

    private static string DetermineOverallStatus(string serviceStatus, int activeAlertCount)
    {
        // Critical if services are unhealthy or there are critical alerts
        if (serviceStatus.Equals("Critical", StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        // Warning if there are any active alerts or services are degraded
        if (activeAlertCount > 0 || serviceStatus.Equals("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Healthy";
    }

    public Task<HealthMetricsViewModel> BuildHealthMetricsAsync()
    {
        try
        {
            var connectionState = _connectionStateService.GetCurrentState();
            var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            var currentLatency = _latencyHistoryService.GetCurrentLatency();

            var latencyStats = _latencyHistoryService.GetStatistics(24);
            var connectionStats7d = _connectionStateService.GetConnectionStats(7);
            var connectionEvents = _connectionStateService.GetConnectionEvents(7);
            var recentLatencySamples = _latencyHistoryService.GetSamples(1).TakeLast(10).ToList();

            var uptime24h = _connectionStateService.GetUptimePercentage(TimeSpan.FromHours(24));
            var uptime7d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(7));
            var uptime30d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(30));

            long workingSetMB;
            long privateMemoryMB;
            int threadCount;
            using (var process = Process.GetCurrentProcess())
            {
                workingSetMB = process.WorkingSet64 / 1024 / 1024;
                privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
                threadCount = process.Threads.Count;
            }
            var maxAllocatedMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
            var memoryUtilizationPercent = maxAllocatedMemoryMB > 0
                ? (double)workingSetMB / maxAllocatedMemoryMB * 100
                : 0;
            var gen2Collections = GC.CollectionCount(2);

            var memoryDiagnostics = _memoryDiagnosticsService.GetDiagnostics();

            var sessionStart = _connectionStateService.GetLastConnectedTime();
            var sessionStartFormatted = sessionStart?.ToString("MMM dd, yyyy 'at' HH:mm") + " UTC" ?? "Unknown";

            var health = new PerformanceHealthDto
            {
                Status = connectionState == GatewayConnectionState.Connected ? "Healthy" : "Unhealthy",
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            return Task.FromResult(new HealthMetricsViewModel
            {
                Health = health,
                LatencyStats = latencyStats,
                ConnectionStats = connectionStats7d,
                RecentConnectionEvents = connectionEvents,
                RecentLatencySamples = recentLatencySamples,
                UptimeFormatted = HealthMetricsViewModel.FormatUptime(sessionDuration),
                Uptime24HFormatted = $"{uptime24h:F1}%",
                Uptime7DFormatted = $"{uptime7d:F1}%",
                Uptime30DFormatted = $"{uptime30d:F1}%",
                ConnectionStateClass = HealthMetricsViewModel.GetConnectionStateClass(connectionState.ToString()),
                LatencyHealthClass = HealthMetricsViewModel.GetLatencyHealthClass(currentLatency),
                SessionStartFormatted = sessionStartFormatted,
                SessionStartUtc = sessionStart,
                WorkingSetMB = workingSetMB,
                PrivateMemoryMB = privateMemoryMB,
                MaxAllocatedMemoryMB = maxAllocatedMemoryMB,
                MemoryUtilizationPercent = memoryUtilizationPercent,
                Gen2Collections = gen2Collections,
                CpuUsagePercent = _cpuHistoryService.GetCurrentCpu(),
                ThreadCount = threadCount,
                MemoryDiagnostics = memoryDiagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build HealthMetricsViewModel");
            return Task.FromResult(new HealthMetricsViewModel
            {
                UptimeFormatted = "0m",
                Uptime24HFormatted = "0%",
                Uptime7DFormatted = "0%",
                Uptime30DFormatted = "0%",
                ConnectionStateClass = "health-status-error",
                LatencyHealthClass = "gauge-fill-error",
                SessionStartFormatted = "Unknown"
            });
        }
    }

    public async Task<CommandPerformanceViewModel> BuildCommandPerformanceAsync(int hours = 24)
    {
        try
        {
            var aggregatesTask = _commandPerformanceAggregator.GetAggregatesAsync(hours);
            var slowestTask = _commandPerformanceAggregator.GetSlowestCommandsAsync(10, hours);

            var aggregates = await aggregatesTask;

            IReadOnlyList<SlowestCommandDto> slowest = Array.Empty<SlowestCommandDto>();
            try
            {
                slowest = await slowestTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch slowest commands for {Hours} hours", hours);
            }

            var totalCommands = aggregates.Sum(a => a.ExecutionCount);
            var avgResponseTime = aggregates.Any() ? aggregates.Average(a => a.AvgMs) : 0;
            var errorRate = totalCommands > 0
                ? aggregates.Sum(a => a.ExecutionCount * a.ErrorRate / 100.0) / totalCommands * 100
                : 0;
            var p99 = aggregates.Any() ? aggregates.Max(a => a.P99Ms) : 0;
            var p95 = aggregates.Any() ? aggregates.Max(a => a.P95Ms) : 0;
            var p50 = aggregates.Any() ? aggregates.Average(a => a.P50Ms) : 0;

            var timeouts = slowest
                .Where(s => s.DurationMs > DiscordConstants.InteractionTimeoutMs)
                .GroupBy(s => s.CommandName)
                .Select(g => new CommandTimeoutDto
                {
                    CommandName = g.Key,
                    TimeoutCount = g.Count(),
                    LastTimeout = g.Max(x => x.ExecutedAt),
                    AvgResponseBeforeTimeout = g.Average(x => x.DurationMs),
                    Status = g.Max(x => x.ExecutedAt) > DateTime.UtcNow.AddHours(-2)
                        ? "Investigating"
                        : "Resolved"
                })
                .ToList();

            return new CommandPerformanceViewModel
            {
                TotalCommands = totalCommands,
                AvgResponseTimeMs = avgResponseTime,
                ErrorRate = errorRate,
                P99ResponseTimeMs = p99,
                P50Ms = p50,
                P95Ms = p95,
                SlowestCommands = slowest,
                TimeoutCount = timeouts.Sum(t => t.TimeoutCount),
                RecentTimeouts = timeouts,
                AvgResponseTimeTrend = 0,
                ErrorRateTrend = 0,
                P99Trend = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build CommandPerformanceViewModel");
            return new CommandPerformanceViewModel
            {
                TotalCommands = 0,
                AvgResponseTimeMs = 0,
                ErrorRate = 0,
                P99ResponseTimeMs = 0,
                P50Ms = 0,
                P95Ms = 0,
                SlowestCommands = Array.Empty<SlowestCommandDto>(),
                RecentTimeouts = Array.Empty<CommandTimeoutDto>(),
                TimeoutCount = 0,
                AvgResponseTimeTrend = 0,
                ErrorRateTrend = 0,
                P99Trend = 0
            };
        }
    }

    public ApiRateLimitsViewModel BuildApiRateLimits(int hours = 24)
    {
        try
        {
            var usageByCategory = _apiRequestTracker.GetUsageStatistics(hours);
            var totalRequests = _apiRequestTracker.GetTotalRequests(hours);
            var rateLimitEvents = _apiRequestTracker.GetRateLimitEvents(hours);
            var latencyStats = _apiRequestTracker.GetLatencyStatistics(hours);

            return new ApiRateLimitsViewModel
            {
                TotalRequests = totalRequests,
                RateLimitHits = rateLimitEvents.Count,
                AvgLatencyMs = latencyStats.AvgLatencyMs,
                P95LatencyMs = latencyStats.P95LatencyMs,
                UsageByCategory = usageByCategory,
                RecentRateLimitEvents = rateLimitEvents.OrderByDescending(e => e.Timestamp).Take(20).ToList(),
                LatencyStats = latencyStats,
                Hours = hours
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build ApiRateLimitsViewModel");
            return new ApiRateLimitsViewModel
            {
                TotalRequests = 0,
                RateLimitHits = 0,
                AvgLatencyMs = 0,
                P95LatencyMs = 0,
                UsageByCategory = Array.Empty<ApiUsageDto>(),
                RecentRateLimitEvents = Array.Empty<RateLimitEventDto>(),
                LatencyStats = null
            };
        }
    }

    public SystemHealthViewModel BuildSystemHealth()
    {
        try
        {
            var dbMetrics = _databaseMetricsCollector.GetMetrics();
            var slowQueries = _databaseMetricsCollector.GetSlowQueries(24);

            var backgroundServices = _backgroundServiceHealthRegistry.GetAllHealth();

            var cacheByPrefix = _instrumentedCache.GetStatistics();

            var totalHits = cacheByPrefix.Sum(c => c.Hits);
            var totalMisses = cacheByPrefix.Sum(c => c.Misses);
            var totalCount = totalHits + totalMisses;

            var overallCacheStats = new CacheStatisticsDto
            {
                KeyPrefix = "Overall",
                Hits = totalHits,
                Misses = totalMisses,
                HitRate = totalCount > 0 ? (double)totalHits / totalCount * 100 : 0,
                Size = cacheByPrefix.Sum(c => c.Size)
            };

            long workingSetMB;
            long privateMemoryMB;
            using (var process = Process.GetCurrentProcess())
            {
                workingSetMB = process.WorkingSet64 / 1024 / 1024;
                privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            }
            var heapSizeMB = GC.GetTotalMemory(false) / 1024 / 1024;
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            var queriesPerSecond = dbMetrics.TotalQueries > 0
                ? dbMetrics.TotalQueries / 60.0
                : 0;

            var systemStatus = SystemHealthViewModel.GetSystemStatus(
                backgroundServices,
                dbMetrics.AvgQueryTimeMs,
                0);

            var systemStatusClass = SystemHealthViewModel.GetSystemStatusClass(systemStatus);

            return new SystemHealthViewModel
            {
                DatabaseMetrics = dbMetrics,
                SlowQueries = slowQueries,
                BackgroundServices = backgroundServices,
                OverallCacheStats = overallCacheStats,
                CacheStatsByPrefix = cacheByPrefix,
                WorkingSetMB = workingSetMB,
                PrivateMemoryMB = privateMemoryMB,
                HeapSizeMB = heapSizeMB,
                Gen0Collections = gen0Collections,
                Gen1Collections = gen1Collections,
                Gen2Collections = gen2Collections,
                SystemStatus = systemStatus,
                SystemStatusClass = systemStatusClass,
                QueriesPerSecond = queriesPerSecond,
                DatabaseErrorCount = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build SystemHealthViewModel");
            return new SystemHealthViewModel
            {
                SystemStatus = "Error Loading Data",
                SystemStatusClass = "health-status-error",
                DatabaseMetrics = new DatabaseMetricsDto(),
                OverallCacheStats = new CacheStatisticsDto()
            };
        }
    }

    public async Task<AlertsPageViewModel> BuildAlertsPageAsync(ClaimsPrincipal user)
    {
        try
        {
            var authResult = await _authorizationService.AuthorizeAsync(user, "RequireAdmin");
            var canEdit = authResult.Succeeded;

            // Execute sequentially — DbContext is not thread-safe
            var activeIncidents = await _alertService.GetActiveIncidentsAsync();
            var alertConfigs = await _alertService.GetAllConfigsAsync();
            var recentIncidents = await _alertService.GetIncidentHistoryAsync(
                new IncidentQueryDto { PageNumber = 1, PageSize = 10 });
            var autoRecoveryEvents = await _alertService.GetAutoRecoveryEventsAsync(10);
            var alertFrequency = await _alertService.GetAlertFrequencyDataAsync(30);
            var alertSummary = await _alertService.GetActiveAlertSummaryAsync();

            return new AlertsPageViewModel
            {
                ActiveIncidents = activeIncidents,
                AlertConfigs = alertConfigs,
                RecentIncidents = recentIncidents.Items,
                AutoRecoveryEvents = autoRecoveryEvents,
                AlertFrequencyData = alertFrequency,
                AlertSummary = alertSummary,
                CanEdit = canEdit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build AlertsPageViewModel");
            return new AlertsPageViewModel();
        }
    }
}
