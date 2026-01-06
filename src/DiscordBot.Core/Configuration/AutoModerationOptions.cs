namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the auto-moderation system.
/// </summary>
public class AutoModerationOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AutoModeration";

    /// <summary>
    /// Duration in minutes before cached detection results expire.
    /// Default: 5 minutes
    /// </summary>
    public int DetectionCacheExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of guilds to cache auto-moderation configurations for.
    /// Default: 1000
    /// </summary>
    public int MaxCachedGuilds { get; set; } = 1000;

    /// <summary>
    /// Number of days to retain flagged event records before cleanup.
    /// Default: 90 days
    /// </summary>
    public int FlaggedEventRetentionDays { get; set; } = 90;

    /// <summary>
    /// Whether to enable debug logging for auto-moderation detection.
    /// Default: false
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Maximum messages to track per user for spam detection.
    /// Higher values use more memory but can catch more sophisticated spam patterns.
    /// Default: 200 (covers ~5 minutes at 40 messages/minute rate)
    /// </summary>
    public int MaxMessagesPerUser { get; set; } = 200;

    /// <summary>
    /// Maximum joins to track per guild for raid detection.
    /// Higher values use more memory but can detect longer-duration raids.
    /// Default: 500 (covers 15 minutes at ~33 joins/minute)
    /// </summary>
    public int MaxJoinsPerGuild { get; set; } = 500;
}
