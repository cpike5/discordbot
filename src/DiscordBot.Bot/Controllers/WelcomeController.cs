using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for welcome configuration management and message preview.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class WelcomeController : ControllerBase
{
    private readonly IWelcomeService _welcomeService;
    private readonly ILogger<WelcomeController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WelcomeController"/> class.
    /// </summary>
    /// <param name="welcomeService">The welcome service.</param>
    /// <param name="logger">The logger.</param>
    public WelcomeController(IWelcomeService welcomeService, ILogger<WelcomeController> logger)
    {
        _welcomeService = welcomeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the welcome configuration for a specific guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The welcome configuration if found.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(WelcomeConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WelcomeConfigurationDto>> GetConfiguration(
        ulong guildId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Welcome configuration requested for guild {GuildId}", guildId);

        var configuration = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);

        if (configuration == null)
        {
            _logger.LogWarning("Welcome configuration not found for guild {GuildId}", guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Welcome configuration not found",
                Detail = $"No welcome configuration exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Welcome configuration retrieved for guild {GuildId}", guildId);

        return Ok(configuration);
    }

    /// <summary>
    /// Updates the welcome configuration for a specific guild.
    /// Creates a new configuration if one doesn't exist.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The update request containing fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated welcome configuration.</returns>
    [HttpPut]
    [ProducesResponseType(typeof(WelcomeConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WelcomeConfigurationDto>> UpdateConfiguration(
        ulong guildId,
        [FromBody] WelcomeConfigurationUpdateDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Welcome configuration update requested for guild {GuildId}", guildId);

        if (request == null)
        {
            _logger.LogWarning("Invalid welcome configuration update request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var configuration = await _welcomeService.UpdateConfigurationAsync(guildId, request, cancellationToken);

        if (configuration == null)
        {
            _logger.LogWarning("Guild {GuildId} not found for welcome configuration update", guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {guildId} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Welcome configuration updated successfully for guild {GuildId}", guildId);

        return Ok(configuration);
    }

    /// <summary>
    /// Generates a preview of the welcome message for a specific guild.
    /// Useful for testing message templates before enabling welcome messages.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The preview request containing the user ID to use for preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview message string with template variables replaced.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewMessage(
        ulong guildId,
        [FromBody] WelcomePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Welcome message preview requested for guild {GuildId} with user {UserId}",
            guildId, request?.PreviewUserId);

        if (request == null)
        {
            _logger.LogWarning("Invalid welcome preview request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (request.PreviewUserId == 0)
        {
            _logger.LogWarning("Invalid welcome preview request: PreviewUserId is required");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "PreviewUserId must be a valid Discord user ID.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var previewMessage = await _welcomeService.PreviewWelcomeMessageAsync(
            guildId,
            request.PreviewUserId,
            cancellationToken);

        if (previewMessage == null)
        {
            _logger.LogWarning("Welcome configuration not found for guild {GuildId} during preview", guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Welcome configuration not found",
                Detail = $"No welcome configuration exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Welcome message preview generated for guild {GuildId}", guildId);

        return Ok(new { Message = previewMessage });
    }
}
