using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for user-specific moderation operations.
/// Provides endpoints for viewing and managing moderation data for individual users.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/users/{userId}")]
[Authorize(Policy = "RequireAdmin")]
public class UserModerationController : ControllerBase
{
    private readonly IModerationService _moderationService;
    private readonly IModNoteService _modNoteService;
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly IModTagService _modTagService;
    private readonly ILogger<UserModerationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserModerationController"/> class.
    /// </summary>
    /// <param name="moderationService">The moderation service.</param>
    /// <param name="modNoteService">The mod note service.</param>
    /// <param name="flaggedEventService">The flagged event service.</param>
    /// <param name="modTagService">The mod tag service.</param>
    /// <param name="logger">The logger.</param>
    public UserModerationController(
        IModerationService moderationService,
        IModNoteService modNoteService,
        IFlaggedEventService flaggedEventService,
        IModTagService modTagService,
        ILogger<UserModerationController> logger)
    {
        _moderationService = moderationService;
        _modNoteService = modNoteService;
        _flaggedEventService = flaggedEventService;
        _modTagService = modTagService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all moderation cases for a specific user with pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID.</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of moderation cases for the user.</returns>
    [HttpGet("cases")]
    [ProducesResponseType(typeof(PaginatedResponseDto<ModerationCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<ModerationCaseDto>>> GetUserCases(
        ulong guildId,
        ulong userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("User cases requested for user {UserId} in guild {GuildId}, page {Page}, pageSize {PageSize}",
            userId, guildId, page, pageSize);

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

        var (items, totalCount) = await _moderationService.GetUserCasesAsync(
            guildId,
            userId,
            page,
            pageSize,
            cancellationToken);

        var response = new PaginatedResponseDto<ModerationCaseDto>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} cases for user {UserId} in guild {GuildId}",
            items.Count(), totalCount, userId, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Gets all moderator notes for a specific user.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of moderator notes for the user.</returns>
    [HttpGet("notes")]
    [ProducesResponseType(typeof(IEnumerable<ModNoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ModNoteDto>>> GetUserNotes(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("User notes requested for user {UserId} in guild {GuildId}", userId, guildId);

        var notes = await _modNoteService.GetNotesAsync(guildId, userId, cancellationToken);

        _logger.LogTrace("Retrieved {Count} notes for user {UserId} in guild {GuildId}",
            notes.Count(), userId, guildId);

        return Ok(notes);
    }

    /// <summary>
    /// Creates a new moderator note for a user.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID.</param>
    /// <param name="request">The note creation request containing content and author ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created moderator note data.</returns>
    [HttpPost("notes")]
    [ProducesResponseType(typeof(ModNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModNoteDto>> CreateUserNote(
        ulong guildId,
        ulong userId,
        [FromBody] ModNoteCreateDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mod note creation requested for user {UserId} in guild {GuildId} by author {AuthorId}",
            userId, guildId, request.AuthorUserId);

        if (request == null)
        {
            _logger.LogWarning("Invalid mod note creation request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            _logger.LogWarning("Invalid mod note creation request: content is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Note content is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            var note = await _modNoteService.AddNoteAsync(
                guildId,
                userId,
                request.Content,
                request.AuthorUserId,
                cancellationToken);

            _logger.LogInformation("Mod note {NoteId} created successfully for user {UserId} by author {AuthorId}",
                note.Id, userId, request.AuthorUserId);

            return CreatedAtAction(
                nameof(GetUserNotes),
                new { guildId, userId },
                note);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid mod note creation request for user {UserId} in guild {GuildId}",
                userId, guildId);

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
    /// Gets all flagged events for a specific user.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of flagged events for the user.</returns>
    [HttpGet("flags")]
    [ProducesResponseType(typeof(IEnumerable<FlaggedEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FlaggedEventDto>>> GetUserFlags(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("User flagged events requested for user {UserId} in guild {GuildId}", userId, guildId);

        var events = await _flaggedEventService.GetUserEventsAsync(guildId, userId, cancellationToken);

        _logger.LogTrace("Retrieved {Count} flagged events for user {UserId} in guild {GuildId}",
            events.Count(), userId, guildId);

        return Ok(events);
    }

    /// <summary>
    /// Gets all tags applied to a specific user.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of tags applied to the user.</returns>
    [HttpGet("tags")]
    [ProducesResponseType(typeof(IEnumerable<UserModTagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserModTagDto>>> GetUserTags(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("User tags requested for user {UserId} in guild {GuildId}", userId, guildId);

        var tags = await _modTagService.GetUserTagsAsync(guildId, userId, cancellationToken);

        _logger.LogTrace("Retrieved {Count} tags for user {UserId} in guild {GuildId}",
            tags.Count(), userId, guildId);

        return Ok(tags);
    }
}
