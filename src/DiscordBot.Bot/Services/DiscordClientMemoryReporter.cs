using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Reports memory usage estimates for the Discord client's internal caches.
/// Provides visibility into guild, user, channel, and message cache sizes.
/// </summary>
public class DiscordClientMemoryReporter : IMemoryReportable
{
    private readonly DiscordSocketClient _client;

    // Rough memory estimates per cached item (based on Discord.NET object sizes)
    private const int BytesPerGuild = 2048;        // Guild object with basic properties
    private const int BytesPerChannel = 512;       // Channel object
    private const int BytesPerUser = 256;          // Cached user object
    private const int BytesPerMessage = 1024;      // Cached message (varies with content)
    private const int BytesPerRole = 128;          // Role object
    private const int BytesPerEmoji = 64;          // Custom emoji

    public DiscordClientMemoryReporter(DiscordSocketClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public string ServiceName => "Discord Client Cache";

    /// <inheritdoc/>
    public ServiceMemoryReportDto GetMemoryReport()
    {
        var guilds = _client.Guilds;
        var guildCount = guilds.Count;

        var totalChannels = 0;
        var totalUsers = 0;
        var totalRoles = 0;
        var totalEmojis = 0;

        foreach (var guild in guilds)
        {
            totalChannels += guild.Channels.Count;
            totalUsers += guild.Users.Count;
            totalRoles += guild.Roles.Count;
            totalEmojis += guild.Emotes.Count;
        }

        // Message cache size is configured in DiscordSocketConfig
        var messageCacheSize = 100; // From DiscordServiceExtensions.cs

        var estimatedBytes =
            (guildCount * BytesPerGuild) +
            (totalChannels * BytesPerChannel) +
            (totalUsers * BytesPerUser) +
            (messageCacheSize * guildCount * BytesPerMessage) + // Per-guild message cache
            (totalRoles * BytesPerRole) +
            (totalEmojis * BytesPerEmoji);

        return new ServiceMemoryReportDto
        {
            ServiceName = ServiceName,
            EstimatedBytes = estimatedBytes,
            ItemCount = guildCount,
            Details = $"{guildCount} guilds, {totalChannels} channels, {totalUsers} cached users, {totalRoles} roles"
        };
    }
}
