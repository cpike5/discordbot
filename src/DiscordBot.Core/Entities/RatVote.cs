namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an individual vote cast on a Rat Watch.
/// </summary>
public class RatVote
{
    /// <summary>
    /// Unique identifier for this vote.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the Rat Watch this vote belongs to.
    /// </summary>
    public Guid RatWatchId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user who cast this vote.
    /// </summary>
    public ulong VoterUserId { get; set; }

    /// <summary>
    /// True if the voter voted "Rat" (guilty), false if voted "Not Rat" (not guilty).
    /// </summary>
    public bool IsGuiltyVote { get; set; }

    /// <summary>
    /// Timestamp when this vote was cast (UTC).
    /// </summary>
    public DateTime VotedAt { get; set; }

    /// <summary>
    /// Navigation property for the Rat Watch this vote belongs to.
    /// </summary>
    public RatWatch? RatWatch { get; set; }
}
