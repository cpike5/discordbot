using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for bulk data purge operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireSuperAdmin")]
public class BulkPurgeController : ControllerBase
{
    private readonly IBulkPurgeService _bulkPurgeService;
    private readonly ILogger<BulkPurgeController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkPurgeController"/> class.
    /// </summary>
    /// <param name="bulkPurgeService">The bulk purge service.</param>
    /// <param name="logger">The logger.</param>
    public BulkPurgeController(
        IBulkPurgeService bulkPurgeService,
        ILogger<BulkPurgeController> logger)
    {
        _bulkPurgeService = bulkPurgeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a preview of records that would be deleted without actually deleting.
    /// </summary>
    /// <param name="criteria">The purge criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preview with estimated record count.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(BulkPurgePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkPurgePreviewDto>> Preview(
        [FromBody] BulkPurgeCriteriaDto criteria,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Bulk purge preview requested for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
            criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

        // Validate date range
        if (criteria.StartDate.HasValue && criteria.EndDate.HasValue &&
            criteria.StartDate.Value > criteria.EndDate.Value)
        {
            _logger.LogWarning(
                "Invalid date range: StartDate={StartDate} > EndDate={EndDate}",
                criteria.StartDate, criteria.EndDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var result = await _bulkPurgeService.PreviewPurgeAsync(criteria, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Preview failed",
                Detail = result.ErrorMessage,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Executes a bulk purge operation based on the specified criteria.
    /// </summary>
    /// <param name="criteria">The purge criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with deleted record count.</returns>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(BulkPurgeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkPurgeResultDto>> Execute(
        [FromBody] BulkPurgeCriteriaDto criteria,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = User.Identity?.Name ?? "unknown";

        _logger.LogInformation(
            "Bulk purge execution requested by {AdminUserId} for {EntityType}, DateRange: {DateRange}, GuildId: {GuildId}",
            adminUserId, criteria.EntityType, criteria.GetDateRangeDescription(), criteria.GuildId);

        // Validate date range
        if (criteria.StartDate.HasValue && criteria.EndDate.HasValue &&
            criteria.StartDate.Value > criteria.EndDate.Value)
        {
            _logger.LogWarning(
                "Invalid date range: StartDate={StartDate} > EndDate={EndDate}",
                criteria.StartDate, criteria.EndDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var result = await _bulkPurgeService.ExecutePurgeAsync(criteria, adminUserId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Purge failed",
                Detail = result.ErrorMessage,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation(
            "Bulk purge completed by {AdminUserId}: {DeletedCount} {EntityType} records deleted",
            adminUserId, result.DeletedCount, result.EntityType);

        return Ok(result);
    }
}
