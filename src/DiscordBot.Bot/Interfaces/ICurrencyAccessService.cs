using System.Security.Claims;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// What a signed-in portal user may do with one currency.
/// <para>
/// Ordered, so a check reads <c>access &gt;= CurrencyAccessLevel.Moderate</c>.
/// </para>
/// </summary>
public enum CurrencyAccessLevel
{
    /// <summary>Not visible. Every currency route refuses.</summary>
    None = 0,

    /// <summary>May read wallets and ledgers, and nothing else.</summary>
    Read = 1,

    /// <summary>May read, and may fine in this guild's currencies.</summary>
    Moderate = 2,

    /// <summary>May edit the currency's rules, its mint authorities, its prices, and adjust rows.</summary>
    Administer = 3
}

/// <summary>
/// Resolves a portal user's access to a currency.
/// <para>
/// The <c>GuildAccess</c> policy answers this for routes that carry a <c>guildId</c>, but the
/// currency routes are keyed by currency id, and a currency carries its own scope: a guild
/// currency belongs to one guild's admins and moderators, a global one to the bot owner. This
/// service is where that mapping lives, so every currency controller asks the same question the
/// same way.
/// </para>
/// </summary>
public interface ICurrencyAccessService
{
    /// <summary>
    /// Gets what the user may do with a currency.
    /// </summary>
    /// <param name="user">The signed-in portal user.</param>
    /// <param name="currency">The currency being acted on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CurrencyAccessLevel> GetAccessAsync(
        ClaimsPrincipal user,
        CurrencyDto currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a member's Discord role IDs in a guild, for the mint authority check. Returns an empty
    /// set when the guild or the member is unknown, which refuses rather than allows.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="userId">Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyCollection<ulong>> GetGuildRoleIdsAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a member holds Discord's Administrator permission in a guild. Moderators may not
    /// fine an administrator, the same hierarchy rule the other moderation actions follow.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="userId">Discord user snowflake ID.</param>
    bool IsGuildAdministrator(ulong guildId, ulong userId);
}
