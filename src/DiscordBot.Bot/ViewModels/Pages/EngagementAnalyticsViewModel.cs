using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Guild Engagement Analytics page.
/// </summary>
public record EngagementAnalyticsViewModel
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
    /// Analytics summary with message volume, active members, and retention metrics.
    /// </summary>
    public EngagementAnalyticsSummaryDto Summary { get; init; } = new();

    /// <summary>
    /// Time series data for message trends chart (messages, unique authors, avg length).
    /// </summary>
    public IReadOnlyList<MessageTrendDto> MessageTrends { get; init; } = Array.Empty<MessageTrendDto>();

    /// <summary>
    /// Channel engagement metrics with message counts and engagement rates.
    /// </summary>
    public IReadOnlyList<ChannelEngagementDto> ChannelEngagement { get; init; } = Array.Empty<ChannelEngagementDto>();

    /// <summary>
    /// New member retention funnel data.
    /// </summary>
    public IReadOnlyList<NewMemberRetentionDto> NewMemberRetention { get; init; } = Array.Empty<NewMemberRetentionDto>();

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
/// Channel engagement metrics for a single channel.
/// </summary>
public record ChannelEngagementDto
{
    /// <summary>
    /// Discord channel snowflake ID.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    /// Channel name (e.g., "general", "off-topic").
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// Total number of messages in this channel.
    /// </summary>
    public long MessageCount { get; init; }

    /// <summary>
    /// Number of unique members who posted in this channel.
    /// </summary>
    public int UniqueAuthors { get; init; }

    /// <summary>
    /// Engagement rate: percentage of guild members who posted in this channel.
    /// </summary>
    public decimal EngagementRate { get; init; }
}
