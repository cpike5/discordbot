namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents the assignment of a moderator tag to a user in a guild.
/// </summary>
public class UserModTag
{
    /// <summary>
    /// Unique identifier for this tag assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this tag was applied.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user who received this tag.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Tag definition ID that was applied to the user.
    /// </summary>
    public Guid TagId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the moderator who applied this tag.
    /// </summary>
    public ulong AppliedByUserId { get; set; }

    /// <summary>
    /// Timestamp when this tag was applied to the user (UTC).
    /// </summary>
    public DateTime AppliedAt { get; set; }

    /// <summary>
    /// Navigation property for the tag definition.
    /// </summary>
    public ModTag? Tag { get; set; }
}
