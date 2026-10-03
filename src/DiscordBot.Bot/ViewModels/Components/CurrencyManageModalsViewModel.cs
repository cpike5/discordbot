using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Drives <c>_CurrencyManageModals</c>: the currency editor and the mint authority dialogs shared
/// by the guild currency page and the bot-wide one.
/// </summary>
public record CurrencyManageModalsViewModel
{
    /// <summary>
    /// True on the bot-wide page. Global currencies have no roles and are not transferable by
    /// default, and their copy says so.
    /// </summary>
    public bool IsGlobal { get; init; }

    /// <summary>
    /// The guild whose members the user picker searches. Null on the bot-wide page, where the
    /// picker searches every user the bot has seen.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>The guild's roles, for granting minting to a role. Empty on the bot-wide page.</summary>
    public IReadOnlyList<CurrencyRoleOptionViewModel> Roles { get; init; } = Array.Empty<CurrencyRoleOptionViewModel>();
}
