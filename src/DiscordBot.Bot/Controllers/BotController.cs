using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for bot status and control operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BotController : ControllerBase
{
    private readonly IBotService _botService;
    private readonly ILogger<BotController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotController"/> class.
    /// </summary>
    /// <param name="botService">The bot service.</param>
    /// <param name="logger">The logger.</param>
    public BotController(IBotService botService, ILogger<BotController> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>Bot status information including uptime, guild count, and latency.</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(BotStatusDto), StatusCodes.Status200OK)]
    public ActionResult<BotStatusDto> GetStatus()
    {
        _logger.LogDebug("Bot status requested");

        var status = _botService.GetStatus();

        _logger.LogTrace("Bot status retrieved: {ConnectionState}, {GuildCount} guilds",
            status.ConnectionState, status.GuildCount);

        return Ok(status);
    }

    /// <summary>
    /// Gets the list of guilds the bot is currently connected to.
    /// </summary>
    /// <returns>A list of guild information.</returns>
    [HttpGet("guilds")]
    [ProducesResponseType(typeof(IReadOnlyList<GuildInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<GuildInfoDto>> GetConnectedGuilds()
    {
        _logger.LogDebug("Connected guilds list requested");

        var guilds = _botService.GetConnectedGuilds();

        _logger.LogTrace("Retrieved {Count} connected guilds", guilds.Count);

        return Ok(guilds);
    }

    /// <summary>
    /// Restarts the bot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Restart(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Bot restart requested via API");

        try
        {
            await _botService.RestartAsync(cancellationToken);
            return Accepted();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Restart operation not supported");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Restart operation is not supported",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Shuts down the bot gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
    [HttpPost("shutdown")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Shutdown(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Bot shutdown requested via API");

        await _botService.ShutdownAsync(cancellationToken);

        return Accepted(new { Message = "Shutdown initiated" });
    }
}
