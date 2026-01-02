using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for performance alert configuration and incident management.
/// Provides endpoints for alert configuration, active incidents, and incident history.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AlertsController : ControllerBase
{
    private readonly IPerformanceAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertsController"/> class.
    /// </summary>
    /// <param name="alertService">The performance alert service.</param>
    /// <param name="logger">The logger.</param>
    public AlertsController(
        IPerformanceAlertService alertService,
        ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    // ============================================================================
    // Alert Configuration Endpoints
    // ============================================================================

    /// <summary>
    /// Gets all alert configurations with their current metric values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all alert configurations.</returns>
    [HttpGet("config")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(IReadOnlyList<AlertConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<AlertConfigDto>>> GetAllConfigs(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Alert configurations requested");

            var configs = await _alertService.GetAllConfigsAsync(cancellationToken);

            _logger.LogTrace("Retrieved {ConfigCount} alert configurations", configs.Count);

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve alert configurations");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve alert configurations",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets a specific alert configuration by metric name.
    /// </summary>
    /// <param name="metricName">The metric name to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The alert configuration if found.</returns>
    [HttpGet("config/{metricName}")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(AlertConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertConfigDto>> GetConfigByMetricName(
        string metricName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Alert configuration requested for metric {MetricName}", metricName);

            var config = await _alertService.GetConfigByMetricNameAsync(metricName, cancellationToken);

            if (config == null)
            {
                _logger.LogWarning("Alert configuration not found for metric {MetricName}", metricName);

                return NotFound(new ApiErrorDto
                {
                    Message = "Alert configuration not found",
                    Detail = $"No alert configuration exists for metric '{metricName}'.",
                    StatusCode = StatusCodes.Status404NotFound,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogTrace("Retrieved alert configuration for metric {MetricName}", metricName);

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve alert configuration for metric {MetricName}", metricName);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve alert configuration",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Updates an alert configuration with new threshold values.
    /// </summary>
    /// <param name="metricName">The metric name to update.</param>
    /// <param name="update">The update data containing new threshold values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated alert configuration.</returns>
    [HttpPut("config/{metricName}")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(AlertConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertConfigDto>> UpdateConfig(
        string metricName,
        [FromBody] AlertConfigUpdateDto update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims for alert config update");

                return BadRequest(new ApiErrorDto
                {
                    Message = "User identification failed",
                    Detail = "Unable to identify the current user.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Updating alert configuration for metric {MetricName} by user {UserId}", metricName, userId);

            var updatedConfig = await _alertService.UpdateConfigAsync(metricName, update, userId, cancellationToken);

            _logger.LogInformation("Alert configuration updated for metric {MetricName} by user {UserId}: WarningThreshold={Warning}, CriticalThreshold={Critical}, IsEnabled={Enabled}",
                metricName, userId, update.WarningThreshold, update.CriticalThreshold, update.IsEnabled);

            return Ok(updatedConfig);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Alert configuration not found for metric {MetricName}", metricName);

            return NotFound(new ApiErrorDto
            {
                Message = "Alert configuration not found",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid update data for alert configuration {MetricName}", metricName);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid update data",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update alert configuration for metric {MetricName}", metricName);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to update alert configuration",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // Incident Endpoints
    // ============================================================================

    /// <summary>
    /// Gets all currently active (unresolved) incidents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active incidents.</returns>
    [HttpGet("active")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(IReadOnlyList<PerformanceIncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<PerformanceIncidentDto>>> GetActiveIncidents(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Active incidents requested");

            var incidents = await _alertService.GetActiveIncidentsAsync(cancellationToken);

            _logger.LogTrace("Retrieved {IncidentCount} active incidents", incidents.Count);

            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active incidents");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve active incidents",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets paginated incident history with optional filtering.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated incident results.</returns>
    [HttpGet("incidents")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(IncidentPagedResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IncidentPagedResultDto>> GetIncidentHistory(
        [FromQuery] IncidentQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Incident history requested: PageNumber={PageNumber}, PageSize={PageSize}, Severity={Severity}, Status={Status}, MetricName={MetricName}",
                query.PageNumber, query.PageSize, query.Severity, query.Status, query.MetricName);

            var result = await _alertService.GetIncidentHistoryAsync(query, cancellationToken);

            _logger.LogTrace("Retrieved {ItemCount} incidents (page {PageNumber}/{TotalPages}, total {TotalCount})",
                result.Items.Count, result.PageNumber, result.TotalPages, result.TotalCount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve incident history");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve incident history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets a specific incident by ID.
    /// </summary>
    /// <param name="id">The incident ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incident details if found.</returns>
    [HttpGet("incidents/{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(PerformanceIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformanceIncidentDto>> GetIncidentById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Incident requested: {IncidentId}", id);

            var incident = await _alertService.GetIncidentByIdAsync(id, cancellationToken);

            if (incident == null)
            {
                _logger.LogWarning("Incident not found: {IncidentId}", id);

                return NotFound(new ApiErrorDto
                {
                    Message = "Incident not found",
                    Detail = $"No incident exists with ID '{id}'.",
                    StatusCode = StatusCodes.Status404NotFound,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogTrace("Retrieved incident {IncidentId}", id);

            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve incident {IncidentId}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve incident",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Acknowledges an incident, marking it as reviewed by an administrator.
    /// </summary>
    /// <param name="id">The incident ID to acknowledge.</param>
    /// <param name="dto">Optional acknowledgment data (notes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated incident.</returns>
    [HttpPost("incidents/{id:guid}/acknowledge")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(PerformanceIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformanceIncidentDto>> AcknowledgeIncident(
        Guid id,
        [FromBody] AcknowledgeIncidentDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims for incident acknowledgment");

                return BadRequest(new ApiErrorDto
                {
                    Message = "User identification failed",
                    Detail = "Unable to identify the current user.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Acknowledging incident {IncidentId} by user {UserId}", id, userId);

            var incident = await _alertService.AcknowledgeIncidentAsync(id, userId, dto.Notes, cancellationToken);

            _logger.LogInformation("Incident {IncidentId} acknowledged by user {UserId}", id, userId);

            return Ok(incident);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Incident not found for acknowledgment: {IncidentId}", id);

            return NotFound(new ApiErrorDto
            {
                Message = "Incident not found",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge incident {IncidentId}", id);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to acknowledge incident",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Acknowledges all currently active incidents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of incidents acknowledged.</returns>
    [HttpPost("incidents/acknowledge-all")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> AcknowledgeAllIncidents(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims for bulk incident acknowledgment");

                return BadRequest(new ApiErrorDto
                {
                    Message = "User identification failed",
                    Detail = "Unable to identify the current user.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Acknowledging all active incidents by user {UserId}", userId);

            var acknowledgedCount = await _alertService.AcknowledgeAllActiveAsync(userId, cancellationToken);

            _logger.LogInformation("Acknowledged {AcknowledgedCount} active incidents by user {UserId}", acknowledgedCount, userId);

            return Ok(new { acknowledgedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge all active incidents");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to acknowledge all active incidents",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // Summary & Statistics Endpoints
    // ============================================================================

    /// <summary>
    /// Gets active alert summary statistics for dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of active alerts by severity.</returns>
    [HttpGet("summary")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(ActiveAlertSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ActiveAlertSummaryDto>> GetActiveAlertSummary(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Active alert summary requested");

            var summary = await _alertService.GetActiveAlertSummaryAsync(cancellationToken);

            _logger.LogTrace("Retrieved active alert summary: {ActiveCount} total, {CriticalCount} critical, {WarningCount} warning",
                summary.ActiveCount, summary.CriticalCount, summary.WarningCount);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active alert summary");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve active alert summary",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets alert frequency statistics for charts.
    /// Returns daily incident counts by severity for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to include (1-365, default: 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily alert frequency data points.</returns>
    [HttpGet("stats")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(IReadOnlyList<AlertFrequencyDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<AlertFrequencyDataDto>>> GetAlertFrequencyStats(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (days < 1 || days > 365)
            {
                _logger.LogWarning("Invalid days parameter: {Days}", days);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid days parameter",
                    Detail = "Days must be between 1 and 365.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Alert frequency statistics requested for {Days} days", days);

            var stats = await _alertService.GetAlertFrequencyDataAsync(days, cancellationToken);

            _logger.LogTrace("Retrieved {DataPointCount} alert frequency data points", stats.Count);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve alert frequency statistics for {Days} days", days);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve alert frequency statistics",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }
}
