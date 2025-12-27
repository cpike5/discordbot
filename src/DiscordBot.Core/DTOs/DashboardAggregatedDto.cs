namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for aggregated dashboard statistics.
/// Returned by the dashboard-stats API endpoint as a fallback when SignalR is unavailable.
/// </summary>
public class DashboardAggregatedDto
{
    /// <summary>
    /// Gets or sets the bot status information.
    /// </summary>
    public BotStatusUpdateDto BotStatus { get; set; } = new();

    /// <summary>
    /// Gets or sets the guild statistics.
    /// </summary>
    public GuildStatsDto GuildStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the command statistics for the last 24 hours.
    /// </summary>
    public CommandStatsDto CommandStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the recent activity items.
    /// </summary>
    public IReadOnlyList<RecentActivityItemDto> RecentActivity { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when this data was retrieved.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Data transfer object for guild statistics.
/// </summary>
public class GuildStatsDto
{
    /// <summary>
    /// Gets or sets the total number of guilds.
    /// </summary>
    public int TotalGuilds { get; set; }

    /// <summary>
    /// Gets or sets the total number of members across all guilds.
    /// </summary>
    public int TotalMembers { get; set; }
}

/// <summary>
/// Data transfer object for command statistics.
/// </summary>
public class CommandStatsDto
{
    /// <summary>
    /// Gets or sets the total number of commands executed in the time period.
    /// </summary>
    public int TotalCommands { get; set; }

    /// <summary>
    /// Gets or sets the number of successful commands.
    /// </summary>
    public int SuccessfulCommands { get; set; }

    /// <summary>
    /// Gets or sets the number of failed commands.
    /// </summary>
    public int FailedCommands { get; set; }

    /// <summary>
    /// Gets or sets the command usage by command name.
    /// </summary>
    public IDictionary<string, int> CommandUsage { get; set; } = new Dictionary<string, int>();
}

/// <summary>
/// Data transfer object for a recent activity item.
/// </summary>
public class RecentActivityItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the activity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the type of activity (e.g., "CommandExecuted").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the activity.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the guild ID where the activity occurred.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name where the activity occurred.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the user ID who performed the activity.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username who performed the activity.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets whether the activity was successful.
    /// </summary>
    public bool Success { get; set; }
}
