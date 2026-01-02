using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using System.Diagnostics;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the System Health dashboard.
/// Displays database performance, background services, cache statistics, and memory metrics.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class SystemHealthModel : PageModel
{
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly ILogger<SystemHealthModel> _logger;

    /// <summary>
    /// Gets the view model for the system health page.
    /// </summary>
    public SystemHealthViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemHealthModel"/> class.
    /// </summary>
    /// <param name="databaseMetricsCollector">The database metrics collector.</param>
    /// <param name="backgroundServiceHealthRegistry">The background service health registry.</param>
    /// <param name="instrumentedCache">The instrumented cache service.</param>
    /// <param name="logger">The logger.</param>
    public SystemHealthModel(
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        ILogger<SystemHealthModel> logger)
    {
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the System Health page.
    /// </summary>
    public void OnGet()
    {
        _logger.LogDebug("System Health page accessed by user {UserId}", User.Identity?.Name);
        LoadViewModel();
    }

    private void LoadViewModel()
    {
        try
        {
            // Get database metrics
            var dbMetrics = _databaseMetricsCollector.GetMetrics();
            var slowQueries = _databaseMetricsCollector.GetSlowQueries(24);

            // Get background service health
            var backgroundServices = _backgroundServiceHealthRegistry.GetAllHealth();

            // Get cache statistics
            var cacheByPrefix = _instrumentedCache.GetStatistics();

            // Calculate overall cache stats
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

            // Get system memory metrics
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            var privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
            var heapSizeMB = GC.GetTotalMemory(false) / 1024 / 1024;
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            // Calculate queries per second (approximate from recent metrics)
            var queriesPerSecond = dbMetrics.TotalQueries > 0
                ? dbMetrics.TotalQueries / 60.0 // Assuming metrics are for last minute
                : 0;

            // Determine overall system status
            var systemStatus = SystemHealthViewModel.GetSystemStatus(
                backgroundServices,
                dbMetrics.AvgQueryTimeMs,
                0); // Error count would come from dbMetrics if available

            var systemStatusClass = SystemHealthViewModel.GetSystemStatusClass(systemStatus);

            ViewModel = new SystemHealthViewModel
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
                DatabaseErrorCount = 0 // Would come from dbMetrics if available
            };

            _logger.LogDebug(
                "System Health ViewModel loaded: DbAvgQueryTime={AvgQueryTimeMs}ms, Services={ServiceCount}, CacheHitRate={HitRate:F1}%",
                dbMetrics.AvgQueryTimeMs,
                backgroundServices.Count,
                overallCacheStats.HitRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load System Health ViewModel");

            // Create a default view model in case of error
            ViewModel = new SystemHealthViewModel
            {
                SystemStatus = "Error Loading Data",
                SystemStatusClass = "health-status-error",
                DatabaseMetrics = new DatabaseMetricsDto(),
                OverallCacheStats = new CacheStatisticsDto()
            };
        }
    }
}
