using System.Security.Claims;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Lists the guilds a user can open in the admin portal, for the guild switcher in the guild header
/// and the recent guilds list in the sidebar.
/// </summary>
/// <remarks>
/// The list holds the active guilds the <c>GuildAccess</c> policy would let the user open (the
/// implementation asks the authorization service, so it cannot drift from the policy). It only
/// decides what is offered: every guild page still authorizes the request itself.
/// </remarks>
public interface IGuildSwitcherService
{
    /// <summary>
    /// Gets the active guilds the user can open, ordered by name. Cached per user for a short time.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guilds, or an empty list for an anonymous user or when the list cannot be loaded.</returns>
    Task<IReadOnlyList<GuildSwitcherEntryDto>> GetAccessibleGuildsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
