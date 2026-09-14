using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for lightweight preview data for user and guild popups. Still serves
/// <c>wwwroot/js/preview-popup.js</c> for the Razor Pages that haven't been ported to Blazor yet
/// (the Blazor surface, <c>Blazor/Shared/Overlays/UserPreview.razor</c>/<c>GuildPreview.razor</c>,
/// calls <see cref="IPreviewService"/> directly in-circuit instead of over HTTP) - a thin wrapper
/// over the lookup logic <see cref="IPreviewService"/> now owns
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4d).
/// </summary>
[ApiController]
[Route("api/preview")]
[Authorize(Policy = "RequireViewer")]
public class PreviewController : ControllerBase
{
    private readonly IPreviewService _previewService;
    private readonly ILogger<PreviewController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewController"/> class.
    /// </summary>
    public PreviewController(
        IPreviewService previewService,
        ILogger<PreviewController> logger)
    {
        _previewService = previewService;
        _logger = logger;
    }

    /// <summary>
    /// Gets preview data for a user (without guild context).
    /// </summary>
    /// <param name="userId">The Discord user ID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User preview data.</returns>
    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(UserPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPreviewDto>> GetUserPreview(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(userId, out var userIdParsed))
        {
            return BadRequest(InvalidIdError("user"));
        }

        _logger.LogDebug("User preview requested for user {UserId}", userIdParsed);

        var preview = await _previewService.GetUserPreviewAsync(userIdParsed, null, cancellationToken);
        if (preview is null)
        {
            return NotFound(NotFoundError("user", userIdParsed));
        }

        return Ok(preview);
    }

    /// <summary>
    /// Gets preview data for a user with guild context.
    /// </summary>
    /// <param name="userId">The Discord user ID as a string.</param>
    /// <param name="guildId">The guild ID for context as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User preview data with guild-specific information.</returns>
    [HttpGet("users/{userId}/guild/{guildId}")]
    [ProducesResponseType(typeof(UserPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPreviewDto>> GetUserPreviewWithGuild(
        string userId,
        string guildId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(userId, out var userIdParsed))
        {
            return BadRequest(InvalidIdError("user"));
        }

        if (!ulong.TryParse(guildId, out var guildIdParsed))
        {
            return BadRequest(InvalidIdError("guild"));
        }

        _logger.LogDebug("User preview requested for user {UserId} in guild {GuildId}", userIdParsed, guildIdParsed);

        var preview = await _previewService.GetUserPreviewAsync(userIdParsed, guildIdParsed, cancellationToken);
        if (preview is null)
        {
            return NotFound(NotFoundError("user", userIdParsed));
        }

        return Ok(preview);
    }

    /// <summary>
    /// Gets preview data for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guild preview data.</returns>
    [HttpGet("guilds/{guildId}")]
    [ProducesResponseType(typeof(GuildPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuildPreviewDto>> GetGuildPreview(
        string guildId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(guildId, out var guildIdParsed))
        {
            return BadRequest(InvalidIdError("guild"));
        }

        _logger.LogDebug("Guild preview requested for guild {GuildId}", guildIdParsed);

        var preview = await _previewService.GetGuildPreviewAsync(guildIdParsed, cancellationToken);
        if (preview is null)
        {
            return NotFound(NotFoundError("guild", guildIdParsed));
        }

        return Ok(preview);
    }

    private ApiErrorDto InvalidIdError(string kind) => new()
    {
        Message = $"Invalid {kind} ID format",
        Detail = $"{char.ToUpperInvariant(kind[0])}{kind[1..]} ID must be a valid Discord snowflake ID.",
        StatusCode = StatusCodes.Status400BadRequest,
        TraceId = HttpContext.GetCorrelationId()
    };

    private ApiErrorDto NotFoundError(string kind, ulong id) => new()
    {
        Message = $"{char.ToUpperInvariant(kind[0])}{kind[1..]} not found",
        Detail = $"No {kind} with ID {id} found in Discord cache.",
        StatusCode = StatusCodes.Status404NotFound,
        TraceId = HttpContext.GetCorrelationId()
    };
}
