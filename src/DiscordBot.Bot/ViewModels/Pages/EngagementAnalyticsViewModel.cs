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
