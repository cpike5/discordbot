using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Prices for a guild's features. A feature with no active entry is free, which is why these
/// routes only ever create, replace, or deactivate one entry at a time.
/// <para>
/// The feature key is the whole contract between this controller and the priced feature: the key
/// saved here has to be the key the charge seam asks for, so callers build it with
/// <see cref="Core.Constants.CurrencyFeatureKeys"/> rather than by hand.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class PricesController : CurrencyControllerBase
{
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<PricesController> _logger;
    private readonly ICurrencyService? _currencyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricesController"/> class.
    /// </summary>
    /// <param name="auditLog">The audit log service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="currencyService">The currency service, null when the feature is off.</param>
    public PricesController(
        IAuditLogService auditLog,
        ILogger<PricesController> logger,
        ICurrencyService? currencyService = null)
    {
        _auditLog = auditLog;
        _logger = logger;
        _currencyService = currencyService;
    }

    /// <summary>
    /// Gets every price that applies in a guild, including the entries that apply everywhere.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/guilds/{guildId}/prices")]
    [ProducesResponseType(typeof(IReadOnlyList<PriceEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PriceEntryDto>>> GetPrices(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        var prices = await _currencyService.GetPricesForGuildAsync(guildId, cancellationToken);
        return Ok(prices);
    }

    /// <summary>
    /// Gets the price that applies to one feature in a guild, or 404 when it is free there.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="featureKey">The feature key, shaped <c>{area}:{identifier}</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [Route("api/guilds/{guildId}/prices/{featureKey}")]
    [ProducesResponseType(typeof(PriceEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceEntryDto>> GetPrice(
        ulong guildId,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        var price = await _currencyService.GetActivePriceAsync(featureKey, guildId, cancellationToken);

        if (price == null)
        {
            return NotFoundError("Price not found", $"'{featureKey}' is free in this server.");
        }

        return Ok(price);
    }

    /// <summary>
    /// Sets the price of a feature in a guild, replacing any existing entry for the same key. A
    /// guild currency may only price features in its own guild; the service enforces that and this
    /// returns the reason.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="featureKey">The feature key, shaped <c>{area}:{identifier}</c>.</param>
    /// <param name="request">Currency, amount, exempt roles, and whether it is charged.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut]
    [Route("api/guilds/{guildId}/prices/{featureKey}")]
    [ProducesResponseType(typeof(PriceEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PriceEntryDto>> SetPrice(
        ulong guildId,
        string featureKey,
        [FromBody] PriceSaveRequestDto request,
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
                "Link your Discord account before setting prices; the entry records who set it.");
        }

        var result = await _currencyService.SetPriceAsync(
            new PriceEntrySaveDto
            {
                FeatureKey = featureKey,
                GuildId = guildId,
                CurrencyId = request.CurrencyId,
                Amount = request.Amount,
                ExemptRoleIds = request.ExemptRoleIds,
                IsActive = request.IsActive
            },
            actorId,
            cancellationToken);

        if (!result.Success)
        {
            return FromCurrencyError(result.Error, request.Amount);
        }

        var price = result.Price!;

        _logger.LogInformation(
            "Price {Amount} set on {FeatureKey} in guild {GuildId} by {ActorId}",
            price.Amount, price.FeatureKey, guildId, actorId);

        await _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.PriceSet)
            .ByUser(actorId.ToString())
            .InGuild(guildId)
            .OnTarget("PriceEntry", price.FeatureKey)
            .WithDetails(new
            {
                price.FeatureKey,
                price.Amount,
                price.CurrencyId,
                price.CurrencyName,
                price.IsActive,
                exemptRoleIds = price.ExemptRoleIds.Select(r => r.ToString()).ToArray()
            })
            .LogAsync(cancellationToken);

        return Ok(price);
    }

    /// <summary>
    /// Makes a feature free again. The entry is deactivated rather than deleted, so its exempt
    /// roles survive a price being turned off and on.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="featureKey">The feature key, shaped <c>{area}:{identifier}</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete]
    [Route("api/guilds/{guildId}/prices/{featureKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePrice(
        ulong guildId,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        if (_currencyService == null)
        {
            return FeatureDisabled();
        }

        var removed = await _currencyService.RemovePriceAsync(featureKey, guildId, cancellationToken);

        if (!removed)
        {
            return NotFoundError("Price not found", $"'{featureKey}' is already free in this server.");
        }

        _logger.LogInformation(
            "Price removed from {FeatureKey} in guild {GuildId} by {ActorId}",
            featureKey, guildId, User.GetDiscordUserId());

        await _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.PriceRemoved)
            .ByUser(User.GetDiscordUserId().ToString())
            .InGuild(guildId)
            .OnTarget("PriceEntry", featureKey)
            .WithDetails(new { featureKey })
            .LogAsync(cancellationToken);

        return NoContent();
    }
}
