using DiscordBot.Bot.ViewModels.Pages;
using System.Security.Claims;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Builds the view models for every tab of the Performance Overview dashboard shell
/// (<c>DiscordBot.Bot.Pages.Admin.Performance.IndexModel</c>), pulling together connection
/// state, latency, command performance, API tracking, background-service health, memory/GC,
/// database metrics, cache stats and alerts. Extracted so the page model stays a thin
/// request router around this aggregator.
/// </summary>
public interface IPerformanceDashboardAggregator
{
    /// <summary>Builds the overview tab content plus the shell (status banner / alert count) view models.</summary>
    Task<PerformanceDashboardOverview> BuildOverviewAsync();

    /// <summary>Builds the Health tab view model.</summary>
    Task<HealthMetricsViewModel> BuildHealthMetricsAsync();

    /// <summary>Builds the Commands tab view model for the given time range.</summary>
    Task<CommandPerformanceViewModel> BuildCommandPerformanceAsync(int hours);

    /// <summary>Builds the API tab view model for the given time range.</summary>
    ApiRateLimitsViewModel BuildApiRateLimits(int hours);

    /// <summary>Builds the System tab view model.</summary>
    SystemHealthViewModel BuildSystemHealth();

    /// <summary>Builds the Alerts tab view model, including whether the given user can edit alert configuration.</summary>
    Task<AlertsPageViewModel> BuildAlertsPageAsync(ClaimsPrincipal user);
}

/// <summary>The overview tab's content view model plus the shell view model that wraps every tab.</summary>
public sealed record PerformanceDashboardOverview
{
    public required PerformanceOverviewViewModel Overview { get; init; }
    public required PerformanceShellViewModel Shell { get; init; }
}
