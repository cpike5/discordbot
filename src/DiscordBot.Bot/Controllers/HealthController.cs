using DiscordBot.Core.DTOs;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for health check endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly BotDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    public HealthController(BotDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Simple liveness probe for load balancers and container orchestration.
    /// Returns 200 OK if the application is running.
    /// </summary>
    /// <returns>OK status.</returns>
    [HttpGet("live")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok();
    }

    /// <summary>
    /// Readiness probe that checks if the application is ready to serve requests.
    /// Verifies database connectivity.
    /// </summary>
    /// <returns>OK if ready, Service Unavailable if not.</returns>
    [HttpGet("ready")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            await _dbContext.Database.CanConnectAsync();
            return Ok();
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Gets detailed health status of the application.
    /// Requires authentication to prevent information disclosure.
    /// </summary>
    /// <returns>Health status information.</returns>
    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponseDto>> GetHealth()
    {
        _logger.LogDebug("Health check requested");

        var checks = new Dictionary<string, string>();

        // Check database connectivity
        try
        {
            await _dbContext.Database.CanConnectAsync();
            checks["Database"] = "Healthy";
            _logger.LogTrace("Database health check passed");
        }
        catch (Exception ex)
        {
            checks["Database"] = $"Unhealthy: {ex.Message}";
            _logger.LogError(ex, "Database health check failed");
        }

        var response = new HealthResponseDto
        {
            Status = checks.Values.All(v => v == "Healthy") ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
            Checks = checks
        };

        _logger.LogInformation("Health check completed: Status={Status}", response.Status);

        return Ok(response);
    }
}
