namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user flagged for ongoing monitoring in a guild.
/// </summary>
public class Watchlist
{
    /// <summary>
    /// Unique identifier for this watchlist entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this user is being watched.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user being watched.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the moderator who added this user to the watchlist.
    /// </summary>
    public ulong AddedByUserId { get; set; }

    /// <summary>
    /// Reason for adding this user to the watchlist.
    /// Null if no reason provided.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Timestamp when this user was added to the watchlist (UTC).
    /// </summary>
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this watchlist entry belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
