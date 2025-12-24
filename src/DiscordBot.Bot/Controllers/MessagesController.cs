using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for message log retrieval, statistics, and data management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class MessagesController : ControllerBase
{
    private readonly IMessageLogService _messageLogService;
    private readonly ILogger<MessagesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesController"/> class.
    /// </summary>
    /// <param name="messageLogService">The message log service.</param>
    /// <param name="logger">The logger.</param>
    public MessagesController(
        IMessageLogService messageLogService,
        ILogger<MessagesController> logger)
    {
        _messageLogService = messageLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets paginated message logs with optional filters.
    /// </summary>
    /// <param name="query">The query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of message logs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<MessageLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<MessageLogDto>>> GetMessages(
        [FromQuery] MessageLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Message logs requested with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}, Source={Source}, Page={Page}",
            query.AuthorId, query.GuildId, query.ChannelId, query.Source, query.Page);

        // Validate date range
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate.Value > query.EndDate.Value)
        {
            _logger.LogWarning("Invalid date range: StartDate={StartDate} > EndDate={EndDate}", query.StartDate, query.EndDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var result = await _messageLogService.GetLogsAsync(query, cancellationToken);

        _logger.LogTrace("Retrieved {Count} message logs (Page {Page}/{TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        return Ok(result);
    }

    /// <summary>
    /// Gets a single message log entry by its identifier.
    /// </summary>
    /// <param name="id">The message log identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message log data if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MessageLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageLogDto>> GetMessageById(long id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Message log {MessageId} requested", id);

        var message = await _messageLogService.GetByIdAsync(id, cancellationToken);

        if (message == null)
        {
            _logger.LogWarning("Message log {MessageId} not found", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Message not found",
                Detail = $"No message log with ID {id} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Message log {MessageId} retrieved for author {AuthorId}", id, message.AuthorId);

        return Ok(message);
    }

    /// <summary>
    /// Gets comprehensive message statistics including counts, breakdowns, and trends.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter statistics. If null, returns global statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message statistics including total counts, source breakdown, and daily trends.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(MessageLogStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageLogStatsDto>> GetStats(
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Message statistics requested for guild {GuildId}", guildId);

        var stats = await _messageLogService.GetStatsAsync(guildId, cancellationToken);

        _logger.LogTrace("Retrieved message statistics: {TotalMessages} total, {UniqueAuthors} unique authors",
            stats.TotalMessages, stats.UniqueAuthors);

        return Ok(stats);
    }

    /// <summary>
    /// Deletes all message logs for a specific user.
    /// Used for GDPR compliance and user data deletion requests.
    /// </summary>
    /// <param name="userId">The Discord user ID whose messages should be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of message logs deleted.</returns>
    [HttpDelete("user/{userId}")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteUserMessages(ulong userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GDPR deletion requested for user {UserId}", userId);

        var deletedCount = await _messageLogService.DeleteUserMessagesAsync(userId, cancellationToken);

        _logger.LogInformation("Deleted {DeletedCount} message logs for user {UserId}", deletedCount, userId);

        return Ok(new { deletedCount });
    }

    /// <summary>
    /// Manually triggers cleanup of old message logs according to the configured retention policy.
    /// Deletes messages older than the retention period in batches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of message logs deleted.</returns>
    [HttpPost("cleanup")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupOldMessages(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual message cleanup triggered");

        var deletedCount = await _messageLogService.CleanupOldMessagesAsync(cancellationToken);

        _logger.LogInformation("Cleanup completed: {DeletedCount} message logs deleted", deletedCount);

        return Ok(new { deletedCount });
    }

    /// <summary>
    /// Exports message logs matching the query criteria to a CSV file.
    /// </summary>
    /// <param name="query">The query parameters for filtering which messages to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CSV file download containing the filtered message logs.</returns>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportMessages(
        [FromQuery] MessageLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Message export requested with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}",
            query.AuthorId, query.GuildId, query.ChannelId);

        // Validate date range
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate.Value > query.EndDate.Value)
        {
            _logger.LogWarning("Invalid date range for export: StartDate={StartDate} > EndDate={EndDate}", query.StartDate, query.EndDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var csvBytes = await _messageLogService.ExportToCsvAsync(query, cancellationToken);

        _logger.LogInformation("Message export completed: {ByteCount} bytes generated", csvBytes.Length);

        var fileName = $"message-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(csvBytes, "text/csv", fileName);
    }
}
