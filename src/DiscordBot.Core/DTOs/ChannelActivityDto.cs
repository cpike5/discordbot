namespace DiscordBot.Core.DTOs;

/// <summary>
/// Channel activity data for rankings and time series analytics.
/// Represents activity metrics for a single channel during a time period.
/// </summary>
public record ChannelActivityDto
{
    /// <summary>
    /// Gets the Discord channel ID.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    /// Gets the channel name.
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total number of messages sent in this channel.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Gets the number of unique users who sent messages in this channel.
    /// </summary>
    public int UniqueUsers { get; init; }

    /// <summary>
    /// Gets the hour of day (0-23) when activity was highest.
    /// Null if not available or not applicable.
    /// </summary>
    public int? PeakHour { get; init; }

    /// <summary>
    /// Gets the average message length in characters.
    /// </summary>
    public double AverageMessageLength { get; init; }
}
