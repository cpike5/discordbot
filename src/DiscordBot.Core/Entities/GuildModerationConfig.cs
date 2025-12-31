using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents per-guild auto-moderation configuration settings.
/// </summary>
public class GuildModerationConfig
{
    /// <summary>
    /// Discord guild snowflake ID (primary key).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Configuration mode for this guild (Simple or Advanced).
    /// </summary>
    public ConfigMode Mode { get; set; }

    /// <summary>
    /// Simple preset name (Relaxed, Moderate, Strict) if using Simple mode.
    /// Null if using Advanced mode.
    /// </summary>
    public string? SimplePreset { get; set; }

    /// <summary>
    /// JSON-serialized spam detection configuration.
    /// </summary>
    public string SpamConfig { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized content filter configuration.
    /// </summary>
    public string ContentFilterConfig { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized raid protection configuration.
    /// </summary>
    public string RaidProtectionConfig { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this configuration was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this configuration belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
