using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Discord;

/// <summary>
/// Resolves the bot owner from the Discord API via GetApplicationInfoAsync.
/// Thread-safe; caches the result since the owner doesn't change at runtime.
/// </summary>
public class DiscordBotOwnerResolver : IBotOwnerResolver
{
    private readonly DiscordSocketClient _client;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ulong? _cachedOwnerId;

    public DiscordBotOwnerResolver(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task<ulong> GetOwnerIdAsync()
    {
        if (_cachedOwnerId.HasValue)
            return _cachedOwnerId.Value;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedOwnerId.HasValue)
                return _cachedOwnerId.Value;

            var appInfo = await _client.GetApplicationInfoAsync();
            _cachedOwnerId = appInfo.Owner.Id;
            return _cachedOwnerId.Value;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
