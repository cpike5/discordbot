using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using System.Diagnostics;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Bot Health Metrics dashboard.
/// Displays connection status, uptime, latency, and system resource metrics.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class HealthMetricsModel : PageModel
{
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly IMemoryDiagnosticsService _memoryDiagnosticsService;
    private readonly ILogger<HealthMetricsModel> _logger;

    /// <summary>
    /// Gets the view model for the health metrics page.
    /// </summary>
    public HealthMetricsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthMetricsModel"/> class.
    /// </summary>
    /// <param name="connectionStateService">The connection state service.</param>
    /// <param name="latencyHistoryService">The latency history service.</param>
    /// <param name="memoryDiagnosticsService">The memory diagnostics service.</param>
    /// <param name="logger">The logger.</param>
    public HealthMetricsModel(
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        IMemoryDiagnosticsService memoryDiagnosticsService,
        ILogger<HealthMetricsModel> logger)
    {
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _memoryDiagnosticsService = memoryDiagnosticsService;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Health Metrics page.
    /// </summary>
    public void OnGet()
    {
        _logger.LogDebug("Health Metrics page accessed by user {UserId}", User.Identity?.Name);
        LoadViewModel();
    }

    private void LoadViewModel()
    {
        try
        {
            // Get connection state and session info
            var connectionState = _connectionStateService.GetCurrentState();
            var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            var currentLatency = _latencyHistoryService.GetCurrentLatency();

            // Get statistics
            var latencyStats = _latencyHistoryService.GetStatistics(24);
            var connectionStats7d = _connectionStateService.GetConnectionStats(7);
            var connectionEvents = _connectionStateService.GetConnectionEvents(7);
            var recentLatencySamples = _latencyHistoryService.GetSamples(1).TakeLast(10).ToList();

            // Calculate uptime percentages
            var uptime24h = _connectionStateService.GetUptimePercentage(TimeSpan.FromHours(24));
            var uptime7d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(7));
            var uptime30d = _connectionStateService.GetUptimePercentage(TimeSpan.FromDays(30));

            // Get system metrics
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            var privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            var maxAllocatedMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
            var memoryUtilizationPercent = maxAllocatedMemoryMB > 0
                ? (double)workingSetMB / maxAllocatedMemoryMB * 100
                : 0;
            var gen2Collections = GC.CollectionCount(2);
            var threadCount = process.Threads.Count;

            // Get detailed memory diagnostics
            var memoryDiagnostics = _memoryDiagnosticsService.GetDiagnostics();

            // Format session start time (UTC for client-side conversion)
            var sessionStart = _connectionStateService.GetLastConnectedTime();
            var sessionStartFormatted = sessionStart?.ToString("MMM dd, yyyy 'at' HH:mm") + " UTC" ?? "Unknown";

            // Create health DTO
            var health = new Core.DTOs.PerformanceHealthDto
            {
                Status = connectionState == Core.Interfaces.GatewayConnectionState.Connected ? "Healthy" : "Unhealthy",
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            ViewModel = new HealthMetricsViewModel
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
                CpuUsagePercent = 0, // TODO: CPU calculation requires time delta, will be handled via JavaScript/SignalR
                ThreadCount = threadCount,
                MemoryDiagnostics = memoryDiagnostics
            };

            _logger.LogDebug(
                "Health Metrics ViewModel loaded: ConnectionState={ConnectionState}, Uptime={Uptime}, Latency={LatencyMs}ms",
                connectionState,
                sessionDuration,
                currentLatency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Health Metrics ViewModel");

            // Create a default view model in case of error
            ViewModel = new HealthMetricsViewModel
            {
                UptimeFormatted = "0m",
                Uptime24HFormatted = "0%",
                Uptime7DFormatted = "0%",
                Uptime30DFormatted = "0%",
                ConnectionStateClass = "health-status-error",
                LatencyHealthClass = "gauge-fill-error",
                SessionStartFormatted = "Unknown"
            };
        }
    }
}
