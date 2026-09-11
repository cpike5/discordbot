using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Currency;

/// <summary>
/// The guild's currency list: create a currency, edit its rules, deactivate it, and manage who
/// may mint it. Every write goes out through <c>CurrenciesController</c>, so this page model only
/// reads.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IGuildService _guildService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IndexModel> _logger;
    private readonly ICurrencyService? _currencyService;
    private readonly IWalletRepository? _walletRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="guildService">The guild service.</param>
    /// <param name="settingsService">The settings service, for the runtime feature switch.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">
    /// The currency service, or null when <c>Currency:Enabled</c> is false and the services are
    /// never registered. The page answers 404 in that case.
    /// </param>
    /// <param name="walletRepository">The wallet repository, null under the same condition.</param>
    public IndexModel(
        IGuildService guildService,
        ISettingsService settingsService,
        ILogger<IndexModel> logger,
        ICurrencyService? currencyService = null,
        IWalletRepository? walletRepository = null)
    {
        _guildService = guildService;
        _settingsService = settingsService;
        _logger = logger;
        _currencyService = currencyService;
        _walletRepository = walletRepository;
    }

    /// <summary>The currencies and totals rendered by the page.</summary>
    public CurrencyIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Whether an administrator has switched currency features off at runtime. Managing currencies
    /// still works; nothing is charged while it is true, so the page says so.
    /// </summary>
    public bool IsCurrencyRuntimeDisabled { get; set; }

    /// <summary>
    /// Loads the guild's currencies.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _walletRepository == null)
        {
            _logger.LogDebug("Currency page requested for guild {GuildId} while the feature is disabled", guildId);
            return NotFound();
        }

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        IsCurrencyRuntimeDisabled =
            !(await _settingsService.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", cancellationToken) ?? true);

        var visible = await _currencyService.GetVisibleInGuildAsync(guildId, includeInactive: true, cancellationToken);

        var guildCurrencies = new List<CurrencyPortalItemViewModel>();

        foreach (var currency in visible.Where(c => c.Scope == CurrencyScope.Guild).OrderBy(c => c.Name))
        {
            guildCurrencies.Add(await BuildItemAsync(currency, cancellationToken));
        }

        var prices = await _currencyService.GetPricesForGuildAsync(guildId, cancellationToken);

        ViewModel = new CurrencyIndexViewModel
        {
            GuildId = guildId,
            GuildName = guild.Name,
            GuildCurrencies = guildCurrencies,
            // Deactivated globals are the bot owner's business, not this guild's.
            GlobalCurrencies = visible
                .Where(c => c.Scope == CurrencyScope.Global && c.IsActive)
                .OrderBy(c => c.Name)
                .ToList(),
            PricedFeatureCount = prices.Count(p =>
                p.IsActive && p.FeatureKey.StartsWith(CurrencyFeatureKeys.SoundboardArea + ":", StringComparison.Ordinal))
        };

        PopulateGuildLayout(
            guildId,
            guild.Name,
            guild.IconUrl,
            "currency",
            "Currency",
            $"Currencies, wallets and feature prices for {guild.Name}");

        return Page();
    }

    /// <summary>
    /// Adds the holder, circulation and debtor totals to one currency.
    /// </summary>
    private async Task<CurrencyPortalItemViewModel> BuildItemAsync(
        CurrencyDto currency,
        CancellationToken cancellationToken)
    {
        var wallets = await _walletRepository!.GetForCurrencyAsync(currency.Id, false, cancellationToken);

        return new CurrencyPortalItemViewModel
        {
            Currency = currency,
            HolderCount = wallets.Count,
            Circulation = wallets.Sum(w => w.CachedBalance),
            DebtorCount = wallets.Count(w => w.CachedBalance < 0)
        };
    }
}
