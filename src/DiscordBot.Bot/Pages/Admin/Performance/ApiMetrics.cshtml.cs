using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the API Rate Limits and Metrics page.
/// Displays Discord API usage, rate limit status, and latency tracking.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class ApiMetricsModel : PageModel
{
    private readonly IApiRequestTracker _apiRequestTracker;
    private readonly ILogger<ApiMetricsModel> _logger;

    /// <summary>
    /// Gets the view model for the API metrics page.
    /// </summary>
    public ApiRateLimitsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets or sets the number of hours of history to display (24, 168, or 720).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; } = 24;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiMetricsModel"/> class.
    /// </summary>
    /// <param name="apiRequestTracker">The API request tracker service.</param>
    /// <param name="logger">The logger.</param>
    public ApiMetricsModel(
        IApiRequestTracker apiRequestTracker,
        ILogger<ApiMetricsModel> logger)
    {
        _apiRequestTracker = apiRequestTracker;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the API Metrics page.
    /// </summary>
    public async Task OnGetAsync()
    {
        _logger.LogDebug("API Metrics page accessed by user {UserId}, hours={Hours}",
            User.Identity?.Name, Hours);

        try
        {
            await Task.Run(() => LoadViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load API metrics data for {Hours} hours", Hours);
            ViewModel = CreateEmptyViewModel();
        }
    }

    /// <summary>
    /// Loads the view model with API metrics data from the tracker service.
    /// </summary>
    private void LoadViewModel()
    {
        // Fetch API usage data
        var usageByCategory = _apiRequestTracker.GetUsageStatistics(Hours);
        var totalRequests = _apiRequestTracker.GetTotalRequests(Hours);
        var rateLimitEvents = _apiRequestTracker.GetRateLimitEvents(Hours);
        var latencyStats = _apiRequestTracker.GetLatencyStatistics(Hours);

        ViewModel = new ApiRateLimitsViewModel
        {
            TotalRequests = totalRequests,
            RateLimitHits = rateLimitEvents.Count,
            AvgLatencyMs = latencyStats.AvgLatencyMs,
            P95LatencyMs = latencyStats.P95LatencyMs,
            UsageByCategory = usageByCategory,
            RecentRateLimitEvents = rateLimitEvents.OrderByDescending(e => e.Timestamp).Take(20).ToList(),
            LatencyStats = latencyStats,
            Hours = Hours
        };

        _logger.LogDebug(
            "API Metrics ViewModel loaded: TotalRequests={TotalRequests}, AvgLatency={AvgLatencyMs}ms, RateLimitHits={RateLimitHits}",
            totalRequests, latencyStats.AvgLatencyMs, rateLimitEvents.Count);
    }

    /// <summary>
    /// Creates an empty view model for error scenarios.
    /// </summary>
    private static ApiRateLimitsViewModel CreateEmptyViewModel() => new()
    {
        TotalRequests = 0,
        RateLimitHits = 0,
        AvgLatencyMs = 0,
        P95LatencyMs = 0,
        UsageByCategory = Array.Empty<Core.DTOs.ApiUsageDto>(),
        RecentRateLimitEvents = Array.Empty<Core.DTOs.RateLimitEventDto>(),
        LatencyStats = null
    };
}
