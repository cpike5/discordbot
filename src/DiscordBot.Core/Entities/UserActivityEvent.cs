using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user activity event for consent-free analytics.
/// Stores only metadata (no content) for aggregate analytics and engagement metrics.
/// </summary>
public class UserActivityEvent
{
    /// <summary>
    /// Unique identifier for this event entry.
    /// Uses long for high volume scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// ID of the user who performed the activity.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// ID of the guild where the activity occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// ID of the channel where the activity occurred.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Timestamp when the activity event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Timestamp when the event was logged to the database.
    /// </summary>
    public DateTime LoggedAt { get; set; }

    /// <summary>
    /// Type of activity event (Message, Reaction, VoiceJoin, etc.).
    /// </summary>
    public ActivityEventType EventType { get; set; }
}
