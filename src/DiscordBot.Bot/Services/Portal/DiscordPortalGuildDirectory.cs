using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;

namespace DiscordBot.Bot.Services.Portal;

/// <summary>
/// The production <see cref="IPortalGuildDirectory"/>: answers from the live Discord client.
/// </summary>
public sealed class DiscordPortalGuildDirectory : IPortalGuildDirectory
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordPortalGuildDirectory> _logger;

    public DiscordPortalGuildDirectory(DiscordSocketClient client, ILogger<DiscordPortalGuildDirectory> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsBotOnline => _client.ConnectionState == ConnectionState.Connected;

    /// <inheritdoc />
    public Task<bool> IsGuildAvailableAsync(ulong guildId, CancellationToken cancellationToken = default)
        => Task.FromResult(_client.GetGuild(guildId) != null);

    /// <inheritdoc />
    public IReadOnlyList<PortalVoiceChannel> GetVoiceChannels(ulong guildId)
    {
        var guild = _client.GetGuild(guildId);
        if (guild == null)
        {
            return [];
        }

        return guild.VoiceChannels
            .Where(c => c != null)
            .OrderBy(c => c.Position)
            .Select(ToChannel)
            .ToList();
    }

    /// <inheritdoc />
    public PortalVoiceChannel? FindVoiceChannel(ulong guildId, ulong channelId)
    {
        var channel = _client.GetGuild(guildId)?.GetVoiceChannel(channelId);
        return channel == null ? null : ToChannel(channel);
    }

    /// <inheritdoc />
    public async Task<bool> IsMemberAsync(ulong guildId, ulong discordUserId, CancellationToken cancellationToken = default)
    {
        var guild = _client.GetGuild(guildId);
        if (guild == null)
        {
            return false;
        }

        if (guild.GetUser(discordUserId) != null)
        {
            return true;
        }

        // Cache miss: AlwaysDownloadUsers is false, so the cache may be incomplete. Ask the REST API.
        try
        {
            var restUser = await _client.Rest.GetGuildUserAsync(guildId, discordUserId);
            return restUser != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify guild membership via REST for user {DiscordUserId} in guild {GuildId}",
                discordUserId, guildId);
            return false;
        }
    }

    private static PortalVoiceChannel ToChannel(SocketVoiceChannel channel)
        => new(channel.Id, channel.Name, channel.ConnectedUsers.Count(u => !u.IsBot));
}
