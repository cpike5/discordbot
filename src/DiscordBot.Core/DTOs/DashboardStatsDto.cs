namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for real-time dashboard statistics updates.
/// Provides aggregated statistics for display on the dashboard.
/// </summary>
public class DashboardStatsDto
{
    /// <summary>
    /// Gets or sets the total number of commands executed today.
    /// </summary>
    public int CommandsToday { get; set; }

    /// <summary>
    /// Gets or sets the total number of members across all guilds.
    /// </summary>
    public int TotalMembers { get; set; }

    /// <summary>
    /// Gets or sets the number of active users in the last hour.
    /// </summary>
    public int ActiveUsersLastHour { get; set; }

    /// <summary>
    /// Gets or sets the number of messages processed today.
    /// </summary>
    public int MessagesToday { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when these stats were captured.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
