namespace DiscordBot.Core.Entities;

/// <summary>
/// Daily guild-level metrics snapshot.
/// Provides high-level summary of guild health and activity.
/// </summary>
public class GuildMetricsSnapshot
{
    /// <summary>
    /// Gets or sets the unique identifier for this snapshot.
    /// Uses long (Int64) to support high-volume logging scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the date of this snapshot (date only, no time component).
    /// Represents a full day of activity in UTC.
    /// </summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>
    /// Gets or sets the total member count at the end of the day.
    /// </summary>
    public int TotalMembers { get; set; }

    /// <summary>
    /// Gets or sets the number of members who sent at least one message.
    /// </summary>
    public int ActiveMembers { get; set; }

    /// <summary>
    /// Gets or sets the number of new members who joined during this day.
    /// </summary>
    public int MembersJoined { get; set; }

    /// <summary>
    /// Gets or sets the number of members who left during this day.
    /// </summary>
    public int MembersLeft { get; set; }

    /// <summary>
    /// Gets or sets the total messages sent in the guild during this day.
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Gets or sets the total commands executed during this day.
    /// </summary>
    public int CommandsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the number of moderation actions taken during this day.
    /// </summary>
    public int ModerationActions { get; set; }

    /// <summary>
    /// Gets or sets the number of unique channels with at least one message.
    /// </summary>
    public int ActiveChannels { get; set; }

    /// <summary>
    /// Gets or sets the total voice minutes across all members during this day.
    /// Currently set to 0 as voice activity tracking is not implemented yet.
    /// </summary>
    public int TotalVoiceMinutes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this snapshot was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the navigation property to the guild entity.
    /// </summary>
    public Guild? Guild { get; set; }
}
