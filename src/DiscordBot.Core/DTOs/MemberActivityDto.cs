namespace DiscordBot.Core.DTOs;

/// <summary>
/// Member activity data point for time series display and analytics.
/// Represents a single member's activity during a specific time period.
/// </summary>
public record MemberActivityDto
{
    /// <summary>
    /// Gets the start of the activity period (UTC).
    /// </summary>
    public DateTime Period { get; init; }

    /// <summary>
    /// Gets the Discord user ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the Discord username.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the member's display name (nickname if set, otherwise username).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the number of messages sent during this period.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Gets the number of reactions added during this period.
    /// </summary>
    public int ReactionCount { get; init; }

    /// <summary>
    /// Gets the total voice minutes during this period.
    /// </summary>
    public int VoiceMinutes { get; init; }

    /// <summary>
    /// Gets the number of unique channels the member was active in.
    /// </summary>
    public int UniqueChannels { get; init; }
}
