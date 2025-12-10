namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying command usage statistics on the dashboard.
/// </summary>
public record CommandStatsViewModel
{
    /// <summary>
    /// Gets the total number of commands executed.
    /// </summary>
    public int TotalCommands { get; init; }

    /// <summary>
    /// Gets the top commands with their usage counts and rankings.
    /// </summary>
    public IReadOnlyList<CommandUsageStat> TopCommands { get; init; } = Array.Empty<CommandUsageStat>();

    /// <summary>
    /// Gets the time range filter in hours (24, 168, 720, or null for all time).
    /// </summary>
    public int? TimeRangeHours { get; init; }

    /// <summary>
    /// Gets the human-readable label for the current time range.
    /// </summary>
    public string TimeRangeLabel { get; init; } = "All Time";

    /// <summary>
    /// Creates a <see cref="CommandStatsViewModel"/> from command statistics data.
    /// </summary>
    /// <param name="stats">Dictionary mapping command names to their usage counts.</param>
    /// <param name="timeRangeHours">Optional time range filter in hours (24, 168, 720, or null for all time).</param>
    /// <returns>A new <see cref="CommandStatsViewModel"/> instance with computed statistics.</returns>
    public static CommandStatsViewModel FromStats(IDictionary<string, int> stats, int? timeRangeHours = null)
    {
        var totalCommands = stats.Values.Sum();

        // Get top 10 commands ordered by count descending
        var topCommands = stats
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select((kvp, index) => new CommandUsageStat(
                CommandName: kvp.Key,
                Count: kvp.Value,
                Rank: index + 1
            ))
            .ToList();

        return new CommandStatsViewModel
        {
            TotalCommands = totalCommands,
            TopCommands = topCommands,
            TimeRangeHours = timeRangeHours,
            TimeRangeLabel = GetTimeRangeLabel(timeRangeHours)
        };
    }

    /// <summary>
    /// Gets a human-readable label for the given time range.
    /// </summary>
    /// <param name="timeRangeHours">The time range in hours.</param>
    /// <returns>A formatted time range label.</returns>
    private static string GetTimeRangeLabel(int? timeRangeHours)
    {
        return timeRangeHours switch
        {
            24 => "Last 24 Hours",
            168 => "Last 7 Days",
            720 => "Last 30 Days",
            _ => "All Time"
        };
    }
}

/// <summary>
/// Represents a single command's usage statistics.
/// </summary>
/// <param name="CommandName">The name of the command (e.g., "/help").</param>
/// <param name="Count">The number of times the command was executed.</param>
/// <param name="Rank">The ranking position (1-based) among all commands.</param>
public record CommandUsageStat(
    string CommandName,
    int Count,
    int Rank
);
