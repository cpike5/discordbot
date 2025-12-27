using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for scheduled message operations and management.
/// Provides CRUD operations and execution control for scheduled messages within guilds.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/scheduled-messages")]
[Authorize(Policy = "RequireAdmin")]
public class ScheduledMessagesController : ControllerBase
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly ILogger<ScheduledMessagesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledMessagesController"/> class.
    /// </summary>
    /// <param name="scheduledMessageService">The scheduled message service.</param>
    /// <param name="logger">The logger.</param>
    public ScheduledMessagesController(
        IScheduledMessageService scheduledMessageService,
        ILogger<ScheduledMessagesController> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all scheduled messages for a guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of scheduled messages for the guild.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<ScheduledMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<ScheduledMessageDto>>> GetScheduledMessages(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scheduled messages list requested for guild {GuildId}, page {Page}, pageSize {PageSize}",
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

        var (items, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(
            guildId,
            page,
            pageSize,
            cancellationToken);

        var response = new PaginatedResponseDto<ScheduledMessageDto>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} scheduled messages for guild {GuildId}",
            items.Count(), totalCount, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific scheduled message by ID.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The scheduled message's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scheduled message data if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScheduledMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduledMessageDto>> GetScheduledMessageById(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Scheduled message {MessageId} requested for guild {GuildId}", id, guildId);

        var message = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);

        if (message == null || message.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found for guild {GuildId}", id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Scheduled message not found",
                Detail = $"No scheduled message with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Scheduled message {MessageId} retrieved: {Title}", id, message.Title);

        return Ok(message);
    }

    /// <summary>
    /// Creates a new scheduled message.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The creation request containing the scheduled message data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created scheduled message data.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ScheduledMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScheduledMessageDto>> CreateScheduledMessage(
        ulong guildId,
        [FromBody] ScheduledMessageCreateDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled message creation requested for guild {GuildId}", guildId);

        if (request == null)
        {
            _logger.LogWarning("Invalid scheduled message creation request: request body is null");

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
            var message = await _scheduledMessageService.CreateAsync(request, cancellationToken);

            _logger.LogInformation("Scheduled message {MessageId} created successfully for guild {GuildId}",
                message.Id, guildId);

            return CreatedAtAction(
                nameof(GetScheduledMessageById),
                new { guildId, id = message.Id },
                message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid scheduled message creation request for guild {GuildId}", guildId);

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
    /// Updates an existing scheduled message.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The scheduled message's unique identifier.</param>
    /// <param name="request">The update request containing the fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated scheduled message data.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ScheduledMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScheduledMessageDto>> UpdateScheduledMessage(
        ulong guildId,
        Guid id,
        [FromBody] ScheduledMessageUpdateDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled message {MessageId} update requested for guild {GuildId}", id, guildId);

        if (request == null)
        {
            _logger.LogWarning("Invalid scheduled message update request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Verify the message belongs to the guild
        var existing = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found for update in guild {GuildId}",
                id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Scheduled message not found",
                Detail = $"No scheduled message with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            var message = await _scheduledMessageService.UpdateAsync(id, request, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Scheduled message {MessageId} not found for update", id);

                return NotFound(new ApiErrorDto
                {
                    Message = "Scheduled message not found",
                    Detail = $"No scheduled message with ID {id} exists.",
                    StatusCode = StatusCodes.Status404NotFound,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogInformation("Scheduled message {MessageId} updated successfully", id);

            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid scheduled message update request for message {MessageId}", id);

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
    /// Deletes a scheduled message.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The scheduled message's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteScheduledMessage(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled message {MessageId} deletion requested for guild {GuildId}", id, guildId);

        // Verify the message belongs to the guild
        var existing = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found for deletion in guild {GuildId}",
                id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Scheduled message not found",
                Detail = $"No scheduled message with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var success = await _scheduledMessageService.DeleteAsync(id, cancellationToken);

        if (!success)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found for deletion", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Scheduled message not found",
                Detail = $"No scheduled message with ID {id} exists.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Scheduled message {MessageId} deleted successfully", id);

        return NoContent();
    }

    /// <summary>
    /// Executes a scheduled message immediately, regardless of its scheduled time.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The scheduled message's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteScheduledMessage(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled message {MessageId} immediate execution requested for guild {GuildId}",
            id, guildId);

        // Verify the message belongs to the guild
        var existing = await _scheduledMessageService.GetByIdAsync(id, cancellationToken);
        if (existing == null || existing.GuildId != guildId)
        {
            _logger.LogWarning("Scheduled message {MessageId} not found for execution in guild {GuildId}",
                id, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Scheduled message not found",
                Detail = $"No scheduled message with ID {id} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var success = await _scheduledMessageService.ExecuteScheduledMessageAsync(id, cancellationToken);

        if (!success)
        {
            _logger.LogError("Failed to execute scheduled message {MessageId}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Execution failed",
                Detail = $"Failed to execute scheduled message {id}. Check logs for details.",
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Scheduled message {MessageId} executed successfully", id);

        return Ok(new { Message = "Scheduled message executed successfully", MessageId = id });
    }

    /// <summary>
    /// Validates a cron expression for correctness.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID (required for route but not used).</param>
    /// <param name="request">The validation request containing the cron expression.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("validate-cron")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateCronExpression(
        ulong guildId,
        [FromBody] CronValidationRequestDto request)
    {
        _logger.LogDebug("Cron expression validation requested for guild {GuildId}", guildId);

        if (request == null || string.IsNullOrWhiteSpace(request.CronExpression))
        {
            _logger.LogWarning("Invalid cron validation request: cron expression is null or empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Cron expression is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var (isValid, errorMessage) = await _scheduledMessageService.ValidateCronExpressionAsync(request.CronExpression);

        if (isValid)
        {
            _logger.LogDebug("Cron expression validated successfully: {CronExpression}", request.CronExpression);

            return Ok(new
            {
                IsValid = true,
                Message = "Cron expression is valid",
                CronExpression = request.CronExpression
            });
        }

        _logger.LogWarning("Cron expression validation failed: {Error}", errorMessage);

        return BadRequest(new ApiErrorDto
        {
            Message = "Invalid cron expression",
            Detail = errorMessage,
            StatusCode = StatusCodes.Status400BadRequest,
            TraceId = HttpContext.GetCorrelationId()
        });
    }
}

/// <summary>
/// Request DTO for cron expression validation.
/// </summary>
public class CronValidationRequestDto
{
    /// <summary>
    /// Gets or sets the cron expression to validate.
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;
}
