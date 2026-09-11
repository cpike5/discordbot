using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Currency;

/// <summary>
/// One currency's wallets and ledger. Administrators get the mint, fine and adjust actions;
/// moderators read the same lists and may fine. What the viewer may do is decided by
/// <see cref="ICurrencyAccessService"/>, the same seam the API routes use, so the buttons the page
/// renders and the calls the API accepts cannot drift apart.
/// </summary>
[Authorize(Policy = "RequireModerator")]
[Authorize(Policy = "GuildAccess")]
public class DetailsModel : GuildPageModelBase
{
    private readonly IGuildService _guildService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DetailsModel> _logger;
    private readonly ICurrencyService? _currencyService;
    private readonly ICurrencyAccessService? _accessService;
    private readonly IWalletRepository? _walletRepository;
    private readonly IMintService? _mintService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetailsModel"/> class.
    /// </summary>
    /// <param name="guildService">The guild service.</param>
    /// <param name="settingsService">The settings service, for the runtime feature switch.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">The currency service, null when the feature is off.</param>
    /// <param name="accessService">The currency access service, null when the feature is off.</param>
    /// <param name="walletRepository">The wallet repository, null when the feature is off.</param>
    /// <param name="mintService">The mint service, null when the feature is off.</param>
    public DetailsModel(
        IGuildService guildService,
        ISettingsService settingsService,
        ILogger<DetailsModel> logger,
        ICurrencyService? currencyService = null,
        ICurrencyAccessService? accessService = null,
        IWalletRepository? walletRepository = null,
        IMintService? mintService = null)
    {
        _guildService = guildService;
        _settingsService = settingsService;
        _logger = logger;
        _currencyService = currencyService;
        _accessService = accessService;
        _walletRepository = walletRepository;
        _mintService = mintService;
    }

    /// <summary>The currency, its totals, and what the viewer may do with it.</summary>
    public CurrencyDetailsViewModel ViewModel { get; set; } = new();

    /// <summary>The shared wallet and ledger panel's configuration.</summary>
    public CurrencyWalletPanelViewModel WalletPanel { get; set; } = new();

    /// <summary>Whether currency features are switched off at runtime.</summary>
    public bool IsCurrencyRuntimeDisabled { get; set; }

    /// <summary>
    /// Loads one of the guild's own currencies.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from the route.</param>
    /// <param name="currencyId">The currency's ID from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null
            || _walletRepository == null || _mintService == null)
        {
            return NotFound();
        }

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        var currency = await _currencyService.GetAsync(currencyId, cancellationToken);

        // This page is scoped to the guild in its route. A global currency's wallets live on the
        // bot-wide page, where they are read across every guild at once.
        if (currency == null || currency.Scope != CurrencyScope.Guild || currency.GuildId != guildId)
        {
            return NotFound();
        }

        var access = await _accessService.GetAccessAsync(User, currency, cancellationToken);
        if (access == CurrencyAccessLevel.None)
        {
            return NotFound();
        }

        IsCurrencyRuntimeDisabled =
            !(await _settingsService.GetSettingValueAsync<bool?>("Features:CurrencyEnabled", cancellationToken) ?? true);

        var wallets = await _walletRepository.GetForCurrencyAsync(currencyId, false, cancellationToken);

        var actorId = User.GetDiscordUserId();
        var canMint = false;

        if (actorId != 0)
        {
            // Minting is gated by the currency's own authority list, not by a portal role, so the
            // button only appears for someone the mint service would actually let through.
            var roleIds = await _accessService.GetGuildRoleIdsAsync(guildId, actorId, cancellationToken);
            canMint = currency.IsActive
                && await _mintService.CanMintAsync(currencyId, actorId, roleIds, cancellationToken);
        }

        ViewModel = new CurrencyDetailsViewModel
        {
            GuildId = guildId,
            GuildName = guild.Name,
            Currency = currency,
            HolderCount = wallets.Count,
            Circulation = wallets.Sum(w => w.CachedBalance),
            DebtorCount = wallets.Count(w => w.CachedBalance < 0),
            CanAdminister = access >= CurrencyAccessLevel.Administer,
            CanFine = access >= CurrencyAccessLevel.Moderate && currency.IsActive,
            CanMint = canMint
        };

        WalletPanel = new CurrencyWalletPanelViewModel
        {
            CurrencyId = currency.Id,
            CurrencySymbol = currency.Symbol,
            CanMint = ViewModel.CanMint,
            CanFine = ViewModel.CanFine,
            CanAdminister = ViewModel.CanAdminister && currency.IsActive
        };

        _logger.LogDebug("Loaded currency {CurrencyId} detail page for guild {GuildId}", currencyId, guildId);

        PopulateGuildLayout(
            guildId,
            guild.Name,
            guild.IconUrl,
            "currency",
            $"{currency.Symbol} {currency.Name}".Trim(),
            $"Wallets and ledger for {currency.Name}");

        return Page();
    }
}
