using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for watchlist operations and management.
/// Provides endpoints for adding, removing, and viewing users on the moderation watchlist.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/watchlist")]
[Authorize(Policy = "RequireAdmin")]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;
    private readonly ILogger<WatchlistController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchlistController"/> class.
    /// </summary>
    /// <param name="watchlistService">The watchlist service.</param>
    /// <param name="logger">The logger.</param>
    public WatchlistController(
        IWatchlistService watchlistService,
        ILogger<WatchlistController> logger)
    {
        _watchlistService = watchlistService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all watchlist entries for a guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of watchlist entries for the guild.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<WatchlistEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<WatchlistEntryDto>>> GetWatchlist(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Watchlist requested for guild {GuildId}, page {Page}, pageSize {PageSize}",
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

        var (items, totalCount) = await _watchlistService.GetWatchlistAsync(
            guildId,
            page,
            pageSize,
            cancellationToken);

        var response = new PaginatedResponseDto<WatchlistEntryDto>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} of {Total} watchlist entries for guild {GuildId}",
            items.Count(), totalCount, guildId);

        return Ok(response);
    }

    /// <summary>
    /// Adds a user to the watchlist.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The watchlist add request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created watchlist entry data.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(WatchlistEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WatchlistEntryDto>> AddToWatchlist(
        ulong guildId,
        [FromBody] WatchlistAddDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Watchlist add requested for guild {GuildId}: user {UserId} by moderator {ModeratorId}",
            guildId, request.UserId, request.AddedByUserId);

        if (request == null)
        {
            _logger.LogWarning("Invalid watchlist add request: request body is null");

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
            var entry = await _watchlistService.AddToWatchlistAsync(
                guildId,
                request.UserId,
                request.Reason,
                request.AddedByUserId,
                cancellationToken);

            _logger.LogInformation("User {UserId} added to watchlist for guild {GuildId} by moderator {ModeratorId}",
                request.UserId, guildId, request.AddedByUserId);

            return CreatedAtAction(
                nameof(GetWatchlist),
                new { guildId },
                entry);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid watchlist add request for guild {GuildId}", guildId);

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
    /// Removes a user from the watchlist.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="userId">The user's Discord snowflake ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromWatchlist(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Watchlist removal requested for user {UserId} in guild {GuildId}", userId, guildId);

        var success = await _watchlistService.RemoveFromWatchlistAsync(guildId, userId, cancellationToken);

        if (!success)
        {
            _logger.LogWarning("User {UserId} not found on watchlist for guild {GuildId}", userId, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Watchlist entry not found",
                Detail = $"User {userId} is not on the watchlist for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("User {UserId} removed from watchlist for guild {GuildId}", userId, guildId);

        return NoContent();
    }
}
