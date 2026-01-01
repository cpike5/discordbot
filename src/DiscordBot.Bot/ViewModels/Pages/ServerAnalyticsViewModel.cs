using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Server Analytics dashboard page.
/// </summary>
public record ServerAnalyticsViewModel
{
    /// <summary>
    /// The guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// The guild's name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Optional guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Analytics summary with member counts, message volume, and growth metrics.
    /// </summary>
    public ServerAnalyticsSummaryDto Summary { get; init; } = new();

    /// <summary>
    /// Time series data for activity trends (messages, active members) over time.
    /// </summary>
    public IReadOnlyList<ActivityTimeSeriesDto> ActivityTimeSeries { get; init; } = Array.Empty<ActivityTimeSeriesDto>();

    /// <summary>
    /// Activity heatmap data (day of week + hour).
    /// </summary>
    public IReadOnlyList<ServerActivityHeatmapDto> ActivityHeatmap { get; init; } = Array.Empty<ServerActivityHeatmapDto>();

    /// <summary>
    /// Top contributors leaderboard (most active members).
    /// </summary>
    public IReadOnlyList<TopContributorDto> TopContributors { get; init; } = Array.Empty<TopContributorDto>();

    /// <summary>
    /// Top channels leaderboard (most active channels by message count).
    /// </summary>
    public IReadOnlyList<TopChannelDto> TopChannels { get; init; } = Array.Empty<TopChannelDto>();

    /// <summary>
    /// Start date for filtering (UTC).
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date for filtering (UTC).
    /// </summary>
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Top channel metrics for leaderboard display.
/// </summary>
public record TopChannelDto
{
    /// <summary>
    /// Discord channel snowflake ID.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    /// Channel name.
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// Total number of messages sent in this channel during the time period.
    /// </summary>
    public long MessageCount { get; init; }

    /// <summary>
    /// Number of unique users who posted in this channel.
    /// </summary>
    public int UniqueContributors { get; init; }

    /// <summary>
    /// Timestamp of the most recent message in this channel.
    /// </summary>
    public DateTime LastActivity { get; init; }
}
