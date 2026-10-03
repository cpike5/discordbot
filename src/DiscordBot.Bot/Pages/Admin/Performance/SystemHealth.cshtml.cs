using DiscordBot.Bot.Services.Performance;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// The standalone System Health page was retired in favour of the dashboard shell. This route
/// stays so old links keep working: it redirects to <c>/Admin/Performance?tab=system</c>.
/// </summary>
public class SystemHealthModel : PerformanceTabRedirectModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemHealthModel"/> class.
    /// </summary>
    public SystemHealthModel() : base(PerformanceDashboardTabs.System)
    {
    }
}
