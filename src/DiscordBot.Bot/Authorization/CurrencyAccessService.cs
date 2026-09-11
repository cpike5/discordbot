using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Maps a portal user onto a currency's scope, mirroring <see cref="GuildAccessHandler"/>: a
/// SuperAdmin may do anything, a guild admin needs Discord's Administrator permission in the
/// currency's own guild, and moderators and viewers need only be members of it.
/// </summary>
public class CurrencyAccessService : ICurrencyAccessService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildMemberService _members;
    private readonly ILogger<CurrencyAccessService> _logger;

    public CurrencyAccessService(
        DiscordSocketClient discordClient,
        IGuildMemberService members,
        ILogger<CurrencyAccessService> logger)
    {
        _discordClient = discordClient;
        _members = members;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<CurrencyAccessLevel> GetAccessAsync(
        ClaimsPrincipal user,
        CurrencyDto currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(currency);

        // The bot owner owns the global scope and overrides the guild one.
        if (user.IsInRole(Roles.SuperAdmin))
        {
            return Task.FromResult(CurrencyAccessLevel.Administer);
        }

        // Global currencies are the bot owner's, including the credit that backs real spend.
        // Guild admins may price features in them, which is a price entry in their own guild, not
        // a change to the currency.
        if (currency.Scope == CurrencyScope.Global || !currency.GuildId.HasValue)
        {
            return Task.FromResult(CurrencyAccessLevel.None);
        }

        var discordUserId = user.GetDiscordUserId();
        if (discordUserId == 0)
        {
            return Task.FromResult(CurrencyAccessLevel.None);
        }

        var guild = _discordClient.GetGuild(currency.GuildId.Value);
        var guildUser = guild?.GetUser(discordUserId);

        if (guildUser == null)
        {
            _logger.LogDebug(
                "User {DiscordUserId} is not a member of guild {GuildId}, denying currency access",
                discordUserId, currency.GuildId.Value);
            return Task.FromResult(CurrencyAccessLevel.None);
        }

        if (user.IsInRole(Roles.Admin))
        {
            // An Admin without Administrator in this guild still reads, the same way the guild
            // pages let them look without letting them change anything.
            return Task.FromResult(guildUser.GuildPermissions.Administrator
                ? CurrencyAccessLevel.Administer
                : CurrencyAccessLevel.Read);
        }

        if (user.IsInRole(Roles.Moderator))
        {
            return Task.FromResult(CurrencyAccessLevel.Moderate);
        }

        // Reading wallets and ledgers is a portal permission, not something guild membership alone
        // confers: a member with no portal role has the commands, and their own history through
        // /wallet history, but no view of everyone else's balances.
        return Task.FromResult(user.IsInRole(Roles.Viewer)
            ? CurrencyAccessLevel.Read
            : CurrencyAccessLevel.None);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ulong>> GetGuildRoleIdsAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var member = await _members.GetMemberAsync(guildId, userId, cancellationToken);
            return member?.RoleIds ?? (IReadOnlyCollection<ulong>)Array.Empty<ulong>();
        }
        catch (Exception ex)
        {
            // Without roles a mint authority check falls back to the user's own grant, which
            // refuses rather than lets an unauthorized mint through.
            _logger.LogError(ex, "Could not read roles for user {UserId} in guild {GuildId}", userId, guildId);
            return Array.Empty<ulong>();
        }
    }

    /// <inheritdoc />
    public bool IsGuildAdministrator(ulong guildId, ulong userId)
    {
        var guildUser = _discordClient.GetGuild(guildId)?.GetUser(userId);
        return guildUser?.GuildPermissions.Administrator ?? false;
    }
}
