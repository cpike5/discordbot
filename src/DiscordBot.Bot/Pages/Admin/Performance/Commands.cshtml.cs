using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Command Performance Analytics page.
/// Displays response times, throughput, error tracking, and timeout analysis for Discord bot commands.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class CommandsModel : PageModel
{
    private readonly ICommandPerformanceAggregator _performanceAggregator;
    private readonly ILogger<CommandsModel> _logger;

    /// <summary>
    /// Gets the view model for the command performance page.
    /// </summary>
    public CommandPerformanceViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets or sets the number of hours of history to display (24, 168, or 720).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; } = 24;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandsModel"/> class.
    /// </summary>
    /// <param name="performanceAggregator">The command performance aggregator service.</param>
    /// <param name="logger">The logger.</param>
    public CommandsModel(
        ICommandPerformanceAggregator performanceAggregator,
        ILogger<CommandsModel> logger)
    {
        _performanceAggregator = performanceAggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Command Performance page.
    /// </summary>
    public async Task OnGetAsync()
    {
        _logger.LogDebug("Command Performance page accessed by user {UserId}, hours={Hours}",
            User.Identity?.Name, Hours);

        try
        {
            await LoadViewModelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load command performance data for {Hours} hours", Hours);
            ViewModel = CreateEmptyViewModel();
        }
    }

    /// <summary>
    /// Loads the view model with command performance data from the aggregator service.
    /// </summary>
    private async Task LoadViewModelAsync()
    {
        _logger.LogDebug("LoadViewModelAsync: Fetching aggregates for {Hours} hours", Hours);

        // Fetch aggregated metrics
        var aggregates = await _performanceAggregator.GetAggregatesAsync(Hours);

        _logger.LogDebug("LoadViewModelAsync: Retrieved {Count} aggregates", aggregates.Count);

        // Fetch slowest commands separately - if this fails, we still want to show aggregate data
        IReadOnlyList<Core.DTOs.SlowestCommandDto> slowest = Array.Empty<Core.DTOs.SlowestCommandDto>();
        try
        {
            slowest = await _performanceAggregator.GetSlowestCommandsAsync(10, Hours);
            _logger.LogDebug("LoadViewModelAsync: Retrieved {Count} slowest commands", slowest.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch slowest commands for {Hours} hours, continuing with aggregate data only", Hours);
        }

        // Calculate summary metrics from aggregates
        var totalCommands = aggregates.Sum(a => a.ExecutionCount);
        var avgResponseTime = aggregates.Any()
            ? aggregates.Average(a => a.AvgMs)
            : 0;
        var errorRate = totalCommands > 0
            ? aggregates.Sum(a => a.ExecutionCount * a.ErrorRate / 100.0) / totalCommands * 100
            : 0;
        var p99 = aggregates.Any() ? aggregates.Max(a => a.P99Ms) : 0;
        var p95 = aggregates.Any() ? aggregates.Max(a => a.P95Ms) : 0;
        var p50 = aggregates.Any() ? aggregates.Average(a => a.P50Ms) : 0;

        // Identify timeouts (commands > 3000ms - Discord's interaction timeout)
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

        ViewModel = new CommandPerformanceViewModel
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
            // Note: Trends would require comparing to previous period
            // For now, we set them to 0 (no change)
            AvgResponseTimeTrend = 0,
            ErrorRateTrend = 0,
            P99Trend = 0
        };

        _logger.LogDebug(
            "Command Performance ViewModel loaded: TotalCommands={TotalCommands}, AvgResponseTime={AvgMs}ms, ErrorRate={ErrorRate}%, Timeouts={TimeoutCount}",
            totalCommands, avgResponseTime, errorRate, ViewModel.TimeoutCount);
    }

    /// <summary>
    /// Creates an empty view model for error scenarios.
    /// </summary>
    private static CommandPerformanceViewModel CreateEmptyViewModel() => new()
    {
        TotalCommands = 0,
        AvgResponseTimeMs = 0,
        ErrorRate = 0,
        P99ResponseTimeMs = 0,
        P50Ms = 0,
        P95Ms = 0,
        SlowestCommands = Array.Empty<Core.DTOs.SlowestCommandDto>(),
        RecentTimeouts = Array.Empty<CommandTimeoutDto>(),
        TimeoutCount = 0,
        AvgResponseTimeTrend = 0,
        ErrorRateTrend = 0,
        P99Trend = 0
    };
}
