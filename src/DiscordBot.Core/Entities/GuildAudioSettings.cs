namespace DiscordBot.Core.Entities;

/// <summary>
/// Per-guild configuration settings for the soundboard and audio features.
/// </summary>
public class GuildAudioSettings
{
    /// <summary>
    /// Discord guild snowflake ID (serves as primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether audio features are enabled for this guild.
    /// </summary>
    public bool AudioEnabled { get; set; } = true;

    /// <summary>
    /// Minutes of inactivity before the bot automatically leaves the voice channel.
    /// Set to 0 to stay indefinitely until manually disconnected.
    /// </summary>
    public int AutoLeaveTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Whether sounds should be queued (true) or replace the current playing sound (false).
    /// </summary>
    public bool QueueEnabled { get; set; } = true;

    /// <summary>
    /// Maximum allowed duration for sound files in seconds.
    /// </summary>
    public int MaxDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum allowed file size for uploads in bytes (default: 5 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    /// <summary>
    /// Maximum number of sounds allowed per guild.
    /// </summary>
    public int MaxSoundsPerGuild { get; set; } = 50;

    /// <summary>
    /// Total storage limit for all sounds in this guild in bytes (default: 100 MB).
    /// </summary>
    public long MaxStorageBytes { get; set; } = 104_857_600;

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

    /// <summary>
    /// Whether the member portal is enabled for this guild.
    /// When enabled, guild members can access the soundboard portal page.
    /// </summary>
    public bool EnableMemberPortal { get; set; } = false;

    /// <summary>
    /// Whether to suppress the "Now Playing" and "Sound Queued" confirmation messages for /play commands.
    /// When enabled, sounds play silently without a visible response. Error messages are always shown.
    /// </summary>
    public bool SilentPlayback { get; set; } = false;

    /// <summary>
    /// Role-based access restrictions for soundboard commands.
    /// </summary>
    public List<CommandRoleRestriction> CommandRoleRestrictions { get; set; } = new();
}
