using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a moderation action taken against a user (warn, kick, ban, mute, note).
/// </summary>
public class ModerationCase
{
    /// <summary>
    /// Unique identifier for this moderation case.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Auto-incremented case number scoped to the guild.
    /// </summary>
    public long CaseNumber { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this action occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user this action was taken against.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the moderator who took this action.
    /// </summary>
    public ulong ModeratorUserId { get; set; }

    /// <summary>
    /// Type of moderation action taken.
    /// </summary>
    public CaseType Type { get; set; }

    /// <summary>
    /// Reason for taking this moderation action.
    /// Null if no reason provided.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Duration for temporary bans or mutes.
    /// Null for permanent actions or warnings.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Timestamp when this case was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this action expires for temporary bans/mutes (UTC).
    /// Null for permanent actions or warnings.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Related flagged event ID if this action was taken in response to an auto-detected event.
    /// Null for manual moderation actions.
    /// </summary>
    public Guid? RelatedFlaggedEventId { get; set; }

    /// <summary>
    /// Discord message snowflake ID that triggered this case (e.g., via context menu action).
    /// Null if the case was not created from a message context.
    /// </summary>
    public ulong? ContextMessageId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where the context message was posted.
    /// Null if the case was not created from a message context.
    /// </summary>
    public ulong? ContextChannelId { get; set; }

    /// <summary>
    /// Cached content of the message that triggered this case.
    /// Truncated to 500 characters for storage. Null if the case was not created from a message context.
    /// </summary>
    public string? ContextMessageContent { get; set; }

    /// <summary>
    /// Navigation property for the guild this case belongs to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the related flagged event if applicable.
    /// </summary>
    public FlaggedEvent? RelatedFlaggedEvent { get; set; }
}
