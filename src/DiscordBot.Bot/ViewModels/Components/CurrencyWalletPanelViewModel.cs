namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// The wallet and ledger panel shared by the guild currency detail page and the bot-wide currency
/// page. The panel itself is static markup; <c>wwwroot/js/currency/currency-wallets.js</c> fills
/// it from the wallet and ledger endpoints.
/// </summary>
public record CurrencyWalletPanelViewModel
{
    /// <summary>
    /// The currency to load on first render, or null when the page picks one later (the bot-wide
    /// page starts with no currency selected).
    /// </summary>
    public Guid? CurrencyId { get; init; }

    /// <summary>Symbol rendered next to every amount.</summary>
    public string CurrencySymbol { get; init; } = string.Empty;

    /// <summary>Whether the viewer is on the currency's mint authority list.</summary>
    public bool CanMint { get; init; }

    /// <summary>Whether the viewer may fine holders.</summary>
    public bool CanFine { get; init; }

    /// <summary>Whether the viewer may adjust rows and run the reconcile check.</summary>
    public bool CanAdminister { get; init; }
}
