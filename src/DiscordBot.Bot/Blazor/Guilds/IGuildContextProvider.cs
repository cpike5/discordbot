using System.Security.Claims;

namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// Resolves a <see cref="GuildContext"/> for a guild route: loads the guild, checks the
/// <c>GuildAccess</c> authorization policy, and computes the permission/feature-flag/nav-tab data
/// every guild page needs. Registered scoped (one instance per circuit/prerender scope) and
/// memoises its result per <paramref name="guildId"/> for the lifetime of that scope, so a page
/// and the layout wrapping it both call <see cref="GetAsync"/> without loading the guild twice.
/// See "GuildContext" in <c>docs/architecture/patterns.md</c>.
/// </summary>
public interface IGuildContextProvider
{
    /// <summary>
    /// Resolves the <see cref="GuildContext"/> for <paramref name="guildId"/> and
    /// <paramref name="user"/>. Safe to call repeatedly for the same <paramref name="guildId"/>
    /// within one scope - only the first call does any work.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="user">
    /// The signed-in user's principal (e.g. from <c>(await AuthenticationStateTask).User</c>) -
    /// not cached as part of the memoisation key, since one scope belongs to one user.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<GuildContextResult> GetAsync(ulong guildId, ClaimsPrincipal user, CancellationToken ct = default);
}
