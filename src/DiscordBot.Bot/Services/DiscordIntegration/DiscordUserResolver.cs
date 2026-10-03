using Discord.WebSocket;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.DiscordIntegration;

/// <summary>
/// Resolves Discord user IDs to display information using the Discord REST API
/// with a short-lived in-memory cache (5 minute TTL) to reduce API calls. When Discord cannot be
/// reached (offline mode, a deleted account, an outage) it falls back to the username the bot
/// stored the last time it saw the user (the <c>Users</c> table), and only then to
/// <c>Unknown#id</c>, which the screens render as "Unknown user" (<c>UserDisplay</c>).
/// </summary>
public class DiscordUserResolver : IDiscordUserResolver
{
    private readonly DiscordSocketClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DiscordUserResolver> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "discord_user_";

    public DiscordUserResolver(
        DiscordSocketClient client,
        IMemoryCache cache,
        ILogger<DiscordUserResolver> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc/>
    public async Task<(string Username, string? AvatarUrl)> ResolveUserAsync(ulong userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";

        if (_cache.TryGetValue(cacheKey, out (string Username, string? AvatarUrl) cached))
        {
            return cached;
        }

        var result = await ResolveFromDiscordAsync(userId);

        _cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<ulong, (string Username, string? AvatarUrl)>> ResolveUsersAsync(IEnumerable<ulong> userIds)
    {
        var distinctIds = userIds.Distinct().ToList();
        var results = new Dictionary<ulong, (string Username, string? AvatarUrl)>();

        if (distinctIds.Count == 0)
        {
            return results;
        }

        foreach (var userId in distinctIds)
        {
            results[userId] = await ResolveUserAsync(userId);
        }

        return results;
    }

    private async Task<(string Username, string? AvatarUrl)> ResolveFromDiscordAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            if (user != null)
            {
                return (user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());
            }

            return await FallbackAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return await FallbackAsync(userId);
        }
    }

    /// <summary>The username the bot stored when it last saw the user, else <c>Unknown#id</c>.</summary>
    private async Task<(string Username, string? AvatarUrl)> FallbackAsync(ulong userId)
    {
        if (_scopeFactory is not null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var stored = await users.GetByDiscordIdAsync(userId);
                if (!string.IsNullOrWhiteSpace(stored?.Username))
                {
                    return (stored.Username, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stored username lookup failed for user {UserId}", userId);
            }
        }

        return ($"Unknown#{userId}", null);
    }
}
