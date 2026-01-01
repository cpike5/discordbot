namespace DiscordBot.Core.DTOs;

/// <summary>
/// Guild growth data point for time series analytics.
/// Represents member growth metrics for a single day.
/// </summary>
public record GuildGrowthDto
{
    /// <summary>
    /// Gets the date of this data point.
    /// </summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// Gets the total member count at the end of this day.
    /// </summary>
    public int TotalMembers { get; init; }

    /// <summary>
    /// Gets the number of members who joined during this day.
    /// </summary>
    public int Joined { get; init; }

    /// <summary>
    /// Gets the number of members who left during this day.
    /// </summary>
    public int Left { get; init; }

    /// <summary>
    /// Gets the net member growth (joined - left) for this day.
    /// </summary>
    public int NetGrowth { get; init; }
}
