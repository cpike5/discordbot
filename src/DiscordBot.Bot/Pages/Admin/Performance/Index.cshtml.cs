using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using System.Diagnostics;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Performance Overview dashboard.
/// Displays aggregated performance metrics, system health, and active alerts.
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
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Gets the view model for the performance overview page.
    /// </summary>
    public PerformanceOverviewViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="connectionStateService">The connection state service.</param>
    /// <param name="latencyHistoryService">The latency history service.</param>
    /// <param name="commandPerformanceAggregator">The command performance aggregator.</param>
    /// <param name="apiRequestTracker">The API request tracker.</param>
    /// <param name="backgroundServiceHealthRegistry">The background service health registry.</param>
    /// <param name="alertService">The performance alert service.</param>
    /// <param name="logger">The logger.</param>
    public IndexModel(
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IApiRequestTracker apiRequestTracker,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IPerformanceAlertService alertService,
        ILogger<IndexModel> logger)
    {
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _apiRequestTracker = apiRequestTracker;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _alertService = alertService;
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

            // Get command metrics
            var commandAggregates = await _commandPerformanceAggregator.GetAggregatesAsync(24);
            var throughputData = await _commandPerformanceAggregator.GetThroughputAsync(1, "hour"); // Last hour for "today"
            var commandsToday = throughputData.Sum(t => t.Count);

            var totalCommands = commandAggregates.Sum(a => a.ExecutionCount);
            var totalErrors = commandAggregates.Sum(a => (int)(a.ExecutionCount * (a.ErrorRate / 100.0)));
            var overallErrorRate = totalCommands > 0 ? (totalErrors * 100.0 / totalCommands) : 0;
            var avgResponseTime = commandAggregates.Any() ? commandAggregates.Average(a => a.AvgMs) : 0;

            // Get active alerts
            var activeAlerts = await _alertService.GetActiveIncidentsAsync();
            var recentAlerts = activeAlerts.OrderByDescending(a => a.TriggeredAt).Take(5).ToList();

            // Get system metrics
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            var maxMemoryMB = 1024; // Placeholder - could be from config or system info
            var memoryUsagePercent = (workingSetMB * 100.0) / maxMemoryMB;

            // Get API metrics
            var apiUsage = _apiRequestTracker.GetUsageStatistics(1); // Last hour
            var totalApiRequests = apiUsage.Sum(u => u.RequestCount);
            var apiLimit = 50; // Placeholder - Discord's per-second rate limit varies by endpoint
            var apiUsagePercent = totalApiRequests > 0 ? Math.Min((totalApiRequests * 100.0) / apiLimit, 100) : 0;

            // Determine overall status based on alerts and health
            var overallHealthStatus = DetermineOverallStatus(overallStatus, activeAlerts.Count);

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
                CpuUsagePercent = 0, // CPU requires time delta, will be handled via JavaScript
                DatabaseConnectionsFormatted = "8 / 20", // Placeholder - would need actual DB pool metrics
                ApiRateLimitFormatted = $"{totalApiRequests} / {apiLimit} requests",
                ApiRateLimitPercent = apiUsagePercent
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

            // Create a default view model in case of error
            ViewModel = new PerformanceOverviewViewModel
            {
                OverallStatus = "Critical",
                Uptime30DaysPercent = 0,
                MemoryUsageFormatted = "Unknown",
                DatabaseConnectionsFormatted = "Unknown",
                ApiRateLimitFormatted = "Unknown"
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
}
