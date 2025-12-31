using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for guild member management and querying.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/members")]
[Authorize(Policy = "RequireAdmin")]
public class GuildMembersController : ControllerBase
{
    private readonly IGuildMemberService _guildMemberService;
    private readonly ILogger<GuildMembersController> _logger;

    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildMembersController"/> class.
    /// </summary>
    /// <param name="guildMemberService">The guild member service.</param>
    /// <param name="logger">The logger.</param>
    public GuildMembersController(
        IGuildMemberService guildMemberService,
        ILogger<GuildMembersController> logger)
    {
        _guildMemberService = guildMemberService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of guild members with filtering, searching, and sorting.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="query">Query parameters for filtering, searching, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of guild members.</returns>
    /// <response code="200">Returns the paginated list of guild members.</response>
    /// <response code="400">Invalid query parameters provided.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<GuildMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<GuildMemberDto>>> GetMembers(
        ulong guildId,
        [FromQuery] GuildMemberQueryDto query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Members list requested for guild {GuildId} with filters: {SearchTerm}, Page {Page}, PageSize {PageSize}",
            guildId, query.SearchTerm ?? "(none)", query.Page, query.PageSize);

        // Validate pagination parameters
        if (query.Page < 1)
        {
            _logger.LogWarning("Invalid page number {Page} for guild {GuildId}", query.Page, guildId);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page number",
                Detail = "Page number must be greater than or equal to 1.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            _logger.LogWarning("Invalid page size {PageSize} for guild {GuildId}", query.PageSize, guildId);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid page size",
                Detail = $"Page size must be between 1 and {MaxPageSize}.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var result = await _guildMemberService.GetMembersAsync(guildId, query, cancellationToken);

        _logger.LogTrace("Retrieved {Count} members out of {Total} for guild {GuildId}",
            result.Items.Count, result.TotalCount, guildId);

        return Ok(result);
    }

    /// <summary>
    /// Gets a specific guild member by user ID.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild member data if found.</returns>
    /// <response code="200">Returns the guild member data.</response>
    /// <response code="404">Member not found in the specified guild.</response>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(GuildMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuildMemberDto>> GetMemberById(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Member {UserId} requested for guild {GuildId}", userId, guildId);

        var member = await _guildMemberService.GetMemberAsync(guildId, userId, cancellationToken);

        if (member == null)
        {
            _logger.LogWarning("Member {UserId} not found in guild {GuildId}", userId, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Member not found",
                Detail = $"No member with user ID {userId} exists in guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Member {UserId} retrieved for guild {GuildId}: {Username}",
            userId, guildId, member.Username);

        return Ok(member);
    }

    /// <summary>
    /// Exports guild members to a CSV file with optional filtering.
    /// Limited to 10,000 rows maximum.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="query">Query parameters for filtering and searching (pagination ignored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A CSV file containing the filtered member data.</returns>
    /// <response code="200">Returns the CSV file as a download.</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMembers(
        ulong guildId,
        [FromQuery] GuildMemberQueryDto query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Member export requested for guild {GuildId} with filters: {SearchTerm}",
            guildId, query.SearchTerm ?? "(none)");

        var csvData = await _guildMemberService.ExportMembersToCsvAsync(guildId, query, cancellationToken: cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"members-{guildId}-{timestamp}.csv";

        _logger.LogInformation("Member export completed for guild {GuildId}, file size: {SizeBytes} bytes",
            guildId, csvData.Length);

        return File(csvData, "text/csv", fileName);
    }
}
