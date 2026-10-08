using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the guild moderation settings page.
/// Contains all configuration data for auto-moderation, including spam detection, content filtering, raid protection, and tags.
/// </summary>
public class ModerationSettingsViewModel
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the configuration mode (Simple or Advanced).
    /// </summary>
    public ConfigMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the simple mode preset (Relaxed, Moderate, or Strict).
    /// Null when in Advanced mode.
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
    /// The channel the mod-log feed posts to, or null when the feed is off.
    /// </summary>
    public ulong? ModLogChannelId { get; set; }

    /// <summary>
    /// Which kinds of event the mod-log feed posts.
    /// </summary>
    public ModLogEventKinds ModLogEvents { get; set; } = ModLogEventKinds.All;

    /// <summary>
    /// Gets or sets the list of mod tags for this guild.
    /// </summary>
    public IReadOnlyList<ModTagDto> Tags { get; set; } = Array.Empty<ModTagDto>();

    /// <summary>
    /// Gets or sets the timestamp when the configuration was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Creates a view model from the given DTOs.
    /// </summary>
    /// <param name="config">The guild moderation configuration DTO.</param>
    /// <param name="tags">The list of mod tags for the guild.</param>
    /// <returns>A populated view model instance.</returns>
    public static ModerationSettingsViewModel FromDto(GuildModerationConfigDto config, IEnumerable<ModTagDto> tags)
    {
        return new ModerationSettingsViewModel
        {
            GuildId = config.GuildId,
            Mode = config.Mode,
            SimplePreset = config.SimplePreset,
            SpamConfig = config.SpamConfig,
            ContentFilterConfig = config.ContentFilterConfig,
            RaidProtectionConfig = config.RaidProtectionConfig,
            ModLogChannelId = config.ModLogChannelId,
            ModLogEvents = config.ModLogEvents,
            Tags = tags.ToList(),
            UpdatedAt = config.UpdatedAt
        };
    }
}

/// <summary>
/// DTO for updating the overview configuration (mode and preset).
/// </summary>
public class OverviewUpdateDto
{
    /// <summary>
    /// Gets or sets the configuration mode. Null leaves the saved mode alone.
    /// </summary>
    public ConfigMode? Mode { get; set; }

    /// <summary>
    /// Gets or sets the simple mode preset name. Null leaves the saved preset alone.
    /// </summary>
    public string? SimplePreset { get; set; }

    /// <summary>
    /// The mod-log channel as a string snowflake. Null leaves the saved channel alone; an empty
    /// string turns the feed off.
    /// </summary>
    public string? ModLogChannelId { get; set; }

    /// <summary>
    /// The <see cref="ModLogEventKinds"/> flags to post. Null leaves the saved value alone.
    /// </summary>
    public int? ModLogEvents { get; set; }
}
