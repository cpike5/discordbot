using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for command log retrieval and statistics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommandLogsController : ControllerBase
{
    private readonly ICommandLogService _commandLogService;
    private readonly ILogger<CommandLogsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLogsController"/> class.
    /// </summary>
    /// <param name="commandLogService">The command log service.</param>
    /// <param name="logger">The logger.</param>
    public CommandLogsController(
        ICommandLogService commandLogService,
        ILogger<CommandLogsController> logger)
    {
        _commandLogService = commandLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets command logs with optional filtering and pagination.
    /// </summary>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="commandName">Optional command name filter.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="successOnly">Optional success-only filter.</param>
    /// <param name="page">Page number (1-based, default: 1).</param>
    /// <param name="pageSize">Page size (default: 50, max: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of command logs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<CommandLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<CommandLogDto>>> GetLogs(
        [FromQuery] ulong? guildId = null,
        [FromQuery] ulong? userId = null,
        [FromQuery] string? commandName = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] bool? successOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Command logs requested with filters: GuildId={GuildId}, UserId={UserId}, CommandName={CommandName}, Page={Page}",
            guildId, userId, commandName, page);

        // Validate date range
        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
        {
            _logger.LogWarning("Invalid date range: StartDate={StartDate} > EndDate={EndDate}", startDate, endDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var query = new CommandLogQueryDto
        {
            GuildId = guildId,
            UserId = userId,
            CommandName = commandName,
            StartDate = startDate,
            EndDate = endDate,
            SuccessOnly = successOnly,
            Page = page,
            PageSize = pageSize
        };

        var result = await _commandLogService.GetLogsAsync(query, cancellationToken);

        _logger.LogTrace("Retrieved {Count} command logs (Page {Page}/{TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        return Ok(result);
    }

    /// <summary>
    /// Gets command usage statistics.
    /// </summary>
    /// <param name="since">Optional start date for statistics. If not provided, returns all-time statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping command names to their usage counts.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IDictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IDictionary<string, int>>> GetCommandStats(
        [FromQuery] DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Command statistics requested since {Since}", since);

        var stats = await _commandLogService.GetCommandStatsAsync(since, cancellationToken);

        _logger.LogTrace("Retrieved statistics for {Count} commands", stats.Count);

        return Ok(stats);
    }
}
