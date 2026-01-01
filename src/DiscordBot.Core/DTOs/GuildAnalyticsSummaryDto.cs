namespace DiscordBot.Core.DTOs;

/// <summary>
/// Quick summary stats for a guild's analytics dashboard.
/// Provides high-level metrics for recent activity periods.
/// </summary>
public record GuildAnalyticsSummaryDto
{
    /// <summary>
    /// Gets the current total member count.
    /// </summary>
    public int TotalMembers { get; init; }

    /// <summary>
    /// Gets the number of members active in the last 24 hours.
    /// </summary>
    public int ActiveMembers24h { get; init; }

    /// <summary>
    /// Gets the number of members active in the last 7 days.
    /// </summary>
    public int ActiveMembers7d { get; init; }

    /// <summary>
    /// Gets the total messages sent in the last 24 hours.
    /// </summary>
    public int Messages24h { get; init; }

    /// <summary>
    /// Gets the total messages sent in the last 7 days.
    /// </summary>
    public int Messages7d { get; init; }

    /// <summary>
    /// Gets the net member growth (joins - leaves) in the last 7 days.
    /// </summary>
    public int MemberGrowth7d { get; init; }

    /// <summary>
    /// Gets the total commands executed in the last 24 hours.
    /// </summary>
    public int Commands24h { get; init; }

    /// <summary>
    /// Gets the total moderation actions taken in the last 7 days.
    /// </summary>
    public int ModerationActions7d { get; init; }

    /// <summary>
    /// Gets the most active channel ID in the last 7 days.
    /// </summary>
    public ulong? TopChannelId { get; init; }

    /// <summary>
    /// Gets the most active channel name in the last 7 days.
    /// </summary>
    public string? TopChannelName { get; init; }
}
