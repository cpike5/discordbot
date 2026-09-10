using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Command Performance Analytics page.
/// Displays response times, throughput, error tracking, and timeout analysis for Discord bot commands.
/// All data aggregation is delegated to <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class CommandsModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
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
    /// <param name="aggregator">The performance dashboard aggregator.</param>
    /// <param name="logger">The logger.</param>
    public CommandsModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<CommandsModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Command Performance page.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Command Performance page accessed by user {UserId}, hours={Hours}",
            User.Identity?.Name, Hours);

        ViewModel = await _aggregator.BuildCommandPerformanceAsync(Hours, cancellationToken);
    }
}
