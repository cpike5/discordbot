using Discord.WebSocket;

namespace DiscordBot.Bot.Services.Reminders;

/// <summary>
/// <see cref="IReminderUserResolver"/> implementation over the real
/// <see cref="DiscordSocketClient"/> - the exact lookup <c>Pages/Guilds/Reminders/Index.cshtml.cs</c>
/// used to inline in its <c>OnGetAsync</c> loop: guild member cache, then a REST fallback wrapped in
/// a try/catch that swallows any failure.
/// </summary>
public sealed class DiscordReminderUserResolver : IReminderUserResolver
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<DiscordReminderUserResolver> _logger;

    public DiscordReminderUserResolver(DiscordSocketClient discordClient, ILogger<DiscordReminderUserResolver> logger)
    {
        _discordClient = discordClient;
        _logger = logger;
    }

    public async Task<ReminderUserInfo> ResolveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        var username = $"Unknown ({userId})";
        string? avatarUrl = null;

        var socketGuild = _discordClient.GetGuild(guildId);
        if (socketGuild is null)
        {
            return new ReminderUserInfo(username, avatarUrl);
        }

        var socketUser = socketGuild.GetUser(userId);
        if (socketUser is not null)
        {
            username = socketUser.GlobalName ?? socketUser.Username;
            avatarUrl = socketUser.GetAvatarUrl() ?? socketUser.GetDefaultAvatarUrl();
            return new ReminderUserInfo(username, avatarUrl);
        }

        try
        {
            var restUser = await _discordClient.Rest.GetUserAsync(userId);
            if (restUser is not null)
            {
                username = restUser.GlobalName ?? restUser.Username;
                avatarUrl = restUser.GetAvatarUrl() ?? restUser.GetDefaultAvatarUrl();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve user {UserId} via REST for guild {GuildId}", userId, guildId);
        }

        return new ReminderUserInfo(username, avatarUrl);
    }
}
