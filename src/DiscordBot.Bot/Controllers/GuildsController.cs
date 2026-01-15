using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for guild operations and settings management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class GuildsController : ControllerBase
{
    private readonly IGuildService _guildService;
    private readonly ILogger<GuildsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildsController"/> class.
    /// </summary>
    /// <param name="guildService">The guild service.</param>
    /// <param name="logger">The logger.</param>
    public GuildsController(IGuildService guildService, ILogger<GuildsController> logger)
    {
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all guilds with merged database and Discord data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all guilds.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GuildDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuildDto>>> GetAllGuilds(CancellationToken cancellationToken)
    {
        _logger.LogDebug("All guilds list requested");

        var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        _logger.LogTrace("Retrieved {Count} guilds", guilds.Count);

        return Ok(guilds);
    }

    /// <summary>
    /// Gets a specific guild by ID.
    /// </summary>
    /// <param name="id">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild data if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GuildDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuildDto>> GetGuildById(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Guild {GuildId} requested", id);

        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);

        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {id} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Guild {GuildId} retrieved: {GuildName}", id, guild.Name);

        return Ok(guild);
    }

    /// <summary>
    /// Updates guild settings.
    /// </summary>
    /// <param name="id">The guild's Discord snowflake ID.</param>
    /// <param name="request">The update request containing fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guild data.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GuildDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuildDto>> UpdateGuild(
        ulong id,
        [FromBody] GuildUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Guild {GuildId} update requested", id);

        if (request == null)
        {
            _logger.LogWarning("Invalid guild update request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var guild = await _guildService.UpdateGuildAsync(id, request, cancellationToken);

        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found for update", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {id} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Guild {GuildId} updated successfully", id);

        return Ok(guild);
    }

    /// <summary>
    /// Synchronizes guild data from Discord to the database.
    /// </summary>
    /// <param name="id">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("{id}/sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncGuild(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Guild {GuildId} sync requested", id);

        var success = await _guildService.SyncGuildAsync(id, cancellationToken);

        if (!success)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord for sync", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {id} is connected to the bot.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Guild {GuildId} synced successfully", id);

        return Ok(new { Message = "Guild synced successfully", GuildId = id });
    }
}
