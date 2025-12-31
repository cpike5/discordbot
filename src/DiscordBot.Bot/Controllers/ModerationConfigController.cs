using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for guild moderation configuration operations.
/// Provides endpoints for viewing and updating moderation settings and applying presets.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/moderation-config")]
[Authorize(Policy = "RequireAdmin")]
public class ModerationConfigController : ControllerBase
{
    private readonly IGuildModerationConfigService _configService;
    private readonly ILogger<ModerationConfigController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationConfigController"/> class.
    /// </summary>
    /// <param name="configService">The guild moderation config service.</param>
    /// <param name="logger">The logger.</param>
    public ModerationConfigController(
        IGuildModerationConfigService configService,
        ILogger<ModerationConfigController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the moderation configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild's moderation configuration.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(GuildModerationConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GuildModerationConfigDto>> GetConfig(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Moderation config requested for guild {GuildId}", guildId);

        var config = await _configService.GetConfigAsync(guildId, cancellationToken);

        _logger.LogTrace("Moderation config retrieved for guild {GuildId}: Mode={Mode}",
            guildId, config.Mode);

        return Ok(config);
    }

    /// <summary>
    /// Updates the moderation configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The updated configuration data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guild moderation configuration.</returns>
    [HttpPut]
    [ProducesResponseType(typeof(GuildModerationConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuildModerationConfigDto>> UpdateConfig(
        ulong guildId,
        [FromBody] GuildModerationConfigDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moderation config update requested for guild {GuildId}", guildId);

        if (request == null)
        {
            _logger.LogWarning("Invalid moderation config update request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Ensure the GuildId from the route matches the request (override if needed)
        request.GuildId = guildId;

        try
        {
            var config = await _configService.UpdateConfigAsync(guildId, request, cancellationToken);

            _logger.LogInformation("Moderation config updated successfully for guild {GuildId}: Mode={Mode}",
                guildId, config.Mode);

            return Ok(config);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid moderation config update request for guild {GuildId}", guildId);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Applies a preset configuration to a guild (Relaxed, Moderate, or Strict).
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The preset application request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guild moderation configuration.</returns>
    [HttpPost("preset")]
    [ProducesResponseType(typeof(GuildModerationConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuildModerationConfigDto>> ApplyPreset(
        ulong guildId,
        [FromBody] ApplyPresetDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moderation config preset application requested for guild {GuildId}: {PresetName}",
            guildId, request.PresetName);

        if (request == null)
        {
            _logger.LogWarning("Invalid preset application request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (string.IsNullOrWhiteSpace(request.PresetName))
        {
            _logger.LogWarning("Invalid preset application request: preset name is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Preset name is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            var config = await _configService.ApplyPresetAsync(guildId, request.PresetName, cancellationToken);

            _logger.LogInformation("Moderation config preset {PresetName} applied successfully for guild {GuildId}",
                request.PresetName, guildId);

            return Ok(config);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid preset application request for guild {GuildId}: {PresetName}",
                guildId, request.PresetName);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }
}
