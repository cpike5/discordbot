using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Pages.CommandLogs;

/// <summary>
/// Page model for the Command Analytics Dashboard.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class AnalyticsModel : PageModel
{
    private readonly ICommandAnalyticsService _analyticsService;
    private readonly IGuildService _guildService;
    private readonly ILogger<AnalyticsModel> _logger;

    public AnalyticsModel(
        ICommandAnalyticsService analyticsService,
        IGuildService guildService,
        ILogger<AnalyticsModel> logger)
    {
        _analyticsService = analyticsService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// The analytics view model with all chart data.
    /// </summary>
    public CommandAnalyticsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Start date filter (bound from query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date filter (bound from query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Guild ID filter (bound from query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        // Default to last 30 days if no dates specified
        var start = StartDate ?? DateTime.UtcNow.AddDays(-30).Date;
        var end = EndDate ?? DateTime.UtcNow.Date.AddDays(1); // End of today

        _logger.LogDebug("Loading analytics for {Start} to {End}, GuildId: {GuildId}", start, end, GuildId);

        try
        {
            // Load analytics data
            var analytics = await _analyticsService.GetAnalyticsAsync(start, end, GuildId, cancellationToken);

            // Load available guilds for filter dropdown
            var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);
            var guildOptions = guilds.Select(g => new GuildSelectOption(g.Id, g.Name)).ToList();

            ViewModel = new CommandAnalyticsViewModel
            {
                TotalCommands = analytics.TotalCommands,
                SuccessRate = analytics.SuccessRate,
                AvgResponseTimeMs = analytics.AvgResponseTimeMs,
                UniqueCommands = analytics.UniqueCommands,
                UsageOverTime = analytics.UsageOverTime,
                TopCommands = analytics.TopCommands,
                SuccessRateData = analytics.SuccessRateData,
                PerformanceData = analytics.PerformanceData,
                StartDate = start,
                EndDate = end.AddDays(-1), // Show the actual end date (not +1)
                GuildId = GuildId,
                AvailableGuilds = guildOptions
            };

            _logger.LogInformation("Analytics loaded successfully. Total commands: {TotalCommands}, Success rate: {SuccessRate:F2}%",
                analytics.TotalCommands, analytics.SuccessRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load analytics data");
            TempData["ErrorMessage"] = "Failed to load analytics data. Please try again.";
            // Return page with empty data
        }

        return Page();
    }
}
