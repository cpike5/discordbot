using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for audit log retrieval, statistics, and correlation tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireSuperAdmin")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogsController"/> class.
    /// </summary>
    /// <param name="auditLogService">The audit log service.</param>
    /// <param name="logger">The logger.</param>
    public AuditLogsController(
        IAuditLogService auditLogService,
        ILogger<AuditLogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets audit logs with optional filtering and pagination.
    /// </summary>
    /// <param name="query">The query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of audit logs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResponseDto<AuditLogDto>>> GetLogs(
        [FromQuery] AuditLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Audit logs requested with filters: Category={Category}, Action={Action}, ActorId={ActorId}, GuildId={GuildId}, Page={Page}",
            query.Category, query.Action, query.ActorId, query.GuildId, query.Page);

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

        var (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

        var result = new PaginatedResponseDto<AuditLogDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        _logger.LogTrace("Retrieved {Count} audit logs (Page {Page}/{TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        return Ok(result);
    }

    /// <summary>
    /// Gets a single audit log entry by its identifier.
    /// </summary>
    /// <param name="id">The audit log identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit log data if found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDto>> GetById(long id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Audit log {AuditLogId} requested", id);

        var auditLog = await _auditLogService.GetByIdAsync(id, cancellationToken);

        if (auditLog == null)
        {
            _logger.LogWarning("Audit log {AuditLogId} not found", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Audit log not found",
                Detail = $"No audit log entry with ID {id} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogTrace("Audit log {AuditLogId} retrieved: Category={Category}, Action={Action}, ActorId={ActorId}",
            id, auditLog.Category, auditLog.Action, auditLog.ActorId);

        return Ok(auditLog);
    }

    /// <summary>
    /// Gets comprehensive audit log statistics including counts and breakdowns.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter statistics. If null, returns global statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit log statistics including total counts, category breakdown, action breakdown, and top actors.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AuditLogStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogStatsDto>> GetStats(
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Audit log statistics requested for guild {GuildId}", guildId);

        var stats = await _auditLogService.GetStatsAsync(guildId, cancellationToken);

        _logger.LogTrace("Retrieved audit log statistics: {TotalEntries} total, {Last24Hours} in last 24h, {Last7Days} in last 7d",
            stats.TotalEntries, stats.Last24Hours, stats.Last7Days);

        return Ok(stats);
    }

    /// <summary>
    /// Gets all audit log entries related by correlation ID.
    /// Used to trace related events that are part of the same operation.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of related audit log entries ordered by timestamp.</returns>
    [HttpGet("by-correlation/{correlationId}")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetByCorrelationId(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Audit logs requested for correlation ID {CorrelationId}", correlationId);

        var logs = await _auditLogService.GetByCorrelationIdAsync(correlationId, cancellationToken);

        _logger.LogTrace("Retrieved {Count} audit logs for correlation ID {CorrelationId}", logs.Count, correlationId);

        return Ok(logs);
    }
}
