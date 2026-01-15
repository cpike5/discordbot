using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// API controller for theme management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ThemeController : ControllerBase
{
    private readonly IThemeService _themeService;
    private readonly ILogger<ThemeController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeController"/> class.
    /// </summary>
    /// <param name="themeService">The theme service.</param>
    /// <param name="logger">The logger.</param>
    public ThemeController(IThemeService themeService, ILogger<ThemeController> logger)
    {
        _themeService = themeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active themes available for selection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available themes.</returns>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<ThemeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ThemeDto>>> GetAvailableThemes(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting available themes");

        var themes = await _themeService.GetActiveThemesAsync(cancellationToken);
        return Ok(themes);
    }

    /// <summary>
    /// Gets the current user's effective theme (following preference hierarchy).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current theme with its source.</returns>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentThemeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentThemeDto>> GetCurrentTheme(
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GetCurrentTheme called without authenticated user");
            return Unauthorized();
        }

        _logger.LogDebug("Getting current theme for user {UserId}", userId);

        var currentTheme = await _themeService.GetUserThemeAsync(userId, cancellationToken);
        return Ok(currentTheme);
    }

    /// <summary>
    /// Sets the current user's theme preference.
    /// Also sets a cookie for server-side rendering on subsequent page loads.
    /// </summary>
    /// <param name="request">The theme to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status with theme key.</returns>
    [HttpPost("user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetUserTheme(
        [FromBody] SetUserThemeDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("SetUserTheme called without authenticated user");
            return Unauthorized();
        }

        _logger.LogInformation("User {UserId} setting theme preference to {ThemeId}",
            userId, request.ThemeId?.ToString() ?? "null");

        var success = await _themeService.SetUserThemeAsync(userId, request.ThemeId, cancellationToken);

        if (success)
        {
            // Get the theme key for the response and cookie
            string themeKey;
            if (request.ThemeId.HasValue)
            {
                var theme = await _themeService.GetThemeByIdAsync(request.ThemeId.Value, cancellationToken);
                themeKey = theme?.ThemeKey ?? "discord-dark";

                // Set cookie for SSR on next page load
                Response.Cookies.Append(IThemeService.ThemePreferenceCookieName, themeKey, new CookieOptions
                {
                    Path = "/",
                    MaxAge = TimeSpan.FromDays(365),
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true // Functional cookie, not subject to consent
                });

                _logger.LogDebug("Set theme preference cookie: {ThemeKey}", themeKey);
            }
            else
            {
                // Clear the cookie when preference is cleared
                Response.Cookies.Delete(IThemeService.ThemePreferenceCookieName);
                var defaultTheme = await _themeService.GetDefaultThemeAsync(cancellationToken);
                themeKey = defaultTheme.ThemeKey;

                _logger.LogDebug("Cleared theme preference cookie, using default: {ThemeKey}", themeKey);
            }

            return Ok(new { themeKey, message = request.ThemeId.HasValue
                ? "Theme preference updated successfully"
                : "Theme preference cleared" });
        }

        return BadRequest(new ApiErrorDto
        {
            StatusCode = 400,
            Message = request.ThemeId.HasValue
                ? "Theme not found or not available"
                : "Failed to clear theme preference"
        });
    }

    /// <summary>
    /// Sets the system default theme (SuperAdmin only).
    /// </summary>
    /// <param name="request">The theme to set as default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("default")]
    [Authorize(Policy = "RequireSuperAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetDefaultTheme(
        [FromBody] SetDefaultThemeDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("User {UserId} setting system default theme to {ThemeId}",
            userId, request.ThemeId);

        var success = await _themeService.SetDefaultThemeAsync(request.ThemeId, cancellationToken);

        if (success)
        {
            return Ok(new { Message = "System default theme updated successfully" });
        }

        return BadRequest(new ApiErrorDto
        {
            StatusCode = 400,
            Message = "Theme not found or not available"
        });
    }
}
