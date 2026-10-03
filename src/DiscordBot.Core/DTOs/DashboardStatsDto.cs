namespace DiscordBot.Core.DTOs;

/// <summary>
/// The numbers in the dashboard's hero cards, pushed to open dashboards as the <c>StatsUpdated</c>
/// hub event and returned by the page's <c>?handler=Stats</c>. The property names (camelCased on
/// the wire) are the ones <c>dashboard-realtime.js</c> reads, so a rename here is a rename there.
/// </summary>
public class DashboardStatsDto
{
    /// <summary>
    /// Gets or sets the number of servers the bot is currently in (active guilds).
    /// </summary>
    public int TotalServers { get; set; }

    /// <summary>
    /// Gets or sets the member count summed across those servers, as Discord reports it.
    /// People who share several servers are counted once per server.
    /// </summary>
    public int TotalMembers { get; set; }

    /// <summary>
    /// Gets or sets the number of commands run in the last 24 hours (a rolling window, not
    /// since midnight).
    /// </summary>
    public int CommandsLast24Hours { get; set; }

    /// <summary>
    /// Gets or sets the percentage of the last 24 hours the bot was connected (0 to 100).
    /// </summary>
    public double UptimePercent24Hours { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when these stats were captured.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
