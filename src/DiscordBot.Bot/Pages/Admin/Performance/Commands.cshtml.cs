using DiscordBot.Bot.Services.Performance;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// The standalone Command Performance page was retired in favour of the dashboard shell. This
/// route stays so old links keep working: it redirects to <c>/Admin/Performance?tab=commands</c>.
/// </summary>
public class CommandsModel : PerformanceTabRedirectModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandsModel"/> class.
    /// </summary>
    public CommandsModel() : base(PerformanceDashboardTabs.Commands)
    {
    }
}
