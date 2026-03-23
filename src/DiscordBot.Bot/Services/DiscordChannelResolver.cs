using Discord.WebSocket;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Resolves Discord channel information using the <see cref="DiscordSocketClient"/> cache.
/// Consolidates channel name resolution and text channel listing logic
/// that was previously duplicated across multiple pages and services.
/// </summary>
public class DiscordChannelResolver : IDiscordChannelResolver
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<DiscordChannelResolver> _logger;

    public DiscordChannelResolver(
        DiscordSocketClient discordClient,
        ILogger<DiscordChannelResolver> logger)
    {
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ResolveChannelName(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving channel {ChannelId}", guildId, channelId);
                return "Unknown Channel";
            }

            var channel = guild.GetChannel(channelId);
            if (channel != null)
            {
                return channel.Name;
            }

            _logger.LogWarning("Could not resolve channel name for channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving channel name for channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
    }

    /// <inheritdoc/>
    public Dictionary<ulong, string> ResolveChannelNames(ulong guildId, IEnumerable<ulong> channelIds)
    {
        var result = new Dictionary<ulong, string>();

        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving channel names", guildId);
                foreach (var channelId in channelIds)
                {
                    result[channelId] = "Unknown Channel";
                }
                return result;
            }

            foreach (var channelId in channelIds)
            {
                var channel = guild.GetChannel(channelId);
                result[channelId] = channel?.Name ?? "Unknown Channel";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving channel names for guild {GuildId}", guildId);
            foreach (var channelId in channelIds)
            {
                result.TryAdd(channelId, "Unknown Channel");
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public List<ChannelInfo> GetTextChannels(ulong guildId)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Could not fetch Discord guild {GuildId} from client", guildId);
            return new List<ChannelInfo>();
        }

        var channels = new List<ChannelInfo>();

        // Add text channels (regular and announcement/news)
        foreach (var channel in guild.TextChannels.Where(c => c != null))
        {
            var displayType = channel is SocketNewsChannel
                ? ChannelDisplayType.Announcement
                : ChannelDisplayType.Text;

            channels.Add(new ChannelInfo(channel.Id, channel.Name, channel.Position, displayType));
        }

        // Add voice channels (they have text chat capability now)
        foreach (var channel in guild.VoiceChannels.Where(c => c != null))
        {
            channels.Add(new ChannelInfo(channel.Id, channel.Name, channel.Position, ChannelDisplayType.Voice));
        }

        // Add stage channels (they also have text chat)
        foreach (var channel in guild.StageChannels.Where(c => c != null))
        {
            channels.Add(new ChannelInfo(channel.Id, channel.Name, channel.Position, ChannelDisplayType.Stage));
        }

        var sortedChannels = channels.OrderBy(c => c.Position).ToList();

        _logger.LogDebug("Retrieved {ChannelCount} text-capable channels for guild {GuildId}",
            sortedChannels.Count, guildId);

        return sortedChannels;
    }
}
