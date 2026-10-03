using DiscordBot.Bot.Services.Performance;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// The standalone API &amp; Rate Limits page was retired in favour of the dashboard shell. This
/// route stays so old links keep working: it redirects to <c>/Admin/Performance?tab=api</c>.
/// </summary>
public class ApiMetricsModel : PerformanceTabRedirectModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiMetricsModel"/> class.
    /// </summary>
    public ApiMetricsModel() : base(PerformanceDashboardTabs.Api)
    {
    }
}
