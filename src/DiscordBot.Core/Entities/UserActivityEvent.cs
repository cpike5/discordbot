using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an anonymous user activity event for consent-free analytics.
/// Stores activity metadata without any message content to track user engagement
/// while respecting privacy (GDPR Article 6.1.f - legitimate interest).
/// </summary>
public class UserActivityEvent
{
    /// <summary>
    /// Unique identifier for this event.
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
    /// Timestamp when the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of activity event (Message, Reaction, VoiceJoin, etc.).
    /// </summary>
    public ActivityEventType EventType { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the user.
    /// </summary>
    public User? User { get; set; }
}
