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
}
