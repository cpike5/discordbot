using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for user logout functionality.
/// </summary>
[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;
    private readonly IAuditLogService _auditLogService;

    public LogoutModel(
        SignInManager<ApplicationUser> signInManager,
        ILogger<LogoutModel> logger,
        IAuditLogService auditLogService)
    {
        _signInManager = signInManager;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Handles POST request for user logout.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Audit log BEFORE signing out (while we still have user context)
        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                _auditLogService.CreateBuilder()
                    .ForCategory(AuditLogCategory.Security)
                    .WithAction(AuditLogAction.Logout)
                    .ByUser(userId)
                    .OnTarget("User", userId)
                    .FromIpAddress(ipAddress ?? "Unknown")
                    .WithDetails(new { userName })
                    .Enqueue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for logout {UserName}", userName);
            }
        }

        await _signInManager.SignOutAsync();

        _logger.LogInformation("User {UserName} logged out", userName ?? "Unknown");

        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
