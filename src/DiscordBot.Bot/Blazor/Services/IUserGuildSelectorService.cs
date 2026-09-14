using System.Security.Claims;
using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Loads the current user's Discord guilds, intersected with the bot's actively connected
/// guilds, as <see cref="GuildSelectorItem"/> entries for <c>GuildContextSelector</c>. Extracted
/// from <c>Pages/Search.cshtml.cs</c>'s <c>LoadUserGuildsAsync</c> (docs/plans/blazor-port-plan.md
/// Phase 3) into its own injectable seam: unlike a Razor Pages page model, a bUnit test cannot
/// easily fake <c>DiscordSocketClient.Guilds</c> (populated only by an active gateway connection,
/// with no public setter), so the bot-guild-id lookup is isolated behind this interface instead
/// of the page injecting <c>DiscordSocketClient</c> directly.
/// </summary>
public interface IUserGuildSelectorService
{
    /// <summary>
    /// Returns the given user's linked Discord guilds that the bot is currently connected to,
    /// sorted by name.
    /// </summary>
    Task<IReadOnlyList<GuildSelectorItem>> GetUserGuildsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
