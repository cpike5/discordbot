using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.Currency;

/// <summary>
/// The bot-wide currencies, including the credit that backs paid features. Global currencies are
/// the bot owner's: guild admins may price their own features in one, but only a SuperAdmin
/// creates, edits, or mints them.
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ICurrencyService? _currencyService;
    private readonly IWalletRepository? _walletRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">
    /// The currency service, or null when <c>Currency:Enabled</c> is false. The page answers 404
    /// in that case, the same as every other currency surface.
    /// </param>
    /// <param name="walletRepository">The wallet repository, null under the same condition.</param>
    public IndexModel(
        ILogger<IndexModel> logger,
        ICurrencyService? currencyService = null,
        IWalletRepository? walletRepository = null)
    {
        _logger = logger;
        _currencyService = currencyService;
        _walletRepository = walletRepository;
    }

    /// <summary>The global currencies and their totals.</summary>
    public AdminCurrencyViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// The shared wallet and ledger panel. It starts with no currency selected: the operator picks
    /// one, and the panel loads its holders across every guild.
    /// </summary>
    public CurrencyWalletPanelViewModel WalletPanel { get; set; } = new();

    /// <summary>Success message from a previous action.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Error message from a previous action.</summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Loads every global currency.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _walletRepository == null)
        {
            _logger.LogDebug("Bot-wide currency page requested while the feature is disabled");
            return NotFound();
        }

        var currencies = await _currencyService.GetGlobalAsync(includeInactive: true, cancellationToken);
        var items = new List<CurrencyPortalItemViewModel>();

        foreach (var currency in currencies.OrderBy(c => c.Name))
        {
            var wallets = await _walletRepository.GetForCurrencyAsync(currency.Id, false, cancellationToken);

            items.Add(new CurrencyPortalItemViewModel
            {
                Currency = currency,
                HolderCount = wallets.Count,
                Circulation = wallets.Sum(w => w.CachedBalance),
                DebtorCount = wallets.Count(w => w.CachedBalance < 0)
            });
        }

        ViewModel = new AdminCurrencyViewModel { Currencies = items };

        WalletPanel = new CurrencyWalletPanelViewModel
        {
            CurrencyId = null,
            CurrencySymbol = string.Empty,
            // Minting a global currency is still gated by its authority list; the API refuses a
            // SuperAdmin who is not on it, and says so.
            CanMint = true,
            // Fines are a guild moderation action and the service refuses them on a global
            // currency, so the button is not offered here at all.
            CanFine = false,
            CanAdminister = true
        };

        return Page();
    }
}
