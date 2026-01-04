using System.Diagnostics;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for aggregating memory diagnostics from all IMemoryReportable services.
/// Caches results to avoid expensive repeated GC calls.
/// </summary>
public class MemoryDiagnosticsService : IMemoryDiagnosticsService
{
    private readonly IEnumerable<IMemoryReportable> _memoryReportableServices;
    private readonly ILogger<MemoryDiagnosticsService> _logger;

    private MemoryDiagnosticsDto? _cachedReport;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(5);
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryDiagnosticsService"/> class.
    /// </summary>
    /// <param name="memoryReportableServices">All services implementing IMemoryReportable.</param>
    /// <param name="logger">The logger.</param>
    public MemoryDiagnosticsService(
        IEnumerable<IMemoryReportable> memoryReportableServices,
        ILogger<MemoryDiagnosticsService> logger)
    {
        _memoryReportableServices = memoryReportableServices;
        _logger = logger;
    }

    /// <inheritdoc/>
    public MemoryDiagnosticsDto GetDiagnostics()
    {
        lock (_lock)
        {
            if (_cachedReport != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedReport;
            }

            var gcInfo = GetGcGenerationSizes();
            var serviceReports = _memoryReportableServices
                .Select(s =>
                {
                    try
                    {
                        return s.GetMemoryReport();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get memory report from {ServiceName}", s.ServiceName);
                        return new ServiceMemoryReportDto
                        {
                            ServiceName = s.ServiceName,
                            EstimatedBytes = 0,
                            ItemCount = 0,
                            Details = "Error collecting memory report"
                        };
                    }
                })
                .OrderByDescending(r => r.EstimatedBytes)
                .ToList();

            var totalAccounted = serviceReports.Sum(r => r.EstimatedBytes);
            var process = Process.GetCurrentProcess();

            // Unaccounted is the difference between total committed and what we've tracked
            // This includes framework overhead, native memory, and services not yet instrumented
            var unaccounted = Math.Max(0, gcInfo.TotalCommittedBytes - totalAccounted);

            _cachedReport = new MemoryDiagnosticsDto
            {
                Timestamp = DateTime.UtcNow,
                GcGenerations = gcInfo,
                ServiceReports = serviceReports,
                TotalAccountedBytes = totalAccounted,
                UnaccountedBytes = unaccounted,
                WorkingSetBytes = process.WorkingSet64,
                ManagedHeapBytes = GC.GetTotalMemory(false)
            };

            _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

            _logger.LogDebug(
                "Memory diagnostics collected: {ServiceCount} services, {AccountedKB:F1} KB accounted, {UnaccountedMB:F2} MB unaccounted",
                serviceReports.Count,
                totalAccounted / 1024.0,
                unaccounted / (1024.0 * 1024.0));

            return _cachedReport;
        }
    }

    /// <inheritdoc/>
    public GcGenerationSizesDto GetGcGenerationSizes()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        // Calculate fragmentation: fragmented / heap size * 100
        var totalHeap = gcInfo.HeapSizeBytes;
        var fragmentedBytes = gcInfo.FragmentedBytes;
        var fragmentationPercent = totalHeap > 0
            ? (double)fragmentedBytes / totalHeap * 100.0
            : 0.0;

        return new GcGenerationSizesDto
        {
            Gen0SizeBytes = gcInfo.GenerationInfo[0].SizeAfterBytes,
            Gen1SizeBytes = gcInfo.GenerationInfo[1].SizeAfterBytes,
            Gen2SizeBytes = gcInfo.GenerationInfo[2].SizeAfterBytes,
            LohSizeBytes = gcInfo.GenerationInfo[3].SizeAfterBytes,
            // POH is index 4, available in .NET 5+
            PohSizeBytes = gcInfo.GenerationInfo.Length > 4
                ? gcInfo.GenerationInfo[4].SizeAfterBytes
                : 0,
            TotalCommittedBytes = gcInfo.TotalCommittedBytes,
            FragmentationPercent = fragmentationPercent
        };
    }
}
