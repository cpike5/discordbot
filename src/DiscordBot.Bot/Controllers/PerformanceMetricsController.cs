using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for performance metrics and monitoring data.
/// </summary>
[ApiController]
[Route("api/metrics")]
[Authorize(Policy = "RequireViewer")]
public class PerformanceMetricsController : ControllerBase
{
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IApiRequestTracker _apiRequestTracker;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly IMetricSnapshotRepository _metricSnapshotRepository;
    private readonly ILogger<PerformanceMetricsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceMetricsController"/> class.
    /// </summary>
    /// <param name="connectionStateService">The connection state service.</param>
    /// <param name="latencyHistoryService">The latency history service.</param>
    /// <param name="commandPerformanceAggregator">The command performance aggregator.</param>
    /// <param name="apiRequestTracker">The API request tracker.</param>
    /// <param name="databaseMetricsCollector">The database metrics collector.</param>
    /// <param name="backgroundServiceHealthRegistry">The background service health registry.</param>
    /// <param name="instrumentedCache">The instrumented cache.</param>
    /// <param name="metricSnapshotRepository">The metric snapshot repository for historical data.</param>
    /// <param name="logger">The logger.</param>
    public PerformanceMetricsController(
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IApiRequestTracker apiRequestTracker,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        IMetricSnapshotRepository metricSnapshotRepository,
        ILogger<PerformanceMetricsController> logger)
    {
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _apiRequestTracker = apiRequestTracker;
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _metricSnapshotRepository = metricSnapshotRepository;
        _logger = logger;
    }

    // ============================================================================
    // Health Endpoints
    // ============================================================================

    /// <summary>
    /// Gets overall performance health status with uptime and latency.
    /// </summary>
    /// <returns>Performance health snapshot.</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(PerformanceHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<PerformanceHealthDto> GetHealth()
    {
        try
        {
            _logger.LogDebug("Performance health requested");

            var connectionState = _connectionStateService.GetCurrentState();
            var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            var currentLatency = _latencyHistoryService.GetCurrentLatency();
            var overallStatus = _backgroundServiceHealthRegistry.GetOverallStatus();

            var health = new PerformanceHealthDto
            {
                Status = overallStatus,
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace("Performance health retrieved: Status={Status}, Uptime={Uptime}, Latency={LatencyMs}ms",
                health.Status, health.Uptime, health.LatencyMs);

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve performance health");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve performance health",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets latency history with statistical analysis.
    /// </summary>
    /// <param name="hours">Number of hours of history to retrieve (1-168, default: 24).</param>
    /// <returns>Latency history with samples and statistics.</returns>
    [HttpGet("health/latency")]
    [ProducesResponseType(typeof(LatencyHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<LatencyHistoryDto> GetLatencyHistory([FromQuery] int hours = 24)
    {
        try
        {
            if (hours < 1 || hours > 168)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 168 (7 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Latency history requested for {Hours} hours", hours);

            var samples = _latencyHistoryService.GetSamples(hours);
            var statistics = _latencyHistoryService.GetStatistics(hours);

            var history = new LatencyHistoryDto
            {
                Samples = samples,
                Statistics = statistics
            };

            _logger.LogTrace("Retrieved {SampleCount} latency samples, avg {AvgMs}ms",
                statistics.SampleCount, statistics.Average);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve latency history for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve latency history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets connection event history and statistics.
    /// </summary>
    /// <param name="days">Number of days of history to retrieve (1-30, default: 7).</param>
    /// <returns>Connection history with events and statistics.</returns>
    [HttpGet("health/connections")]
    [ProducesResponseType(typeof(ConnectionHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<ConnectionHistoryDto> GetConnectionHistory([FromQuery] int days = 7)
    {
        try
        {
            if (days < 1 || days > 30)
            {
                _logger.LogWarning("Invalid days parameter: {Days}", days);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid days parameter",
                    Detail = "Days must be between 1 and 30.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Connection history requested for {Days} days", days);

            var events = _connectionStateService.GetConnectionEvents(days);
            var statistics = _connectionStateService.GetConnectionStats(days);

            var history = new ConnectionHistoryDto
            {
                Events = events,
                Statistics = statistics
            };

            _logger.LogTrace("Retrieved {EventCount} connection events, uptime {UptimePercentage:F2}%",
                statistics.TotalEvents, statistics.UptimePercentage);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve connection history for {Days} days", days);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve connection history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // Command Performance Endpoints
    // ============================================================================

    /// <summary>
    /// Gets aggregated command performance metrics.
    /// </summary>
    /// <param name="hours">Number of hours of history to aggregate (1-720, default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of command performance aggregates.</returns>
    [HttpGet("commands/performance")]
    [ProducesResponseType(typeof(IReadOnlyList<CommandPerformanceAggregateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<CommandPerformanceAggregateDto>>> GetCommandPerformance(
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Command performance requested for {Hours} hours", hours);

            var aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);

            _logger.LogTrace("Retrieved performance metrics for {CommandCount} commands", aggregates.Count);

            return Ok(aggregates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve command performance for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve command performance",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets the slowest command executions.
    /// </summary>
    /// <param name="limit">Maximum number of results to return (1-100, default: 10).</param>
    /// <param name="hours">Number of hours of history to query (1-720, default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of slowest commands.</returns>
    [HttpGet("commands/slowest")]
    [ProducesResponseType(typeof(IReadOnlyList<SlowestCommandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<SlowestCommandDto>>> GetSlowestCommands(
        [FromQuery] int limit = 10,
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit < 1 || limit > 100)
            {
                _logger.LogWarning("Invalid limit parameter: {Limit}", limit);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid limit parameter",
                    Detail = "Limit must be between 1 and 100.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Slowest commands requested: limit={Limit}, hours={Hours}", limit, hours);

            var slowestCommands = await _commandPerformanceAggregator.GetSlowestCommandsAsync(limit, hours);

            _logger.LogTrace("Retrieved {Count} slowest commands", slowestCommands.Count);

            return Ok(slowestCommands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slowest commands for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve slowest commands",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets command execution throughput over time.
    /// </summary>
    /// <param name="hours">Number of hours of history to include (1-720, default: 24).</param>
    /// <param name="granularity">Time bucket granularity: "hour" or "day" (default: "hour").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of throughput measurements over time.</returns>
    [HttpGet("commands/throughput")]
    [ProducesResponseType(typeof(IReadOnlyList<CommandThroughputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<CommandThroughputDto>>> GetCommandThroughput(
        [FromQuery] int hours = 24,
        [FromQuery] string granularity = "hour",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            if (granularity != "hour" && granularity != "day")
            {
                _logger.LogWarning("Invalid granularity parameter: {Granularity}", granularity);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid granularity parameter",
                    Detail = "Granularity must be 'hour' or 'day'.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Command throughput requested: hours={Hours}, granularity={Granularity}", hours, granularity);

            var throughput = await _commandPerformanceAggregator.GetThroughputAsync(hours, granularity);

            _logger.LogTrace("Retrieved {DataPointCount} throughput data points", throughput.Count);

            return Ok(throughput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve command throughput for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve command throughput",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets command error breakdown and statistics.
    /// </summary>
    /// <param name="hours">Number of hours of history to analyze (1-720, default: 24).</param>
    /// <param name="limit">Maximum number of commands to return (1-100, default: 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command error summary with rate and breakdown.</returns>
    [HttpGet("commands/errors")]
    [ProducesResponseType(typeof(CommandErrorsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommandErrorsDto>> GetCommandErrors(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            if (limit < 1 || limit > 100)
            {
                _logger.LogWarning("Invalid limit parameter: {Limit}", limit);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid limit parameter",
                    Detail = "Limit must be between 1 and 100.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Command errors requested: hours={Hours}, limit={Limit}", hours, limit);

            var errorBreakdown = await _commandPerformanceAggregator.GetErrorBreakdownAsync(hours, limit);

            // Calculate overall error rate from aggregates
            var aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);
            var totalCommands = aggregates.Sum(a => a.ExecutionCount);
            var totalErrors = aggregates.Sum(a => (int)(a.ExecutionCount * (a.ErrorRate / 100.0)));
            var overallErrorRate = totalCommands > 0 ? (totalErrors * 100.0 / totalCommands) : 0;

            // Create recent errors list from error breakdown
            var recentErrors = errorBreakdown
                .SelectMany(eb => eb.ErrorMessages.Select(em => new RecentCommandErrorDto
                {
                    Timestamp = DateTime.UtcNow, // Note: This is approximate, actual timestamps would need to come from command logs
                    CommandName = eb.CommandName,
                    ErrorMessage = em.Key,
                    GuildId = null
                }))
                .Take(limit)
                .ToList();

            var errors = new CommandErrorsDto
            {
                ErrorRate = overallErrorRate,
                ByType = errorBreakdown,
                RecentErrors = recentErrors
            };

            _logger.LogTrace("Retrieved error data: overall rate {ErrorRate:F2}%, {BreakdownCount} commands with errors",
                errors.ErrorRate, errorBreakdown.Count);

            return Ok(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve command errors for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve command errors",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // API Usage Endpoints
    // ============================================================================

    /// <summary>
    /// Gets Discord API usage statistics.
    /// </summary>
    /// <param name="hours">Number of hours of history to retrieve (1-168, default: 24).</param>
    /// <returns>API usage summary with total requests and breakdown by category.</returns>
    [HttpGet("api/usage")]
    [ProducesResponseType(typeof(ApiUsageSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<ApiUsageSummaryDto> GetApiUsage([FromQuery] int hours = 24)
    {
        try
        {
            if (hours < 1 || hours > 168)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 168 (7 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("API usage requested for {Hours} hours", hours);

            var usageByCategory = _apiRequestTracker.GetUsageStatistics(hours);
            var totalRequests = _apiRequestTracker.GetTotalRequests(hours);
            var rateLimitEvents = _apiRequestTracker.GetRateLimitEvents(hours);

            var usage = new ApiUsageSummaryDto
            {
                TotalRequests = totalRequests,
                ByCategory = usageByCategory,
                RateLimitHits = rateLimitEvents.Count
            };

            _logger.LogTrace("Retrieved API usage: {TotalRequests} total requests, {CategoryCount} categories",
                totalRequests, usageByCategory.Count);

            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API usage for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve API usage",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets Discord API rate limit events.
    /// </summary>
    /// <param name="hours">Number of hours of history to retrieve (1-168, default: 24).</param>
    /// <returns>Rate limit summary with event count and details.</returns>
    [HttpGet("api/rate-limits")]
    [ProducesResponseType(typeof(RateLimitSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<RateLimitSummaryDto> GetRateLimits([FromQuery] int hours = 24)
    {
        try
        {
            if (hours < 1 || hours > 168)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 168 (7 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Rate limit events requested for {Hours} hours", hours);

            var events = _apiRequestTracker.GetRateLimitEvents(hours);

            var rateLimits = new RateLimitSummaryDto
            {
                HitCount = events.Count,
                Events = events
            };

            _logger.LogTrace("Retrieved {EventCount} rate limit events", events.Count);

            return Ok(rateLimits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve rate limit events for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve rate limit events",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets Discord API latency history with samples and statistics.
    /// </summary>
    /// <param name="hours">Number of hours of history to retrieve (1-168, default: 24).</param>
    /// <returns>API latency history with time series samples and aggregate statistics.</returns>
    [HttpGet("api/latency")]
    [ProducesResponseType(typeof(ApiLatencyHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<ApiLatencyHistoryDto> GetApiLatency([FromQuery] int hours = 24)
    {
        try
        {
            if (hours < 1 || hours > 168)
            {
                _logger.LogWarning("Invalid hours parameter: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 168 (7 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("API latency history requested for {Hours} hours", hours);

            var samples = _apiRequestTracker.GetLatencySamples(hours);
            var statistics = _apiRequestTracker.GetLatencyStatistics(hours);

            var history = new ApiLatencyHistoryDto
            {
                Samples = samples,
                Statistics = statistics
            };

            _logger.LogTrace("Retrieved {SampleCount} API latency samples, avg {AvgMs:F1}ms",
                samples.Count, statistics.AvgLatencyMs);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API latency history for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve API latency history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // System Health Endpoints
    // ============================================================================

    /// <summary>
    /// Gets database performance metrics and slow queries.
    /// </summary>
    /// <param name="limit">Maximum number of slow queries to return (1-100, default: 20).</param>
    /// <returns>Database metrics summary with overall stats and slow queries.</returns>
    [HttpGet("system/database")]
    [ProducesResponseType(typeof(DatabaseMetricsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<DatabaseMetricsSummaryDto> GetDatabaseMetrics([FromQuery] int limit = 20)
    {
        try
        {
            if (limit < 1 || limit > 100)
            {
                _logger.LogWarning("Invalid limit parameter: {Limit}", limit);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid limit parameter",
                    Detail = "Limit must be between 1 and 100.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Database metrics requested with limit {Limit}", limit);

            var metrics = _databaseMetricsCollector.GetMetrics();
            var slowQueries = _databaseMetricsCollector.GetSlowQueries(limit);

            var summary = new DatabaseMetricsSummaryDto
            {
                Metrics = metrics,
                RecentSlowQueries = slowQueries
            };

            _logger.LogTrace("Retrieved database metrics: {TotalQueries} queries, avg {AvgMs}ms, {SlowQueryCount} slow queries",
                metrics.TotalQueries, metrics.AvgQueryTimeMs, slowQueries.Count);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve database metrics");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve database metrics",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets health status of all background services.
    /// </summary>
    /// <returns>List of background service health information.</returns>
    [HttpGet("system/services")]
    [ProducesResponseType(typeof(IReadOnlyList<BackgroundServiceHealthDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<IReadOnlyList<BackgroundServiceHealthDto>> GetBackgroundServicesHealth()
    {
        try
        {
            _logger.LogDebug("Background services health requested");

            var servicesHealth = _backgroundServiceHealthRegistry.GetAllHealth();

            _logger.LogTrace("Retrieved health for {ServiceCount} background services", servicesHealth.Count);

            return Ok(servicesHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve background services health");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve background services health",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets cache statistics with overall and per-prefix breakdown.
    /// </summary>
    /// <returns>Cache summary with overall stats and breakdown by key prefix.</returns>
    [HttpGet("system/cache")]
    [ProducesResponseType(typeof(CacheSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public ActionResult<CacheSummaryDto> GetCacheStatistics()
    {
        try
        {
            _logger.LogDebug("Cache statistics requested");

            var statisticsByPrefix = _instrumentedCache.GetStatistics();

            // Calculate overall statistics
            var totalHits = statisticsByPrefix.Sum(s => s.Hits);
            var totalMisses = statisticsByPrefix.Sum(s => s.Misses);
            var totalAccesses = totalHits + totalMisses;
            var overallHitRate = totalAccesses > 0 ? (totalHits * 100.0 / totalAccesses) : 0;
            var totalSize = statisticsByPrefix.Sum(s => s.Size);

            var overall = new CacheStatisticsDto
            {
                KeyPrefix = "Overall",
                Hits = totalHits,
                Misses = totalMisses,
                HitRate = overallHitRate,
                Size = totalSize
            };

            var summary = new CacheSummaryDto
            {
                Overall = overall,
                ByType = statisticsByPrefix
            };

            _logger.LogTrace("Retrieved cache statistics: {HitRate:F2}% hit rate, {PrefixCount} prefixes",
                overallHitRate, statisticsByPrefix.Count);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve cache statistics");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve cache statistics",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    // ============================================================================
    // Historical Metrics Endpoints
    // ============================================================================

    /// <summary>
    /// Gets historical system metrics for charting.
    /// </summary>
    /// <param name="hours">Time range in hours (1-720, default: 24).</param>
    /// <param name="metric">Metric filter: "database", "memory", "cache", "services", or "all" (default: "all").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical metrics with time range and aggregation info.</returns>
    [HttpGet("system/history")]
    [ProducesResponseType(typeof(HistoricalMetricsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HistoricalMetricsResponseDto>> GetHistoricalMetrics(
        [FromQuery] int hours = 24,
        [FromQuery] string metric = "all",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter for historical metrics: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            var validMetrics = new[] { "all", "database", "memory", "cache", "services" };
            if (!validMetrics.Contains(metric.ToLowerInvariant()))
            {
                _logger.LogWarning("Invalid metric parameter: {Metric}", metric);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid metric parameter",
                    Detail = "Metric must be 'database', 'memory', 'cache', 'services', or 'all'.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Historical metrics requested: hours={Hours}, metric={Metric}", hours, metric);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hours);

            // Determine aggregation based on time range
            var (aggregationMinutes, granularityLabel) = GetAggregationForTimeRange(hours);

            var snapshots = await _metricSnapshotRepository.GetRangeAsync(
                startTime,
                endTime,
                aggregationMinutes,
                cancellationToken);

            var response = new HistoricalMetricsResponseDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Granularity = granularityLabel,
                Snapshots = snapshots
            };

            _logger.LogTrace("Retrieved {SnapshotCount} historical metric snapshots for {Hours} hours",
                snapshots.Count, hours);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve historical metrics for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve historical metrics",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets historical database performance metrics.
    /// </summary>
    /// <param name="hours">Time range in hours (1-720, default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical database metrics with samples and statistics.</returns>
    [HttpGet("system/history/database")]
    [ProducesResponseType(typeof(DatabaseHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DatabaseHistoryResponseDto>> GetDatabaseHistory(
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter for database history: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Database history requested for {Hours} hours", hours);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hours);

            // Determine aggregation based on time range
            var (aggregationMinutes, _) = GetAggregationForTimeRange(hours);

            var snapshots = await _metricSnapshotRepository.GetRangeAsync(
                startTime,
                endTime,
                aggregationMinutes,
                cancellationToken);

            // Transform to database-specific samples
            var samples = snapshots.Select(s => new DatabaseHistorySampleDto
            {
                Timestamp = s.Timestamp,
                AvgQueryTimeMs = s.DatabaseAvgQueryTimeMs,
                TotalQueries = s.DatabaseTotalQueries,
                SlowQueryCount = s.DatabaseSlowQueryCount
            }).ToList();

            // Calculate statistics
            var statistics = new DatabaseHistoryStatisticsDto();
            if (samples.Count > 0)
            {
                statistics = new DatabaseHistoryStatisticsDto
                {
                    AvgQueryTimeMs = samples.Average(s => s.AvgQueryTimeMs),
                    MinQueryTimeMs = samples.Min(s => s.AvgQueryTimeMs),
                    MaxQueryTimeMs = samples.Max(s => s.AvgQueryTimeMs),
                    TotalSlowQueries = samples.Sum(s => s.SlowQueryCount)
                };
            }

            var response = new DatabaseHistoryResponseDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Samples = samples,
                Statistics = statistics
            };

            _logger.LogTrace("Retrieved {SampleCount} database history samples for {Hours} hours",
                samples.Count, hours);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve database history for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve database history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Gets historical memory metrics.
    /// </summary>
    /// <param name="hours">Time range in hours (1-720, default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical memory metrics with samples and statistics.</returns>
    [HttpGet("system/history/memory")]
    [ProducesResponseType(typeof(MemoryHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemoryHistoryResponseDto>> GetMemoryHistory(
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (hours < 1 || hours > 720)
            {
                _logger.LogWarning("Invalid hours parameter for memory history: {Hours}", hours);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid hours parameter",
                    Detail = "Hours must be between 1 and 720 (30 days).",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            _logger.LogDebug("Memory history requested for {Hours} hours", hours);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hours);

            // Determine aggregation based on time range
            var (aggregationMinutes, _) = GetAggregationForTimeRange(hours);

            var snapshots = await _metricSnapshotRepository.GetRangeAsync(
                startTime,
                endTime,
                aggregationMinutes,
                cancellationToken);

            // Transform to memory-specific samples
            var samples = snapshots.Select(s => new MemoryHistorySampleDto
            {
                Timestamp = s.Timestamp,
                WorkingSetMB = s.WorkingSetMB,
                HeapSizeMB = s.HeapSizeMB,
                PrivateMemoryMB = s.PrivateMemoryMB
            }).ToList();

            // Calculate statistics
            var statistics = new MemoryHistoryStatisticsDto();
            if (samples.Count > 0)
            {
                statistics = new MemoryHistoryStatisticsDto
                {
                    AvgWorkingSetMB = samples.Average(s => s.WorkingSetMB),
                    MaxWorkingSetMB = samples.Max(s => s.WorkingSetMB),
                    AvgHeapSizeMB = samples.Average(s => s.HeapSizeMB)
                };
            }

            var response = new MemoryHistoryResponseDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Samples = samples,
                Statistics = statistics
            };

            _logger.LogTrace("Retrieved {SampleCount} memory history samples for {Hours} hours",
                samples.Count, hours);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memory history for {Hours} hours", hours);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Failed to retrieve memory history",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Determines the aggregation bucket size based on the requested time range.
    /// </summary>
    /// <param name="hours">The requested time range in hours.</param>
    /// <returns>Tuple of (aggregation minutes, granularity label).</returns>
    private static (int aggregationMinutes, string granularityLabel) GetAggregationForTimeRange(int hours)
    {
        return hours switch
        {
            <= 6 => (0, "raw"),              // 1-6 hours: raw samples
            <= 24 => (5, "5m"),              // 7-24 hours: 5-minute buckets
            <= 168 => (15, "15m"),           // 25-168 hours (7 days): 15-minute buckets
            _ => (60, "1h")                  // 169-720 hours (30 days): 1-hour buckets
        };
    }
}
