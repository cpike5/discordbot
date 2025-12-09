using DiscordBot.Core.DTOs;
using DiscordBot.Infrastructure.Data;
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
    /// Gets the health status of the application.
    /// </summary>
    /// <returns>Health status information.</returns>
    [HttpGet]
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
