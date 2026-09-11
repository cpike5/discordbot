using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Currency administration for the portal: listing the currencies visible in a guild, creating
/// and editing them, managing who may mint them, and the reconcile check.
/// <para>
/// Routes keyed by guild are gated by the <c>GuildAccess</c> policy. Routes keyed by currency id
/// cannot be, because the guild is a property of the currency rather than of the route, so they
/// go through <see cref="ICurrencyAccessService"/> instead.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public class CurrenciesController : CurrencyControllerBase
{
    private readonly ICurrencyService? _currencyService;
    private readonly ICurrencyAccessService? _accessService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<CurrenciesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrenciesController"/> class.
    /// </summary>
    /// <param name="auditLog">The audit log service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">
    /// The currency service, or null when <c>Currency:Enabled</c> is false and nothing is
    /// registered. Every route answers 404 in that case.
    /// </param>
    /// <param name="accessService">The currency access service, null under the same condition.</param>
    public CurrenciesController(
        IAuditLogService auditLog,
        ILogger<CurrenciesController> logger,
        ICurrencyService? currencyService = null,
        ICurrencyAccessService? accessService = null)
    {
        _auditLog = auditLog;
        _logger = logger;
        _currencyService = currencyService;
        _accessService = accessService;
    }

    /// <summary>
    /// Gets the currencies usable in a guild: the guild's own plus every global one.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="includeInactive">Whether to include deactivated currencies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/guilds/{guildId}/currencies")]
    [Authorize(Policy = "RequireViewer")]
    [Authorize(Policy = "GuildAccess")]
    [ProducesResponseType(typeof(IReadOnlyList<CurrencyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetGuildCurrencies(
        ulong guildId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        var currencies = await _currencyService.GetVisibleInGuildAsync(guildId, includeInactive, cancellationToken);
        return Ok(currencies);
    }

    /// <summary>
    /// Creates a currency owned by a guild. The creator becomes its first mint authority.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The currency rules. Scope and guild come from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/guilds/{guildId}/currencies")]
    [Authorize(Policy = "RequireAdmin")]
    [Authorize(Policy = "GuildAccess")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CurrencyDto>> CreateGuildCurrency(
        ulong guildId,
        [FromBody] CurrencyCreateDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var actorId = User.GetDiscordUserId();
        if (actorId == 0)
        {
            return BadRequestError(
                "Discord account required",
                "Link your Discord account before creating a currency; the creator becomes its first mint authority.");
        }

        var result = await _currencyService.CreateAsync(
            request with { Scope = CurrencyScope.Guild, GuildId = guildId },
            actorId,
            cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error);
        }

        var currency = result.Currency!;

        _logger.LogInformation(
            "Currency {CurrencyId} ({Name}) created in guild {GuildId} by {UserId} from the portal",
            currency.Id, currency.Name, guildId, actorId);

        await _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.CurrencyCreated)
            .ByUser(actorId.ToString())
            .InGuild(guildId)
            .OnTarget("Currency", currency.Id.ToString())
            .WithDetails(new
            {
                currency.Name,
                currency.Symbol,
                currency.IsTransferable,
                currency.AllowNegative,
                currency.DebtFloor,
                source = "portal"
            })
            .LogAsync(cancellationToken);

        return CreatedAtAction(nameof(GetGuildCurrencies), new { guildId }, currency);
    }

    /// <summary>
    /// Changes a currency's rules. Scope, guild, and creator are fixed.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="request">The rules to change. Null members are left alone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut]
    [Route("api/currencies/{id:guid}")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrencyDto>> UpdateCurrency(
        Guid id,
        [FromBody] CurrencyUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var result = await _currencyService.UpdateAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            return FromCurrencyError(result.Error);
        }

        var updated = result.Currency!;

        await LogCurrencyChangeAsync(
            AuditLogAction.CurrencyUpdated,
            updated,
            new
            {
                updated.Name,
                updated.Symbol,
                updated.IsTransferable,
                updated.AllowNegative,
                updated.DebtFloor,
                updated.IncomeAmount,
                IncomeInterval = updated.IncomeInterval?.ToString()
            },
            cancellationToken);

        _logger.LogInformation("Currency {CurrencyId} updated from the portal", id);

        return Ok(updated);
    }

    /// <summary>
    /// Freezes a currency. No mint, spend, transfer, or fine afterwards; history stays readable.
    /// Currencies are never deleted.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/currencies/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrencyDto>> DeactivateCurrency(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var result = await _currencyService.DeactivateAsync(id, cancellationToken);
        if (!result.Success)
        {
            return FromCurrencyError(result.Error);
        }

        await LogCurrencyChangeAsync(
            AuditLogAction.CurrencyDeactivated,
            currency!,
            new { currency!.Name, currency.Symbol },
            cancellationToken);

        _logger.LogInformation("Currency {CurrencyId} deactivated from the portal", id);

        return Ok(result.Currency);
    }

    /// <summary>
    /// Gets the principals allowed to mint a currency.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/currencies/{id:guid}/mint-authorities")]
    [ProducesResponseType(typeof(IReadOnlyList<MintAuthorityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MintAuthorityDto>>> GetMintAuthorities(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        var (_, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var authorities = await _currencyService.GetMintAuthoritiesAsync(id, cancellationToken);
        return Ok(authorities);
    }

    /// <summary>
    /// Adds a principal to a currency's mint authority list.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="request">The principal to grant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/currencies/{id:guid}/mint-authorities")]
    [ProducesResponseType(typeof(MintAuthorityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MintAuthorityDto>> GrantMintAuthority(
        Guid id,
        [FromBody] MintAuthorityGrantRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var actorId = User.GetDiscordUserId();
        if (actorId == 0)
        {
            return BadRequestError(
                "Discord account required",
                "Link your Discord account before granting mint authority; the grant records who made it.");
        }

        var result = await _currencyService.GrantMintAuthorityAsync(
            id, request.PrincipalType, request.PrincipalId, actorId, cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error);
        }

        await LogCurrencyChangeAsync(
            AuditLogAction.MintAuthorityGranted,
            currency!,
            new
            {
                currency!.Name,
                principalType = request.PrincipalType.ToString(),
                principalId = request.PrincipalId?.ToString()
            },
            cancellationToken);

        _logger.LogInformation(
            "Mint authority granted on currency {CurrencyId} to {PrincipalType} {PrincipalId}",
            id, request.PrincipalType, request.PrincipalId?.ToString() ?? "system");

        return Ok(result.Authority);
    }

    /// <summary>
    /// Removes a grant from a currency's mint authority list.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="authorityId">The grant to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete]
    [Route("api/currencies/{id:guid}/mint-authorities/{authorityId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeMintAuthority(
        Guid id,
        Guid authorityId,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        var (currency, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        // The grant has to belong to the currency the caller was authorized against, or a grant on
        // someone else's currency could be revoked through a currency they do administer.
        var authorities = await _currencyService.GetMintAuthoritiesAsync(id, cancellationToken);
        var authority = authorities.FirstOrDefault(a => a.Id == authorityId);

        if (authority == null)
        {
            return NotFoundError("Mint authority not found", "That grant is not on this currency.");
        }

        var removed = await _currencyService.RevokeMintAuthorityAsync(authorityId, cancellationToken);
        if (!removed)
        {
            return NotFoundError("Mint authority not found", "That grant no longer exists.");
        }

        await LogCurrencyChangeAsync(
            AuditLogAction.MintAuthorityRevoked,
            currency!,
            new
            {
                currency!.Name,
                principalType = authority.PrincipalType.ToString(),
                principalId = authority.PrincipalId?.ToString()
            },
            cancellationToken);

        _logger.LogInformation("Mint authority {AuthorityId} revoked on currency {CurrencyId}", authorityId, id);

        return NoContent();
    }

    /// <summary>
    /// Compares every wallet's cached balance against the sum of its ledger rows. An empty list
    /// means the currency reconciles.
    /// </summary>
    /// <param name="id">The currency ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/currencies/{id:guid}/reconcile")]
    [ProducesResponseType(typeof(IReadOnlyList<WalletReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WalletReconciliationDto>>> Reconcile(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null || _accessService == null)
        {
            return FeatureDisabled();
        }

        var (_, failure) = await ResolveCurrencyAsync(
            _currencyService, _accessService, User, id, CurrencyAccessLevel.Administer, cancellationToken);

        if (failure != null)
        {
            return failure;
        }

        var drifted = await _currencyService.ReconcileAsync(id, cancellationToken);

        if (drifted.Count > 0)
        {
            _logger.LogWarning(
                "Currency {CurrencyId} does not reconcile: {Count} wallet(s) differ from their ledger sum",
                id, drifted.Count);
        }

        return Ok(drifted);
    }

    /// <summary>
    /// Gets the bot-wide currencies, including the credit that backs paid features.
    /// </summary>
    /// <param name="includeInactive">Whether to include deactivated currencies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/admin/currencies")]
    [Authorize(Policy = "RequireSuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<CurrencyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetGlobalCurrencies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        var currencies = await _currencyService.GetGlobalAsync(includeInactive, cancellationToken);
        return Ok(currencies);
    }

    /// <summary>
    /// Creates a bot-wide currency. This is where bot credit is created.
    /// </summary>
    /// <param name="request">The currency rules. Scope and guild are set by the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [Route("api/admin/currencies")]
    [Authorize(Policy = "RequireSuperAdmin")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CurrencyDto>> CreateGlobalCurrency(
        [FromBody] CurrencyCreateDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        if (request == null)
        {
            return BadRequestError("Invalid request", "Request body cannot be null.");
        }

        var actorId = User.GetDiscordUserId();
        if (actorId == 0)
        {
            return BadRequestError(
                "Discord account required",
                "Link your Discord account before creating a currency; the creator becomes its first mint authority.");
        }

        var result = await _currencyService.CreateAsync(
            request with { Scope = CurrencyScope.Global, GuildId = null },
            actorId,
            cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error);
        }

        var currency = result.Currency!;

        _logger.LogInformation(
            "Global currency {CurrencyId} ({Name}) created by {UserId}",
            currency.Id, currency.Name, actorId);

        await _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.CurrencyCreated)
            .ByUser(actorId.ToString())
            .OnTarget("Currency", currency.Id.ToString())
            .WithDetails(new
            {
                currency.Name,
                currency.Symbol,
                currency.IsTransferable,
                currency.AllowNegative,
                currency.DebtFloor,
                scope = "global",
                source = "portal"
            })
            .LogAsync(cancellationToken);

        return CreatedAtAction(nameof(GetGlobalCurrencies), null, currency);
    }

    /// <summary>
    /// Writes one configuration audit row for a currency change, scoped to the currency's guild
    /// when it has one.
    /// </summary>
    private async Task LogCurrencyChangeAsync(
        AuditLogAction action,
        CurrencyDto currency,
        object details,
        CancellationToken cancellationToken)
    {
        var builder = _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(action)
            .ByUser(User.GetDiscordUserId().ToString())
            .OnTarget("Currency", currency.Id.ToString())
            .WithDetails(details);

        if (currency.GuildId.HasValue)
        {
            builder = builder.InGuild(currency.GuildId.Value);
        }

        await builder.LogAsync(cancellationToken);
    }
}
