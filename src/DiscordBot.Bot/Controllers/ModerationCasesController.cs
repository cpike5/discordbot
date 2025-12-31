using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for moderation case operations and management.
/// Provides endpoints for creating, querying, and updating moderation cases.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/cases")]
[Authorize(Policy = "RequireAdmin")]
public class ModerationCasesController : ControllerBase
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModerationCasesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationCasesController"/> class.
    /// </summary>
    /// <param name="moderationService">The moderation service.</param>
    /// <param name="logger">The logger.</param>
    public ModerationCasesController(
        IModerationService moderationService,
        ILogger<ModerationCasesController> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets moderation cases for a guild with optional filters and pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="type">Optional case type filter.</param>
    /// <param name="targetUserId">Optional target user ID filter.</param>
    /// <param name="moderatorUserId">Optional moderator user ID filter.</param>
    /// <param name="startDate">Optional start date filter (UTC).</param>
    /// <param name="endDate">Optional end date filter (UTC).</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of moderation cases matching the filters.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<ModerationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<ModerationCaseDto>>> GetCases(
        ulong guildId,
        [FromQuery] CaseType? type = null,
        [FromQuery] ulong? targetUserId = null,
        [FromQuery] ulong? moderatorUserId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Moderation cases list requested for guild {GuildId}, page {Page}, pageSize {PageSize}",
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

        var query = new ModerationCaseQueryDto
        {
            GuildId = guildId,
            Type = type,
            TargetUserId = targetUserId,
            ModeratorUserId = moderatorUserId,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _moderationService.GetCasesAsync(query, cancellationToken);

        var response = new PaginatedResponseDto<ModerationCaseDto>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} moderation cases for guild {GuildId}",
            items.Count(), totalCount, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific moderation case by its GUID ID.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="caseId">The case's unique GUID identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The moderation case data if found.</returns>
    [HttpGet("{caseId:guid}")]
    [ProducesResponseType(typeof(ModerationCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationCaseDto>> GetCaseById(
        ulong guildId,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Moderation case {CaseId} requested for guild {GuildId}", caseId, guildId);

        var moderationCase = await _moderationService.GetCaseAsync(caseId, cancellationToken);

        if (moderationCase == null || moderationCase.GuildId != guildId)
        {
            _logger.LogWarning("Moderation case {CaseId} not found for guild {GuildId}", caseId, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Moderation case not found",
                Detail = $"No moderation case with ID {caseId} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Moderation case {CaseId} retrieved: #{CaseNumber}", caseId, moderationCase.CaseNumber);

        return Ok(moderationCase);
    }

    /// <summary>
    /// Gets a specific moderation case by its case number within the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="caseNumber">The sequential case number within the guild.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The moderation case data if found.</returns>
    [HttpGet("number/{caseNumber:long}")]
    [ProducesResponseType(typeof(ModerationCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationCaseDto>> GetCaseByNumber(
        ulong guildId,
        long caseNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Moderation case #{CaseNumber} requested for guild {GuildId}", caseNumber, guildId);

        var moderationCase = await _moderationService.GetCaseByNumberAsync(guildId, caseNumber, cancellationToken);

        if (moderationCase == null)
        {
            _logger.LogWarning("Moderation case #{CaseNumber} not found for guild {GuildId}", caseNumber, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Moderation case not found",
                Detail = $"No moderation case #{caseNumber} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Moderation case #{CaseNumber} retrieved: {CaseId}", caseNumber, moderationCase.Id);

        return Ok(moderationCase);
    }

    /// <summary>
    /// Creates a new moderation case.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The creation request containing the case data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created moderation case data.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ModerationCaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModerationCaseDto>> CreateCase(
        ulong guildId,
        [FromBody] ModerationCaseCreateDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moderation case creation requested for guild {GuildId}", guildId);

        if (request == null)
        {
            _logger.LogWarning("Invalid moderation case creation request: request body is null");

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
            var moderationCase = await _moderationService.CreateCaseAsync(request, cancellationToken);

            _logger.LogInformation("Moderation case {CaseId} (#{CaseNumber}) created successfully for guild {GuildId}",
                moderationCase.Id, moderationCase.CaseNumber, guildId);

            return CreatedAtAction(
                nameof(GetCaseById),
                new { guildId, caseId = moderationCase.Id },
                moderationCase);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid moderation case creation request for guild {GuildId}", guildId);

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
    /// Updates the reason for an existing moderation case.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="caseNumber">The sequential case number within the guild.</param>
    /// <param name="request">The update request containing the new reason and moderator ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated moderation case data.</returns>
    [HttpPatch("number/{caseNumber:long}/reason")]
    [ProducesResponseType(typeof(ModerationCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModerationCaseDto>> UpdateCaseReason(
        ulong guildId,
        long caseNumber,
        [FromBody] CaseReasonUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moderation case #{CaseNumber} reason update requested for guild {GuildId} by moderator {ModeratorId}",
            caseNumber, guildId, request.ModeratorId);

        if (request == null)
        {
            _logger.LogWarning("Invalid case reason update request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            _logger.LogWarning("Invalid case reason update request: reason is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Reason is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var moderationCase = await _moderationService.UpdateCaseReasonAsync(
            guildId,
            caseNumber,
            request.Reason,
            request.ModeratorId,
            cancellationToken);

        if (moderationCase == null)
        {
            _logger.LogWarning("Moderation case #{CaseNumber} not found for update in guild {GuildId}",
                caseNumber, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Moderation case not found",
                Detail = $"No moderation case #{caseNumber} exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Moderation case #{CaseNumber} reason updated successfully by moderator {ModeratorId}",
            caseNumber, request.ModeratorId);

        return Ok(moderationCase);
    }
}
