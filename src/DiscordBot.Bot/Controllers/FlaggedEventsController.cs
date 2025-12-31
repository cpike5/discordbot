using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for flagged event operations and management.
/// Provides endpoints for reviewing, dismissing, and taking action on auto-moderation flagged events.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/flagged-events")]
[Authorize(Policy = "RequireAdmin")]
public class FlaggedEventsController : ControllerBase
{
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly ILogger<FlaggedEventsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlaggedEventsController"/> class.
    /// </summary>
    /// <param name="flaggedEventService">The flagged event service.</param>
    /// <param name="logger">The logger.</param>
    public FlaggedEventsController(
        IFlaggedEventService flaggedEventService,
        ILogger<FlaggedEventsController> logger)
    {
        _flaggedEventService = flaggedEventService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all pending flagged events for a guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of pending flagged events for the guild.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<FlaggedEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<FlaggedEventDto>>> GetPendingEvents(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Pending flagged events list requested for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, page, pageSize);

        if (page < 1)
        {
            _logger.LogWarning("Invalid page number: {Page}", page);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page number",
                Detail = "Page number must be greater than or equal to 1.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            _logger.LogWarning("Invalid page size: {PageSize}", pageSize);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page size",
                Detail = "Page size must be between 1 and 100.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var (items, totalCount) = await _flaggedEventService.GetPendingEventsAsync(
            guildId,
            page,
            pageSize,
            cancellationToken);

        var response = new PaginatedResponseDto<FlaggedEventDto>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} pending flagged events for guild {GuildId}",
            items.Count(), totalCount, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets filtered flagged events for a guild with advanced filtering and pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="query">The query parameters containing filters and pagination settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of filtered flagged events for the guild.</returns>
    [HttpGet("filter")]
    [ProducesResponseType(typeof(PaginatedResponseDto<FlaggedEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<FlaggedEventDto>>> GetFilteredEvents(
        ulong guildId,
        [FromQuery] FlaggedEventQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Filtered flagged events list requested for guild {GuildId}, filters: RuleType={RuleType}, Severity={Severity}, Status={Status}, page {Page}",
            guildId, query.RuleType, query.Severity, query.Status, query.Page);

        if (query.Page < 1)
        {
            _logger.LogWarning("Invalid page number: {Page}", query.Page);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page number",
                Detail = "Page number must be greater than or equal to 1.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            _logger.LogWarning("Invalid page size: {PageSize}", query.PageSize);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page size",
                Detail = "Page size must be between 1 and 100.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var (items, totalCount) = await _flaggedEventService.GetFilteredEventsAsync(
            guildId,
            query,
            cancellationToken);

        var response = new PaginatedResponseDto<FlaggedEventDto>
        {
            Items = items.ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} filtered flagged events for guild {GuildId}",
            items.Count(), totalCount, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific flagged event by ID.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The flagged event's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The flagged event data if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FlaggedEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlaggedEventDto>> GetEventById(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Flagged event {EventId} requested for guild {GuildId}", id, guildId);

        var flaggedEvent = await _flaggedEventService.GetEventAsync(id, cancellationToken);

        if (flaggedEvent == null || flaggedEvent.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for guild {GuildId}", id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Flagged event {EventId} retrieved: {Description}", id, flaggedEvent.Description);

        return Ok(flaggedEvent);
    }

    /// <summary>
    /// Dismisses a flagged event (marks as not requiring action).
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The flagged event's unique identifier.</param>
    /// <param name="request">The dismiss request containing the reviewer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated flagged event data.</returns>
    [HttpPost("{id}/dismiss")]
    [ProducesResponseType(typeof(FlaggedEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FlaggedEventDto>> DismissEvent(
        ulong guildId,
        Guid id,
        [FromBody] FlaggedEventReviewDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flagged event {EventId} dismiss requested for guild {GuildId} by reviewer {ReviewerId}",
            id, guildId, request.ReviewerId);

        if (request == null)
        {
            _logger.LogWarning("Invalid dismiss request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Verify the event belongs to the guild
        var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for dismiss in guild {GuildId}", id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var flaggedEvent = await _flaggedEventService.DismissEventAsync(id, request.ReviewerId, cancellationToken);

        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found for dismiss", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Flagged event {EventId} dismissed successfully by reviewer {ReviewerId}",
            id, request.ReviewerId);

        return Ok(flaggedEvent);
    }

    /// <summary>
    /// Acknowledges a flagged event (marks as seen but not yet actioned).
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The flagged event's unique identifier.</param>
    /// <param name="request">The acknowledge request containing the reviewer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated flagged event data.</returns>
    [HttpPost("{id}/acknowledge")]
    [ProducesResponseType(typeof(FlaggedEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FlaggedEventDto>> AcknowledgeEvent(
        ulong guildId,
        Guid id,
        [FromBody] FlaggedEventReviewDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flagged event {EventId} acknowledge requested for guild {GuildId} by reviewer {ReviewerId}",
            id, guildId, request.ReviewerId);

        if (request == null)
        {
            _logger.LogWarning("Invalid acknowledge request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Verify the event belongs to the guild
        var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for acknowledge in guild {GuildId}", id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var flaggedEvent = await _flaggedEventService.AcknowledgeEventAsync(id, request.ReviewerId, cancellationToken);

        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found for acknowledge", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Flagged event {EventId} acknowledged successfully by reviewer {ReviewerId}",
            id, request.ReviewerId);

        return Ok(flaggedEvent);
    }

    /// <summary>
    /// Takes action on a flagged event (marks as actioned and records the action taken).
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The flagged event's unique identifier.</param>
    /// <param name="request">The action request containing the action description and reviewer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated flagged event data.</returns>
    [HttpPost("{id}/action")]
    [ProducesResponseType(typeof(FlaggedEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FlaggedEventDto>> TakeAction(
        ulong guildId,
        Guid id,
        [FromBody] FlaggedEventTakeActionDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flagged event {EventId} action requested for guild {GuildId} by reviewer {ReviewerId}: {Action}",
            id, guildId, request.ReviewerId, request.Action);

        if (request == null)
        {
            _logger.LogWarning("Invalid action request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            _logger.LogWarning("Invalid action request: action is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Action description is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Verify the event belongs to the guild
        var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for action in guild {GuildId}", id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var flaggedEvent = await _flaggedEventService.TakeActionAsync(
            id,
            request.Action,
            request.ReviewerId,
            cancellationToken);

        if (flaggedEvent == null)
        {
            _logger.LogWarning("Flagged event {EventId} not found for action", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Flagged event not found",
                Detail = $"No flagged event with ID {id} exists.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Flagged event {EventId} actioned successfully by reviewer {ReviewerId}: {Action}",
            id, request.ReviewerId, request.Action);

        return Ok(flaggedEvent);
    }
}
