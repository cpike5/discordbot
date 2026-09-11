using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Wallets and the balance actions behind the currency detail page: the holder list, one wallet's
/// ledger, and the mint, fine and adjust operations.
/// <para>
/// Every write goes through the same services the Discord commands use, so the balance rules and
/// the audit rows are identical whichever surface issued them.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public class WalletsController : CurrencyControllerBase
{
    private const int MaxLedgerPageSize = 100;

    private readonly IDiscordUserResolver _userResolver;
    private readonly IModerationService _moderationService;
    private readonly ILogger<WalletsController> _logger;
    private readonly ICurrencyService? _currencyService;
    private readonly ICurrencyAccessService? _accessService;
    private readonly IWalletService? _walletService;
    private readonly IWalletRepository? _walletRepository;
    private readonly IMintService? _mintService;
    private readonly ILedgerRepository? _ledgerRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalletsController"/> class.
    /// </summary>
    /// <param name="userResolver">Resolves Discord IDs to names for the holder list.</param>
    /// <param name="moderationService">Opens the mod case a fine may be linked to.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">The currency service, null when the feature is off.</param>
    /// <param name="accessService">The currency access service, null when the feature is off.</param>
    /// <param name="walletService">The wallet service, null when the feature is off.</param>
    /// <param name="walletRepository">The wallet repository, null when the feature is off.</param>
    /// <param name="mintService">The mint service, null when the feature is off.</param>
    /// <param name="ledgerRepository">The ledger repository, null when the feature is off.</param>
    public WalletsController(
        IDiscordUserResolver userResolver,
        IModerationService moderationService,
        ILogger<WalletsController> logger,
        ICurrencyService? currencyService = null,
        ICurrencyAccessService? accessService = null,
        IWalletService? walletService = null,
        IWalletRepository? walletRepository = null,
        IMintService? mintService = null,
        ILedgerRepository? ledgerRepository = null)
    {
        _userResolver = userResolver;
        _moderationService = moderationService;
        _logger = logger;
        _currencyService = currencyService;
        _accessService = accessService;
        _walletService = walletService;
        _walletRepository = walletRepository;
        _mintService = mintService;
        _ledgerRepository = ledgerRepository;
    }

    /// <summary>
    /// Gets the wallets in one currency, highest balance first, with the holders' display names.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="debtorsOnly">Whether to return only wallets below zero.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/currencies/{id:guid}/wallets")]
    [ProducesResponseType(typeof(IReadOnlyList<WalletHolderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WalletHolderDto>>> GetWallets(
        Guid id,
        [FromQuery] bool debtorsOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null || _walletRepository == null)
        {
            return FeatureDisabled();
        }

        var (_, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Read, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var wallets = await _walletRepository.GetForCurrencyAsync(id, debtorsOnly, cancellationToken);

        var names = await _userResolver.ResolveUsersAsync(wallets.Select(w => w.UserId));

        var holders = wallets.Select(w =>
        {
            var resolved = names.TryGetValue(w.UserId, out var identity);

            return new WalletHolderDto
            {
                WalletId = w.Id,
                UserId = w.UserId,
                Username = resolved ? identity.Username : $"Unknown ({w.UserId})",
                AvatarUrl = resolved ? identity.AvatarUrl : null,
                Balance = w.CachedBalance
            };
        }).ToList();

        return Ok(holders);
    }

    /// <summary>
    /// Gets one page of a wallet's ledger, newest first.
    /// </summary>
    /// <param name="id">The wallet ID.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page, capped at 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/wallets/{id:guid}/ledger")]
    [ProducesResponseType(typeof(PagedResult<LedgerTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LedgerTransactionDto>>> GetLedger(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null
            || _walletService == null || _walletRepository == null)
        {
            return FeatureDisabled();
        }

        var wallet = await _walletRepository.GetAsync(id, cancellationToken);
        if (wallet == null)
        {
            return NotFoundError("Wallet not found", $"No wallet with ID {id} exists.");
        }

        var currency = await _currencyService.GetAsync(wallet.CurrencyId, cancellationToken);
        if (currency == null)
        {
            return NotFoundError("Wallet not found", $"No wallet with ID {id} exists.");
        }

        // A wallet's own holder may always read their history, even when they hold no portal role
        // in the guild the currency belongs to.
        var isOwner = User.GetDiscordUserId() == wallet.UserId && wallet.UserId != 0;

        if (!isOwner)
        {
            var access = await _accessService.GetAccessAsync(User, currency, cancellationToken);
            if (access < CurrencyAccessLevel.Read)
            {
                return NotFoundError("Wallet not found", $"No wallet with ID {id} exists.");
            }
        }

        var history = await _walletService.GetHistoryAsync(
            id,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, MaxLedgerPageSize),
            cancellationToken);

        return Ok(history);
    }

    /// <summary>
    /// Creates units and credits them to a user's wallet. The mint authority list decides who may
    /// call this; being a guild admin is not enough on its own.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="request">Recipient, amount and reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/currencies/{id:guid}/mint")]
    [ProducesResponseType(typeof(MintResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MintResult>> Mint(
        Guid id,
        [FromBody] MintRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null || _mintService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        // Reading the currency is the gate for seeing it at all; the mint authority list is the
        // gate for minting it, and MintService enforces that.
        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Read, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return FromCurrencyError(CurrencyErrors.ReasonRequired);
        }

        var actorId = User.GetDiscordUserId();
        if (actorId == 0)
        {
            return Forbidden("Not a mint authority", "Link your Discord account to mint currency.");
        }

        var result = await _mintService.MintAsync(
            id,
            request.UserId,
            request.Amount,
            request.Reason.Trim(),
            LedgerSource.Manual,
            $"portal:mint:{id}:{request.UserId}:{Guid.NewGuid()}",
            actorId,
            cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error, request.Amount, symbol: currency!.Symbol);
        }

        _logger.LogInformation(
            "Minted {Amount} of currency {CurrencyId} to user {UserId} from the portal by {ActorId}",
            request.Amount, id, request.UserId, actorId);

        return Ok(result);
    }

    /// <summary>
    /// Fines a user. Guild currencies only, and the amount is clamped at zero or at the currency's
    /// debt floor; the response says when it was.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="request">Target, amount, reason, and whether to open a mod case.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/currencies/{id:guid}/fine")]
    [ProducesResponseType(typeof(FineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FineResult>> Fine(
        Guid id,
        [FromBody] FineRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null || _walletService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Moderate, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return FromCurrencyError(CurrencyErrors.ReasonRequired);
        }

        // Fines are a guild moderation action. The service refuses a global currency too; this is
        // here so the portal gets the reason without a round trip through the ledger.
        if (currency!.Scope != CurrencyScope.Guild || !currency.GuildId.HasValue)
        {
            return FromCurrencyError(CurrencyErrors.FineRequiresGuildCurrency);
        }

        var moderatorId = User.GetDiscordUserId();
        if (moderatorId == 0)
        {
            return Forbidden("Discord account required", "Link your Discord account before issuing a fine.");
        }

        if (moderatorId == request.UserId)
        {
            return Forbidden("Not allowed", "You can't fine yourself.");
        }

        // A moderator cannot fine an administrator; the same hierarchy rule the /wallet fine
        // command follows. An administrator fining another administrator is allowed.
        var guildId = currency.GuildId.Value;
        if (_accessService.IsGuildAdministrator(guildId, request.UserId)
            && !_accessService.IsGuildAdministrator(guildId, moderatorId))
        {
            return Forbidden("Not allowed", "You can't fine an administrator.");
        }

        Guid? caseId = null;

        if (request.OpenCase)
        {
            try
            {
                var modCase = await _moderationService.CreateCaseAsync(new ModerationCaseCreateDto
                {
                    GuildId = guildId,
                    TargetUserId = request.UserId,
                    ModeratorUserId = moderatorId,
                    Type = CaseType.Note,
                    Reason = $"Fine: {request.Reason.Trim()}"
                });

                caseId = modCase.Id;
            }
            catch (Exception ex)
            {
                // The fine is the point; a failed case link should not swallow it.
                _logger.LogError(ex, "Failed to open a mod case for a portal fine in guild {GuildId}", guildId);
            }
        }

        var result = await _walletService.FineAsync(
            id, request.UserId, request.Amount, request.Reason.Trim(), moderatorId, caseId, cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error, request.Amount, symbol: currency.Symbol);
        }

        _logger.LogInformation(
            "User {TargetId} fined {Amount} of currency {CurrencyId} from the portal by {ModeratorId}",
            request.UserId, request.Amount, id, moderatorId);

        return Ok(result);
    }

    /// <summary>
    /// Corrects a ledger row by writing a signed adjustment against the same wallet. The only way
    /// to fix a mistake, and it is audited.
    /// </summary>
    /// <param name="txId">The ledger row being corrected.</param>
    /// <param name="request">Signed amount and the reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/ledger/{txId:long}/adjust")]
    [ProducesResponseType(typeof(AdjustmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdjustmentResult>> Adjust(
        long txId,
        [FromBody] AdjustRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null
            || _walletService == null || _walletRepository == null || _ledgerRepository == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var row = await _ledgerRepository.GetByIdAsync(txId, cancellationToken);
        if (row == null)
        {
            return NotFoundError("Transaction not found", $"No ledger row with ID {txId} exists.");
        }

        var wallet = await _walletRepository.GetAsync(row.WalletId, cancellationToken);
        if (wallet == null)
        {
            return NotFoundError("Transaction not found", $"No ledger row with ID {txId} exists.");
        }

        var (_, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, wallet.CurrencyId, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return FromCurrencyError(CurrencyErrors.ReasonRequired);
        }

        var actorId = User.GetDiscordUserId();
        if (actorId == 0)
        {
            return Forbidden("Discord account required", "Link your Discord account before adjusting the ledger.");
        }

        var result = await _walletService.AdjustAsync(
            txId, request.Amount, request.Reason.Trim(), actorId, cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error, Math.Abs(request.Amount));
        }

        _logger.LogInformation(
            "Ledger row {TransactionId} adjusted by {Amount} from the portal by {ActorId}",
            txId, request.Amount, actorId);

        return Ok(result);
    }
}
