using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a Rat Watch accountability tracker for monitoring user commitments.
/// </summary>
public class RatWatch
{
    /// <summary>
    /// Unique identifier for this Rat Watch.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this watch was created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where this watch was created.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user being watched (the accused).
    /// </summary>
    public ulong AccusedUserId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user who initiated the watch.
    /// </summary>
    public ulong InitiatorUserId { get; set; }

    /// <summary>
    /// Discord message snowflake ID of the original message that triggered the watch.
    /// </summary>
    public ulong OriginalMessageId { get; set; }

    /// <summary>
    /// Optional custom message describing the commitment or reason for the watch.
    /// </summary>
    public string? CustomMessage { get; set; }

    /// <summary>
    /// Timestamp when the rat check should occur (UTC).
    /// </summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// Timestamp when this watch was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Current status of the Rat Watch.
    /// </summary>
    public RatWatchStatus Status { get; set; }

    /// <summary>
    /// Discord message snowflake ID of the notification message with "I'm Here!" button.
    /// Null if notification has not been posted yet.
    /// </summary>
    public ulong? NotificationMessageId { get; set; }

    /// <summary>
    /// Discord message snowflake ID of the voting message with voting buttons.
    /// Null if voting has not started yet.
    /// </summary>
    public ulong? VotingMessageId { get; set; }

    /// <summary>
    /// Timestamp when the accused cleared themselves early (UTC).
    /// Null if not cleared early.
    /// </summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>
    /// Timestamp when voting started (UTC).
    /// Null if voting has not started.
    /// </summary>
    public DateTime? VotingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when voting ended (UTC).
    /// Null if voting has not ended.
    /// </summary>
    public DateTime? VotingEndedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this watch belongs to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the votes cast on this watch.
    /// </summary>
    public ICollection<RatVote> Votes { get; set; } = new List<RatVote>();

    /// <summary>
    /// Navigation property for the record created if guilty verdict reached.
    /// Null if not guilty or voting not complete.
    /// </summary>
    public RatRecord? Record { get; set; }
}
