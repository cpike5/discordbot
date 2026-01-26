using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Pages;
using System.Diagnostics;
using IAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Performance Overview dashboard.
/// Displays aggregated performance metrics, system health, and active alerts.
/// Uses a shell layout with client-side tab switching.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
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
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Gets the view model for the performance overview page content.
    /// </summary>
    public PerformanceOverviewViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the shell view model for the performance dashboard layout.
    /// </summary>
    public PerformanceShellViewModel ShellViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
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
        ILogger<IndexModel> logger)
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

    /// <summary>
    /// Handles GET requests for the Performance Overview page.
    /// </summary>
    public async Task OnGetAsync()
    {
        _logger.LogDebug("Performance Overview page accessed by user {UserId}", User.Identity?.Name);
        await LoadViewModelAsync();
    }

    /// <summary>
    /// Handles AJAX requests for tab content partial views.
    /// </summary>
    /// <param name="tabId">The ID of the tab to load.</param>
    /// <param name="hours">The time range in hours (24, 168, or 720).</param>
    /// <returns>The partial view for the requested tab.</returns>
    public async Task<IActionResult> OnGetPartialAsync(string tabId, int hours = 24)
    {
        _logger.LogDebug("Loading partial content for tab {TabId} with hours={Hours}", tabId, hours);

        // Validate hours parameter
        if (hours != 24 && hours != 168 && hours != 720)
        {
            hours = 24;
        }

        return tabId?.ToLowerInvariant() switch
        {
            "overview" => await LoadOverviewTabAsync(),
            "health" => await LoadHealthTabAsync(),
            "commands" => await LoadCommandsTabAsync(hours),
            "api" => LoadApiTab(hours),
            "system" => LoadSystemTab(),
            "alerts" => await LoadAlertsTabAsync(),
            _ => HandleInvalidTab(tabId)
        };
    }

    private async Task<IActionResult> LoadOverviewTabAsync()
    {
        await LoadViewModelAsync();
        return Partial("Tabs/_OverviewTab", ViewModel);
    }

    private async Task<IActionResult> LoadHealthTabAsync()
    {
        var viewModel = await BuildHealthMetricsViewModelAsync();
        return Partial("Tabs/_HealthTab", viewModel);
    }

    private async Task<IActionResult> LoadCommandsTabAsync(int hours)
    {
        var viewModel = await BuildCommandPerformanceViewModelAsync(hours);
        return Partial("Tabs/_CommandsTab", viewModel);
    }

    private IActionResult LoadApiTab(int hours)
    {
        var viewModel = BuildApiRateLimitsViewModel(hours);
        return Partial("Tabs/_ApiTab", viewModel);
    }

    private IActionResult LoadSystemTab()
    {
        var viewModel = BuildSystemHealthViewModel();
        return Partial("Tabs/_SystemTab", viewModel);
    }

    private async Task<IActionResult> LoadAlertsTabAsync()
    {
        var viewModel = await BuildAlertsPageViewModelAsync();
        return Partial("Tabs/_AlertsTab", viewModel);
    }

    private IActionResult HandleInvalidTab(string? tabId)
    {
        _logger.LogWarning("Invalid tab ID requested: {TabId}", tabId);
        return NotFound();
    }

    private async Task LoadViewModelAsync()
    {
        try
        {
            // Get bot health and connection state
            var connectionState = _connectionStateService.GetCurrentState();
            var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            var currentLatency = _latencyHistoryService.GetCurrentLatency();
            var overallStatus = _backgroundServiceHealthRegistry.GetOverallStatus();

            var botHealth = new Core.DTOs.PerformanceHealthDto
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
            var maxMemoryMB = 1024; // Placeholder - could be from config or system info
            var memoryUsagePercent = (workingSetMB * 100.0) / maxMemoryMB;

            // Get API metrics
            var apiUsage = _apiRequestTracker.GetUsageStatistics(1); // Last hour
            var totalApiRequests = apiUsage.Sum(u => u.RequestCount);
            var apiLimit = 50; // Placeholder - Discord's per-second rate limit varies by endpoint
            var apiUsagePercent = totalApiRequests > 0 ? Math.Min((totalApiRequests * 100.0) / apiLimit, 100) : 0;

            // Determine overall status based on alerts and health
            var overallHealthStatus = DetermineOverallStatus(overallStatus, activeAlerts.Count);

            // Create the overview content view model
            ViewModel = new PerformanceOverviewViewModel
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

            // Create the shell view model
            ShellViewModel = new PerformanceShellViewModel
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Performance Overview ViewModel");

            // Create default view models in case of error
            ViewModel = new PerformanceOverviewViewModel
            {
                OverallStatus = "Critical",
                Uptime30DaysPercent = 0,
                MemoryUsageFormatted = "Unknown",
                ApiRateLimitFormatted = "Unknown"
            };

            ShellViewModel = new PerformanceShellViewModel
            {
                OverallStatus = "Critical",
                ActiveAlertCount = 0,
                ActiveTab = "overview",
                TimeRangeHours = 24,
                IsLive = false
            };
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

    private Task<HealthMetricsViewModel> BuildHealthMetricsViewModelAsync()
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

    private async Task<CommandPerformanceViewModel> BuildCommandPerformanceViewModelAsync(int hours = 24)
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

    private ApiRateLimitsViewModel BuildApiRateLimitsViewModel(int hours = 24)
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

    private SystemHealthViewModel BuildSystemHealthViewModel()
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

    private async Task<AlertsPageViewModel> BuildAlertsPageViewModelAsync()
    {
        try
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdmin");
            var canEdit = authResult.Succeeded;

            var activeIncidentsTask = _alertService.GetActiveIncidentsAsync();
            var alertConfigsTask = _alertService.GetAllConfigsAsync();
            var recentIncidentsTask = _alertService.GetIncidentHistoryAsync(
                new IncidentQueryDto { PageNumber = 1, PageSize = 10 });
            var autoRecoveryEventsTask = _alertService.GetAutoRecoveryEventsAsync(10);
            var alertFrequencyTask = _alertService.GetAlertFrequencyDataAsync(30);
            var summaryTask = _alertService.GetActiveAlertSummaryAsync();

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
                AlertSummary = summaryTask.Result,
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
