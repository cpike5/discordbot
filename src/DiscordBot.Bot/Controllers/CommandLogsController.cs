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
    private readonly ICommandAnalyticsService _analyticsService;
    private readonly ILogger<CommandLogsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLogsController"/> class.
    /// </summary>
    /// <param name="commandLogService">The command log service.</param>
    /// <param name="analyticsService">The analytics service.</param>
    /// <param name="logger">The logger.</param>
    public CommandLogsController(
        ICommandLogService commandLogService,
        ICommandAnalyticsService analyticsService,
        ILogger<CommandLogsController> logger)
    {
        _commandLogService = commandLogService;
        _analyticsService = analyticsService;
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

    /// <summary>
    /// Gets comprehensive analytics data for the dashboard.
    /// </summary>
    /// <param name="start">Optional start date for analytics period. Defaults to 30 days ago.</param>
    /// <param name="end">Optional end date for analytics period. Defaults to now.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive analytics data including usage over time, success rates, and performance metrics.</returns>
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(CommandAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommandAnalyticsDto>> GetAnalytics(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-30);
        var endDate = end ?? DateTime.UtcNow;

        _logger.LogDebug("Analytics requested from {StartDate} to {EndDate} for guild {GuildId}",
            startDate, endDate, guildId);

        var analytics = await _analyticsService.GetAnalyticsAsync(startDate, endDate, guildId, cancellationToken);

        _logger.LogTrace("Retrieved analytics with {TotalCommands} total commands, {SuccessRate:F2}% success rate",
            analytics.TotalCommands, analytics.SuccessRate);

        return Ok(analytics);
    }

    /// <summary>
    /// Gets command usage over time.
    /// </summary>
    /// <param name="start">Start date for the period.</param>
    /// <param name="end">End date for the period.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily usage data points.</returns>
    [HttpGet("analytics/usage-over-time")]
    [ProducesResponseType(typeof(IEnumerable<UsageOverTimeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UsageOverTimeDto>>> GetUsageOverTime(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Usage over time requested from {StartDate} to {EndDate} for guild {GuildId}",
            start, end, guildId);

        var usageData = await _analyticsService.GetUsageOverTimeAsync(start, end, guildId, cancellationToken);

        _logger.LogTrace("Retrieved {DataPointCount} usage over time data points", usageData.Count);

        return Ok(usageData);
    }

    /// <summary>
    /// Gets success/failure rate statistics.
    /// </summary>
    /// <param name="since">Optional start date. If not provided, returns all-time statistics.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success rate statistics including success count, failure count, and percentage.</returns>
    [HttpGet("analytics/success-rate")]
    [ProducesResponseType(typeof(CommandSuccessRateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommandSuccessRateDto>> GetSuccessRate(
        [FromQuery] DateTime? since = null,
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Success rate requested since {Since} for guild {GuildId}", since, guildId);

        var successRate = await _analyticsService.GetSuccessRateAsync(since, guildId, cancellationToken);

        _logger.LogTrace("Retrieved success rate: {SuccessCount} successful, {FailureCount} failed, {SuccessRate:F2}%",
            successRate.SuccessCount, successRate.FailureCount, successRate.SuccessRate);

        return Ok(successRate);
    }

    /// <summary>
    /// Gets response time performance metrics.
    /// </summary>
    /// <param name="since">Optional start date. If not provided, returns all-time statistics.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="limit">Maximum number of commands to return. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of command performance metrics including average, min, and max response times.</returns>
    [HttpGet("analytics/performance")]
    [ProducesResponseType(typeof(IEnumerable<CommandPerformanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CommandPerformanceDto>>> GetPerformance(
        [FromQuery] DateTime? since = null,
        [FromQuery] ulong? guildId = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Performance metrics requested since {Since} for guild {GuildId}, limit {Limit}",
            since, guildId, limit);

        var performance = await _analyticsService.GetCommandPerformanceAsync(since, guildId, limit, cancellationToken);

        _logger.LogTrace("Retrieved performance metrics for {CommandCount} commands", performance.Count);

        return Ok(performance);
    }
}
