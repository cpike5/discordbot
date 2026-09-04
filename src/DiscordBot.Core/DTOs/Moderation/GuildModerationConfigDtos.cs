using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for guild moderation configuration.
/// </summary>
public class GuildModerationConfigDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets whether the moderation system is enabled for this guild.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration mode.
    /// </summary>
    public ConfigMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the simple mode preset (if using Simple mode).
    /// </summary>
    public string? SimplePreset { get; set; }

    /// <summary>
    /// Gets or sets the spam detection configuration.
    /// </summary>
    public SpamDetectionConfigDto SpamConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the content filter configuration.
    /// </summary>
    public ContentFilterConfigDto ContentFilterConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the raid protection configuration.
    /// </summary>
    public RaidProtectionConfigDto RaidProtectionConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the configuration was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Data transfer object for spam detection configuration settings.
/// </summary>
public class SpamDetectionConfigDto
{
    /// <summary>
    /// Gets or sets whether spam detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed in the time window.
    /// Default: 5 messages.
    /// </summary>
    public int MaxMessagesPerWindow { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window in seconds for message counting.
    /// Default: 5 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of mentions allowed per message.
    /// Default: 5 mentions.
    /// </summary>
    public int MaxMentionsPerMessage { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum allowed duplicate message similarity (0.0-1.0).
    /// Default: 0.8 (80% similar).
    /// </summary>
    public double DuplicateMessageThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the automatic action to take when spam is detected.
    /// </summary>
    public AutoAction AutoAction { get; set; } = AutoAction.Delete;
}

/// <summary>
/// Data transfer object for content filtering configuration settings.
/// </summary>
public class ContentFilterConfigDto
{
    /// <summary>
    /// Gets or sets whether content filtering is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of prohibited words or phrases.
    /// </summary>
    public List<string> ProhibitedWords { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of allowed link domains (whitelist).
    /// Empty list means all links are allowed.
    /// </summary>
    public List<string> AllowedLinkDomains { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to block all links not in the whitelist.
    /// </summary>
    public bool BlockUnlistedLinks { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to block invite links to other Discord servers.
    /// </summary>
    public bool BlockInviteLinks { get; set; } = false;

    /// <summary>
    /// Gets or sets the automatic action to take when prohibited content is detected.
    /// </summary>
    public AutoAction AutoAction { get; set; } = AutoAction.Delete;
}

/// <summary>
/// Data transfer object for raid protection configuration settings.
/// </summary>
public class RaidProtectionConfigDto
{
    /// <summary>
    /// Gets or sets whether raid protection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of joins allowed in the time window before triggering raid detection.
    /// Default: 10 joins.
    /// </summary>
    public int MaxJoinsPerWindow { get; set; } = 10;

    /// <summary>
    /// Gets or sets the time window in seconds for join counting.
    /// Default: 10 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the minimum account age in hours required to join (0 = no restriction).
    /// Default: 0 (no restriction).
    /// </summary>
    public int MinAccountAgeHours { get; set; } = 0;

    /// <summary>
    /// Gets or sets the automatic action to take when a raid is detected.
    /// </summary>
    public RaidAutoAction AutoAction { get; set; } = RaidAutoAction.AlertOnly;
}

/// <summary>
/// DTO for applying a moderation preset.
/// </summary>
public class ApplyPresetDto
{
    /// <summary>
    /// Gets or sets the preset name (Relaxed, Moderate, or Strict).
    /// </summary>
    public string PresetName { get; set; } = string.Empty;
}
