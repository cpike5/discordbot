using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// One currency in a portal list, with the totals that make it worth looking at: how many people
/// hold it, how much of it exists, and how many holders are in debt.
/// </summary>
public class CurrencyPortalItemViewModel
{
    /// <summary>The currency and its rules.</summary>
    public CurrencyDto Currency { get; set; } = new();

    /// <summary>How many wallets exist in this currency.</summary>
    public int HolderCount { get; set; }

    /// <summary>Sum of every wallet's balance: the units in circulation.</summary>
    public long Circulation { get; set; }

    /// <summary>How many holders are below zero and therefore locked out of priced features.</summary>
    public int DebtorCount { get; set; }
}

/// <summary>
/// The guild currency list page: this guild's own currencies, plus the global ones it can price
/// features in but cannot edit.
/// </summary>
public class CurrencyIndexViewModel
{
    /// <summary>Discord guild snowflake ID.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Guild display name.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>Currencies this guild owns, deactivated ones included.</summary>
    public IReadOnlyList<CurrencyPortalItemViewModel> GuildCurrencies { get; set; } =
        Array.Empty<CurrencyPortalItemViewModel>();

    /// <summary>Active bot-wide currencies, shown for reference and pricing.</summary>
    public IReadOnlyList<CurrencyDto> GlobalCurrencies { get; set; } = Array.Empty<CurrencyDto>();

    /// <summary>How many soundboard sounds in this guild currently carry a price.</summary>
    public int PricedFeatureCount { get; set; }
}

/// <summary>
/// The currency detail page: one currency, its totals, and what the signed-in user may do with it.
/// </summary>
public class CurrencyDetailsViewModel
{
    /// <summary>Discord guild snowflake ID the page is scoped to.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Guild display name.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>The currency being viewed.</summary>
    public CurrencyDto Currency { get; set; } = new();

    /// <summary>How many wallets exist in this currency.</summary>
    public int HolderCount { get; set; }

    /// <summary>Units in circulation.</summary>
    public long Circulation { get; set; }

    /// <summary>How many holders are below zero.</summary>
    public int DebtorCount { get; set; }

    /// <summary>Whether the viewer may edit rules, adjust rows, and run the reconcile check.</summary>
    public bool CanAdminister { get; set; }

    /// <summary>Whether the viewer may fine holders.</summary>
    public bool CanFine { get; set; }

    /// <summary>Whether the viewer is on this currency's mint authority list.</summary>
    public bool CanMint { get; set; }
}

/// <summary>
/// One priceable soundboard sound on the prices page, with the price it currently carries.
/// </summary>
public class CurrencyPriceRowViewModel
{
    /// <summary>The sound's identifier.</summary>
    public Guid SoundId { get; set; }

    /// <summary>Sound display name.</summary>
    public string SoundName { get; set; } = string.Empty;

    /// <summary>
    /// The key this sound is priced under, built with
    /// <see cref="Core.Constants.CurrencyFeatureKeys.Soundboard(Guid)"/>. The charge seam looks
    /// the price up by this exact string.
    /// </summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>Length in seconds, when it is known.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>How many times the sound has been played.</summary>
    public int PlayCount { get; set; }

    /// <summary>The active price, or null when the sound is free.</summary>
    public PriceEntryDto? Price { get; set; }

    /// <summary>Whether this sound currently costs anything.</summary>
    public bool IsPriced => Price is { IsActive: true };
}

/// <summary>
/// A Discord role offered in the exempt-role picker.
/// </summary>
public class CurrencyRoleOptionViewModel
{
    /// <summary>Discord role snowflake ID.</summary>
    public ulong Id { get; set; }

    /// <summary>Role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Role colour as a hex string, for the swatch.</summary>
    public string Color { get; set; } = string.Empty;
}

/// <summary>
/// The prices page: the guild's sounds with their prices, the currencies that may be charged, and
/// the roles that can be made exempt.
/// </summary>
public class CurrencyPricesViewModel
{
    /// <summary>Discord guild snowflake ID.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Guild display name.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>The guild's sounds, priced or not.</summary>
    public IReadOnlyList<CurrencyPriceRowViewModel> Sounds { get; set; } =
        Array.Empty<CurrencyPriceRowViewModel>();

    /// <summary>Active currencies a price may be set in: this guild's own plus the globals.</summary>
    public IReadOnlyList<CurrencyDto> Currencies { get; set; } = Array.Empty<CurrencyDto>();

    /// <summary>Roles offered in the exempt picker.</summary>
    public IReadOnlyList<CurrencyRoleOptionViewModel> Roles { get; set; } =
        Array.Empty<CurrencyRoleOptionViewModel>();

    /// <summary>
    /// Prices that apply here but are not soundboard sounds — a global entry, or a feature area a
    /// later project added. Read-only, so nothing that costs money is invisible on this page.
    /// </summary>
    public IReadOnlyList<PriceEntryDto> OtherPrices { get; set; } = Array.Empty<PriceEntryDto>();

    /// <summary>How many sounds currently carry a price.</summary>
    public int PricedCount => Sounds.Count(s => s.IsPriced);
}

/// <summary>
/// The bot-wide currency page: the globals, including the credit that backs paid features.
/// </summary>
public class AdminCurrencyViewModel
{
    /// <summary>Every global currency, deactivated ones included.</summary>
    public IReadOnlyList<CurrencyPortalItemViewModel> Currencies { get; set; } =
        Array.Empty<CurrencyPortalItemViewModel>();

    /// <summary>Units in circulation across every global currency.</summary>
    public long TotalCirculation => Currencies.Sum(c => c.Circulation);

    /// <summary>Wallets held across every global currency.</summary>
    public int TotalHolders => Currencies.Sum(c => c.HolderCount);
}
