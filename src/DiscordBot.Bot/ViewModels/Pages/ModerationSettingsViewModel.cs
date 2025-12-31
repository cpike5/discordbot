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
    /// Gets or sets the configuration mode.
    /// </summary>
    public ConfigMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the simple mode preset name (optional, only used when Mode is Simple).
    /// </summary>
    public string? SimplePreset { get; set; }
}
