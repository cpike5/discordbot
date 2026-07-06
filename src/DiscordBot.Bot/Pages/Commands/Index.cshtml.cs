using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Commands;

/// <summary>
/// Page model hosting the Commands Blazor island (Command List + Execution Logs tabs;
/// the Analytics tab is delegated to /api/commands/analytics via Chart.js interop).
/// The page binds initial filter state from the query string and passes it to the island.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the active tab identifier.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "command-list";

    /// <summary>
    /// Gets or sets the start date for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the guild ID for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the search term for filtering command logs.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the command name for filtering command logs.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? CommandName { get; set; }

    /// <summary>
    /// Gets or sets the status filter for command logs (true=success, false=failure, null=all).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    /// <summary>
    /// Handles the GET request for the Commands page. All tab data is loaded by the
    /// Blazor island (or, for Analytics, by /api/commands/analytics).
    /// </summary>
    public void OnGet()
    {
        _logger.LogInformation("User accessing commands page");
    }
}
