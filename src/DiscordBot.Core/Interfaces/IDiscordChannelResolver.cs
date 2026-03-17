using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Resolves Discord channel information from the Discord client cache.
/// Consolidates channel name resolution and text channel listing logic
/// that was previously duplicated across multiple pages and services.
/// </summary>
public interface IDiscordChannelResolver
{
    /// <summary>
    /// Resolves a single channel ID to its display name.
    /// Returns "Unknown Channel" if the channel cannot be resolved.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="channelId">The channel's Discord snowflake ID.</param>
    /// <returns>The channel name or "Unknown Channel" if not found.</returns>
    string ResolveChannelName(ulong guildId, ulong channelId);

    /// <summary>
    /// Resolves multiple channel IDs to their display names in a single batch.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="channelIds">The channel IDs to resolve.</param>
    /// <returns>A dictionary mapping channel IDs to their names. Missing channels map to "Unknown Channel".</returns>
    Dictionary<ulong, string> ResolveChannelNames(ulong guildId, IEnumerable<ulong> channelIds);

    /// <summary>
    /// Gets all text-capable channels for a guild from the Discord client cache.
    /// Includes text channels, voice channels (with text chat), announcement channels, and stage channels.
    /// Results are sorted by channel position.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>A list of channel information sorted by position.</returns>
    List<ChannelInfo> GetTextChannels(ulong guildId);
}

/// <summary>
/// Represents basic channel information resolved from Discord.
/// </summary>
/// <param name="Id">The channel's Discord snowflake ID.</param>
/// <param name="Name">The channel name.</param>
/// <param name="Position">The channel position for sorting.</param>
/// <param name="Type">The channel display type.</param>
public record ChannelInfo(ulong Id, string Name, int Position, ChannelDisplayType Type);
