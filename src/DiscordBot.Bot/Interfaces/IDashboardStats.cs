using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Computes the dashboard's hero numbers. One definition serves the page render, the page's
/// <c>?handler=Stats</c> and the <c>StatsUpdated</c> push, so the three can never disagree.
/// </summary>
public interface IDashboardStatsProvider
{
    /// <summary>
    /// Reads the current numbers from the database and the connection tracker.
    /// </summary>
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tells open dashboards that their hero numbers may have changed. Calls are coalesced: a burst
/// of commands produces one broadcast shortly after the last one.
/// </summary>
public interface IDashboardStatsBroadcaster
{
    /// <summary>
    /// Schedules a <c>StatsUpdated</c> broadcast. Returns at once and never throws.
    /// </summary>
    void NotifyChanged();
}
