using DiscordBot.Bot.Services.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Base for the retired standalone Performance pages (System Health, API Metrics, Health Metrics,
/// Alerts, Commands). Each route stays so old links, bookmarks and stored notification links keep
/// working, and answers with a permanent redirect to the matching tab of the dashboard shell,
/// <c>/Admin/Performance?tab=…</c>. A <c>hours</c> query value is forwarded after it is clamped to
/// a supported range.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public abstract class PerformanceTabRedirectModel : PageModel
{
    private readonly string _tab;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceTabRedirectModel"/> class.
    /// </summary>
    /// <param name="tab">The tab id (see <see cref="PerformanceDashboardTabs"/>) to redirect to.</param>
    protected PerformanceTabRedirectModel(string tab)
    {
        _tab = tab;
    }

    /// <summary>
    /// Gets or sets the time range the old link asked for, if any. The old Commands and API pages
    /// took it as <c>?hours=</c>; it is clamped when forwarded.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? Hours { get; set; }

    /// <summary>
    /// Redirects to the matching tab of the dashboard shell.
    /// </summary>
    public IActionResult OnGet() => RedirectPermanent(PerformanceDashboardTabs.TabUrl(_tab, Hours));
}
