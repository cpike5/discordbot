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
    /// <param name="hours">Time range in hours (24, 168, or 720) used for the command aggregates and the shell's reported time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PerformanceDashboardOverview> BuildOverviewAsync(int hours = 24, CancellationToken cancellationToken = default);

    /// <summary>Builds the Health tab view model.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HealthMetricsViewModel> BuildHealthMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds the Commands tab view model for the given time range.</summary>
    /// <param name="hours">Time range in hours.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CommandPerformanceViewModel> BuildCommandPerformanceAsync(int hours, CancellationToken cancellationToken = default);

    /// <summary>Builds the API tab view model for the given time range.</summary>
    ApiRateLimitsViewModel BuildApiRateLimits(int hours);

    /// <summary>Builds the System tab view model.</summary>
    SystemHealthViewModel BuildSystemHealth();

    /// <summary>Builds the Alerts tab view model, including whether the given user can edit alert configuration.</summary>
    /// <param name="user">The current user, used to determine alert-editing permission.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AlertsPageViewModel> BuildAlertsPageAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>The overview tab's content view model plus the shell view model that wraps every tab.</summary>
public sealed record PerformanceDashboardOverview
{
    public required PerformanceOverviewViewModel Overview { get; init; }
    public required PerformanceShellViewModel Shell { get; init; }
}
