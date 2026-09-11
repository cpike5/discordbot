using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Shared base for the three currency controllers: the feature switch, the scope check every
/// currency-keyed route makes, and one place that turns a <see cref="CurrencyErrors"/> code into
/// an HTTP status.
/// <para>
/// The currency services are registered only when <c>Currency:Enabled</c> is true, so each
/// controller takes them as optional constructor arguments and answers 404 when they are absent.
/// That is the rollback path: with the feature off the routes simply are not there.
/// </para>
/// </summary>
public abstract class CurrencyControllerBase : ApiControllerBase
{
    /// <summary>
    /// The answer for a request made while the currency feature is switched off.
    /// </summary>
    protected NotFoundObjectResult FeatureDisabled() =>
        NotFoundError("Currency feature is not enabled", "The virtual currency feature is switched off on this bot.");

    /// <summary>
    /// Turns a failed currency result into the matching HTTP response, with a sentence the portal
    /// can show as-is.
    /// </summary>
    /// <param name="error">Reason code from <see cref="CurrencyErrors"/>.</param>
    /// <param name="amount">Amount asked for, when the code is about money.</param>
    /// <param name="balance">Balance at the time, when the code is about money.</param>
    /// <param name="symbol">Currency symbol, when the code is about money.</param>
    protected ObjectResult FromCurrencyError(string? error, long amount = 0, long balance = 0, string symbol = "")
    {
        var detail = CurrencyFormatting.DescribeError(error, balance, symbol, amount);

        var status = error switch
        {
            CurrencyErrors.CurrencyNotFound => StatusCodes.Status404NotFound,
            CurrencyErrors.WalletNotFound => StatusCodes.Status404NotFound,
            CurrencyErrors.ReferenceNotFound => StatusCodes.Status404NotFound,
            CurrencyErrors.NotAuthorizedToMint => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        var message = status switch
        {
            StatusCodes.Status404NotFound => "Not found",
            StatusCodes.Status403Forbidden => "Not allowed",
            _ => "The request could not be completed"
        };

        return CurrencyError(status, message, detail, error);
    }

    /// <summary>
    /// Builds an error response carrying the machine-readable <see cref="CurrencyErrors"/> code,
    /// so the page scripts can branch on the reason rather than on the sentence.
    /// </summary>
    /// <param name="statusCode">HTTP status to return.</param>
    /// <param name="message">Short error message.</param>
    /// <param name="detail">Sentence the portal shows.</param>
    /// <param name="errorCode">Reason code from <see cref="CurrencyErrors"/>, when there is one.</param>
    protected ObjectResult CurrencyError(int statusCode, string message, string? detail, string? errorCode = null)
    {
        var errorDto = new ApiErrorDto
        {
            Message = message,
            Detail = detail,
            StatusCode = statusCode,
            ErrorCode = errorCode,
            TraceId = HttpContext.GetCorrelationId()
        };

        return new ObjectResult(errorDto) { StatusCode = statusCode };
    }

    /// <summary>
    /// Creates a 403 Forbidden response with the standard error body.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="detail">Optional additional detail.</param>
    protected ObjectResult Forbidden(string message, string? detail = null) =>
        CurrencyError(StatusCodes.Status403Forbidden, message, detail);

    /// <summary>
    /// Loads a currency and checks the caller's access to it in one step.
    /// </summary>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="accessService">The access service.</param>
    /// <param name="user">The signed-in portal user.</param>
    /// <param name="currencyId">The currency being acted on.</param>
    /// <param name="required">The access level the route needs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The currency when the caller may act on it, otherwise the response to return instead. A
    /// currency the caller cannot see is a 404, not a 403, so the routes do not confirm that an
    /// id exists to someone who may not look at it.
    /// </returns>
    protected async Task<(CurrencyDto? Currency, ActionResult? Failure)> ResolveCurrencyAsync(
        ICurrencyService currencyService,
        ICurrencyAccessService accessService,
        ClaimsPrincipal user,
        Guid currencyId,
        CurrencyAccessLevel required,
        CancellationToken cancellationToken = default)
    {
        var currency = await currencyService.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return (null, NotFoundError("Currency not found", $"No currency with ID {currencyId} exists."));
        }

        var access = await accessService.GetAccessAsync(user, currency, cancellationToken);

        if (access == CurrencyAccessLevel.None)
        {
            return (null, NotFoundError("Currency not found", $"No currency with ID {currencyId} exists."));
        }

        if (access < required)
        {
            return (null, Forbidden(
                "Not allowed",
                "You do not have permission to perform this action on this currency."));
        }

        return (currency, null);
    }
}
