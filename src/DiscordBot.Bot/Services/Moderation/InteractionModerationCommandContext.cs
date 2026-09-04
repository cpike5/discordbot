using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// Adapts a real <see cref="SocketInteractionContext"/> to <see cref="IModerationCommandContext"/>
/// so <see cref="ModerationActionRunner"/> never touches Discord.Net's concrete socket types.
/// </summary>
public class InteractionModerationCommandContext : IModerationCommandContext
{
    private readonly SocketInteractionContext _context;
    private readonly ILogger _logger;

    public InteractionModerationCommandContext(SocketInteractionContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public IGuild Guild => _context.Guild;

    public IUser ModeratorUser => _context.User;

    public ulong BotUserId => _context.Client.CurrentUser.Id;

    public int? ModeratorHierarchy => _context.User is SocketGuildUser moderator ? moderator.Hierarchy : null;

    /// <summary>
    /// Resolves an <see cref="IGuildUser"/> for the target, first from cache then via REST.
    /// </summary>
    public async Task<IGuildUser?> ResolveGuildUserAsync(IUser user)
    {
        var guildUser = user as IGuildUser ?? _context.Guild.GetUser(user.Id);
        if (guildUser == null)
        {
            guildUser = await _context.Client.Rest.GetGuildUserAsync(_context.Guild.Id, user.Id);
        }

        if (guildUser == null)
        {
            _logger.LogWarning(
                "Could not resolve guild user {UserId} ({Username}) in guild {GuildId}. User may not be a member of this server.",
                user.Id, user.Username, _context.Guild.Id);
        }

        return guildUser;
    }
}
