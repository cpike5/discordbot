using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing guild moderation configuration.
/// </summary>
public interface IGuildModerationConfigService
{
    /// <summary>
    /// Gets the moderation configuration for a guild.
    /// Returns default configuration if not configured.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The guild moderation configuration DTO.</returns>
    Task<GuildModerationConfigDto> GetConfigAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Updates the moderation configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="config">The updated configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated guild moderation configuration DTO.</returns>
    Task<GuildModerationConfigDto> UpdateConfigAsync(ulong guildId, GuildModerationConfigDto config, CancellationToken ct = default);

    /// <summary>
    /// Applies a preset configuration to a guild (Relaxed, Moderate, or Strict).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="presetName">The preset name (Relaxed, Moderate, or Strict).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated guild moderation configuration DTO.</returns>
    Task<GuildModerationConfigDto> ApplyPresetAsync(ulong guildId, string presetName, CancellationToken ct = default);

    /// <summary>
    /// Gets the default moderation configuration.
    /// </summary>
    /// <returns>The default guild moderation configuration DTO.</returns>
    GuildModerationConfigDto GetDefaultConfig();

    /// <summary>
    /// Gets the spam detection configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The spam detection configuration DTO.</returns>
    Task<SpamDetectionConfigDto> GetSpamConfigAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the content filter configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The content filter configuration DTO.</returns>
    Task<ContentFilterConfigDto> GetContentFilterConfigAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the raid protection configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raid protection configuration DTO.</returns>
    Task<RaidProtectionConfigDto> GetRaidProtectionConfigAsync(ulong guildId, CancellationToken ct = default);
}
