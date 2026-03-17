using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Base controller for API controllers providing shared error response helpers.
/// Ensures consistent error response format across all API endpoints.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Creates a 404 Not Found response with a standardized <see cref="ApiErrorDto"/> body.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="detail">Optional additional detail about the error.</param>
    /// <returns>A <see cref="NotFoundObjectResult"/> containing the error DTO.</returns>
    protected NotFoundObjectResult NotFoundError(string message, string? detail = null)
    {
        return NotFound(new ApiErrorDto
        {
            Message = message,
            Detail = detail,
            StatusCode = StatusCodes.Status404NotFound,
            TraceId = HttpContext.GetCorrelationId()
        });
    }

    /// <summary>
    /// Creates a 400 Bad Request response with a standardized <see cref="ApiErrorDto"/> body.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="detail">Optional additional detail about the error.</param>
    /// <returns>A <see cref="BadRequestObjectResult"/> containing the error DTO.</returns>
    protected BadRequestObjectResult BadRequestError(string message, string? detail = null)
    {
        return BadRequest(new ApiErrorDto
        {
            Message = message,
            Detail = detail,
            StatusCode = StatusCodes.Status400BadRequest,
            TraceId = HttpContext.GetCorrelationId()
        });
    }

    /// <summary>
    /// Creates a 422 Unprocessable Entity response with a standardized <see cref="ApiErrorDto"/> body
    /// and optional validation error details.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errors">Optional dictionary of field-level validation errors.</param>
    /// <returns>An <see cref="ObjectResult"/> with status 422 containing the error DTO.</returns>
    protected ObjectResult ValidationError(string message, IDictionary<string, string[]>? errors = null)
    {
        var detail = errors != null
            ? string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"))
            : null;

        var errorDto = new ApiErrorDto
        {
            Message = message,
            Detail = detail,
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            TraceId = HttpContext.GetCorrelationId()
        };

        return new ObjectResult(errorDto)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }
}
