using DiscordBot.Bot.Services.Performance;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// The standalone Health Metrics page was retired in favour of the dashboard shell. This route
/// stays so old links keep working: it redirects to <c>/Admin/Performance?tab=health</c>.
/// </summary>
public class HealthMetricsModel : PerformanceTabRedirectModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthMetricsModel"/> class.
    /// </summary>
    public HealthMetricsModel() : base(PerformanceDashboardTabs.Health)
    {
    }
}
