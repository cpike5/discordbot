namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a permanent record of a guilty verdict from a Rat Watch.
/// Only created when voting results in a guilty verdict.
/// </summary>
public class RatRecord
{
    /// <summary>
    /// Unique identifier for this record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the Rat Watch that resulted in this record.
    /// </summary>
    public Guid RatWatchId { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this record was created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user who received the guilty verdict.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Number of "Rat" (guilty) votes received.
    /// </summary>
    public int GuiltyVotes { get; set; }

    /// <summary>
    /// Number of "Not Rat" (not guilty) votes received.
    /// </summary>
    public int NotGuiltyVotes { get; set; }

    /// <summary>
    /// Timestamp when this record was created (UTC).
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Optional link to the original Discord message that triggered the watch.
    /// </summary>
    public string? OriginalMessageLink { get; set; }

    /// <summary>
    /// Navigation property for the Rat Watch that created this record.
    /// </summary>
    public RatWatch? RatWatch { get; set; }

    /// <summary>
    /// Navigation property for the guild this record belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
