using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Resolves the bot owner from the Discord API via GetApplicationInfoAsync.
/// Caches the result since the owner doesn't change at runtime.
/// </summary>
public class DiscordBotOwnerResolver : IBotOwnerResolver
{
    private readonly DiscordSocketClient _client;
    private ulong? _cachedOwnerId;

    public DiscordBotOwnerResolver(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task<ulong> GetOwnerIdAsync()
    {
        if (_cachedOwnerId.HasValue)
            return _cachedOwnerId.Value;

        var appInfo = await _client.GetApplicationInfoAsync();
        _cachedOwnerId = appInfo.Owner.Id;
        return _cachedOwnerId.Value;
    }
}
