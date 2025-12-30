namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for the Rat Watch feature.
/// </summary>
public class GuildRatWatchSettings
{
    /// <summary>
    /// Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether the Rat Watch feature is enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// IANA timezone identifier for this guild (e.g., "America/New_York", "UTC").
    /// Used for parsing user time input like "10pm".
    /// </summary>
    public string Timezone { get; set; } = "Eastern Standard Time";

    /// <summary>
    /// Maximum number of hours in advance a Rat Watch can be scheduled.
    /// </summary>
    public int MaxAdvanceHours { get; set; } = 24;

    /// <summary>
    /// Duration in minutes for the voting window.
    /// </summary>
    public int VotingDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Timestamp when these settings were created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when these settings were last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild these settings belong to.
    /// </summary>
    public Guild? Guild { get; set; }
}
