namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for channel engagement metrics.
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
