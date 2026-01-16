using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// API controller for loading Performance Dashboard tab content via AJAX.
/// Returns partial view HTML for each tab panel.
/// </summary>
[ApiController]
[Route("api/performance/tabs")]
[Authorize(Policy = "RequireViewer")]
public class PerformanceTabsController : Controller
{
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IApiRequestTracker _apiRequestTracker;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly IPerformanceAlertService _alertService;
    private readonly IMemoryDiagnosticsService _memoryDiagnosticsService;
    private readonly ICpuHistoryService _cpuHistoryService;
    private readonly ILogger<PerformanceTabsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceTabsController"/> class.
    /// </summary>
    public PerformanceTabsController(
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IApiRequestTracker apiRequestTracker,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        IPerformanceAlertService alertService,
        IMemoryDiagnosticsService memoryDiagnosticsService,
        ICpuHistoryService cpuHistoryService,
        ILogger<PerformanceTabsController> logger)
    {
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _apiRequestTracker = apiRequestTracker;
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _alertService = alertService;
        _memoryDiagnosticsService = memoryDiagnosticsService;
        _cpuHistoryService = cpuHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the Overview tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the overview tab.</returns>
    [HttpGet("overview")]
    [Produces("text/html")]
    public async Task<IActionResult> GetOverviewTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading Overview tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await BuildOverviewViewModelAsync(hours);
            return PartialView("~/Pages/Admin/Performance/Tabs/_OverviewTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Overview tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load overview data"));
        }
    }

    /// <summary>
    /// Gets the Health Metrics tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the health tab.</returns>
    [HttpGet("health")]
    [Produces("text/html")]
    public IActionResult GetHealthTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading Health tab content for {Hours} hours", hours);

        try
        {
            var viewModel = BuildHealthMetricsViewModel();
            return PartialView("~/Pages/Admin/Performance/Tabs/_HealthTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Health tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load health metrics"));
        }
    }

    /// <summary>
    /// Gets the Commands tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the commands tab.</returns>
    [HttpGet("commands")]
    [Produces("text/html")]
    public async Task<IActionResult> GetCommandsTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading Commands tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await BuildCommandsViewModelAsync(hours);
            return PartialView("~/Pages/Admin/Performance/Tabs/_CommandsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Commands tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load command performance data"));
        }
    }

    /// <summary>
    /// Gets the API Metrics tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the API tab.</returns>
    [HttpGet("api")]
    [Produces("text/html")]
    public IActionResult GetApiTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading API tab content for {Hours} hours", hours);

        try
        {
            var viewModel = BuildApiMetricsViewModel(hours);
            return PartialView("~/Pages/Admin/Performance/Tabs/_ApiTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load API tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load API metrics"));
        }
    }

    /// <summary>
    /// Gets the System Health tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the system tab.</returns>
    [HttpGet("system")]
    [Produces("text/html")]
    public IActionResult GetSystemTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading System tab content for {Hours} hours", hours);

        try
        {
            var viewModel = BuildSystemHealthViewModel();
            return PartialView("~/Pages/Admin/Performance/Tabs/_SystemTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load System tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load system health data"));
        }
    }

    /// <summary>
    /// Gets the Alerts tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the alerts tab.</returns>
    [HttpGet("alerts")]
    [Produces("text/html")]
    public async Task<IActionResult> GetAlertsTab(
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Alerts tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await BuildAlertsViewModelAsync(cancellationToken);
            return PartialView("~/Pages/Admin/Performance/Tabs/_AlertsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Alerts tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load alerts data"));
        }
    }

    #region ViewModel Builders

    private async Task<PerformanceOverviewViewModel> BuildOverviewViewModelAsync(int hours)
    {
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

        var uptime30d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(30));

        var commandAggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);
        var throughputData = await _commandPerformanceAggregator.GetThroughputAsync(1, "hour");
        var commandsToday = throughputData.Sum(t => t.Count);

        var totalCommands = commandAggregates.Sum(a => a.ExecutionCount);
        var totalErrors = commandAggregates.Sum(a => (int)(a.ExecutionCount * (a.ErrorRate / 100.0)));
        var overallErrorRate = totalCommands > 0 ? (totalErrors * 100.0 / totalCommands) : 0;
        var avgResponseTime = commandAggregates.Any() ? commandAggregates.Average(a => a.AvgMs) : 0;

        var activeAlerts = await _alertService.GetActiveIncidentsAsync();
        var recentAlerts = activeAlerts.OrderByDescending(a => a.TriggeredAt).Take(5).ToList();

        long workingSetMB;
        using (var process = Process.GetCurrentProcess())
        {
            workingSetMB = process.WorkingSet64 / 1024 / 1024;
        }
        var maxMemoryMB = 1024;
        var memoryUsagePercent = (workingSetMB * 100.0) / maxMemoryMB;

        var apiUsage = _apiRequestTracker.GetUsageStatistics(1);
        var totalApiRequests = apiUsage.Sum(u => u.RequestCount);
        var apiLimit = 50;
        var apiUsagePercent = totalApiRequests > 0 ? Math.Min((totalApiRequests * 100.0) / apiLimit, 100) : 0;

        var overallHealthStatus = DetermineOverallStatus(overallStatus, activeAlerts.Count);

        return new PerformanceOverviewViewModel
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
            ApiRateLimitFormatted = $"{totalApiRequests} / {apiLimit} requests",
            ApiRateLimitPercent = apiUsagePercent
        };
    }

    private HealthMetricsViewModel BuildHealthMetricsViewModel()
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

        long workingSetMB2;
        long privateMemoryMB;
        int threadCount;
        using (var process = Process.GetCurrentProcess())
        {
            workingSetMB2 = process.WorkingSet64 / 1024 / 1024;
            privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            threadCount = process.Threads.Count;
        }
        var maxAllocatedMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
        var memoryUtilizationPercent = maxAllocatedMemoryMB > 0
            ? (double)workingSetMB2 / maxAllocatedMemoryMB * 100
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

        return new HealthMetricsViewModel
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
            WorkingSetMB = workingSetMB2,
            PrivateMemoryMB = privateMemoryMB,
            MaxAllocatedMemoryMB = maxAllocatedMemoryMB,
            MemoryUtilizationPercent = memoryUtilizationPercent,
            Gen2Collections = gen2Collections,
            CpuUsagePercent = _cpuHistoryService.GetCurrentCpu(),
            ThreadCount = threadCount,
            MemoryDiagnostics = memoryDiagnostics
        };
    }

    private async Task<CommandPerformanceViewModel> BuildCommandsViewModelAsync(int hours)
    {
        var aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);

        IReadOnlyList<SlowestCommandDto> slowest = Array.Empty<SlowestCommandDto>();
        try
        {
            slowest = await _commandPerformanceAggregator.GetSlowestCommandsAsync(10, hours);
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
            .Where(s => s.DurationMs > 3000)
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

    private ApiRateLimitsViewModel BuildApiMetricsViewModel(int hours)
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

    private SystemHealthViewModel BuildSystemHealthViewModel()
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
        long privateMemoryMB2;
        using (var process = Process.GetCurrentProcess())
        {
            workingSetMB = process.WorkingSet64 / 1024 / 1024;
            privateMemoryMB2 = process.PrivateMemorySize64 / 1024 / 1024;
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
            PrivateMemoryMB = privateMemoryMB2,
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

    private async Task<AlertsPageViewModel> BuildAlertsViewModelAsync(CancellationToken cancellationToken)
    {
        var activeIncidentsTask = _alertService.GetActiveIncidentsAsync(cancellationToken);
        var alertConfigsTask = _alertService.GetAllConfigsAsync(cancellationToken);
        var recentIncidentsTask = _alertService.GetIncidentHistoryAsync(
            new IncidentQueryDto { PageNumber = 1, PageSize = 10 },
            cancellationToken);
        var autoRecoveryEventsTask = _alertService.GetAutoRecoveryEventsAsync(10, cancellationToken);
        var alertFrequencyTask = _alertService.GetAlertFrequencyDataAsync(30, cancellationToken);
        var summaryTask = _alertService.GetActiveAlertSummaryAsync(cancellationToken);

        await Task.WhenAll(
            activeIncidentsTask,
            alertConfigsTask,
            recentIncidentsTask,
            autoRecoveryEventsTask,
            alertFrequencyTask,
            summaryTask);

        return new AlertsPageViewModel
        {
            ActiveIncidents = activeIncidentsTask.Result,
            AlertConfigs = alertConfigsTask.Result,
            RecentIncidents = recentIncidentsTask.Result.Items,
            AutoRecoveryEvents = autoRecoveryEventsTask.Result,
            AlertFrequencyData = alertFrequencyTask.Result,
            AlertSummary = summaryTask.Result
        };
    }

    #endregion

    #region Helpers

    private static string DetermineOverallStatus(string serviceStatus, int activeAlertCount)
    {
        if (serviceStatus.Equals("Critical", StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        if (activeAlertCount > 0 || serviceStatus.Equals("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static ContentResult CreateErrorHtml(string message)
    {
        var html = $@"
<div class=""tab-error-state"">
    <div class=""tab-error-content"">
        <svg class=""tab-error-icon"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
            <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"" />
        </svg>
        <h3 class=""tab-error-title"">Error Loading Content</h3>
        <p class=""tab-error-message"">{System.Web.HttpUtility.HtmlEncode(message)}</p>
        <button class=""btn btn-secondary tab-retry-btn"" onclick=""window.PerformanceTabs?.retryCurrentTab()"">
            <svg class=""btn-svg-icon"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"" />
            </svg>
            Retry
        </button>
    </div>
</div>";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html",
            StatusCode = 500
        };
    }

    #endregion
}
