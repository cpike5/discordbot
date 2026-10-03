using DiscordBot.Bot.Services.Performance;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// The standalone Alerts &amp; Incidents page was retired in favour of the dashboard shell. This
/// route stays so old links and stored notification links keep working: it redirects to
/// <c>/Admin/Performance?tab=alerts</c>.
/// </summary>
public class AlertsModel : PerformanceTabRedirectModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlertsModel"/> class.
    /// </summary>
    public AlertsModel() : base(PerformanceDashboardTabs.Alerts)
    {
    }
}
