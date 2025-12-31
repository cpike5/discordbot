using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an auto-detected moderation event flagged for review.
/// </summary>
public class FlaggedEvent
{
    /// <summary>
    /// Unique identifier for this flagged event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this event occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user who triggered this event.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where this event occurred.
    /// Null for events not tied to a specific channel (e.g., raid detection).
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Type of auto-moderation rule that triggered this event.
    /// </summary>
    public RuleType RuleType { get; set; }

    /// <summary>
    /// Severity level of this flagged event.
    /// </summary>
    public Severity Severity { get; set; }

    /// <summary>
    /// Human-readable description of why this event was flagged.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized evidence supporting this flag (message IDs, content, timestamps).
    /// </summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>
    /// Current review status of this flagged event.
    /// </summary>
    public FlaggedEventStatus Status { get; set; }

    /// <summary>
    /// Description of action taken in response to this event.
    /// Null if no action has been taken yet.
    /// </summary>
    public string? ActionTaken { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the moderator who reviewed this event.
    /// Null if not yet reviewed.
    /// </summary>
    public ulong? ReviewedByUserId { get; set; }

    /// <summary>
    /// Timestamp when this event was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this event was reviewed by a moderator (UTC).
    /// Null if not yet reviewed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this event belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
