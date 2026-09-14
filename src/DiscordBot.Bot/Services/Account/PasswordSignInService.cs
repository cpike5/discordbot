using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Account;

/// <summary>
/// Default <see cref="IPasswordSignInService"/> - a near-verbatim port of
/// <c>Pages/Account/Login.cshtml.cs</c>'s <c>OnPostAsync</c> (docs/plans/blazor-port-plan.md
/// Phase 4 cluster 4c), with the 2FA branch collapsed into <see cref="PasswordSignInOutcome.Failed"/>
/// (see the interface's remarks) and no <c>IActionResult</c>/<c>ModelState</c> anywhere in it.
/// </summary>
public sealed class PasswordSignInService : IPasswordSignInService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PasswordSignInService> _logger;

    public PasswordSignInService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ILogger<PasswordSignInService> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PasswordSignInOutcome> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        string returnUrl,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for user {Email}", email);

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && !user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user {Email}", email);
            return new PasswordSignInOutcome.Inactive();
        }

        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully", email);

            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                try
                {
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.Security)
                        .WithAction(AuditLogAction.Login)
                        .ByUser(user.Id)
                        .OnTarget("User", user.Id)
                        .FromIpAddress(ipAddress ?? "Unknown")
                        .WithDetails(new { email = user.Email, method = "Password" })
                        .Enqueue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log audit entry for login {Email}", email);
                }
            }

            return new PasswordSignInOutcome.Success(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account {Email} is locked out", email);
            return new PasswordSignInOutcome.LockedOut();
        }

        if (result.RequiresTwoFactor)
        {
            // No 2FA flow exists in this application - the legacy page redirected to a
            // "./LoginWith2fa" page that was never implemented. Log distinctly, then fall through
            // to the same "invalid credentials" handling as any other failed attempt.
            _logger.LogWarning(
                "User {Email} sign-in reported RequiresTwoFactor, but no 2FA flow is implemented - treating as a failed attempt",
                email);
        }
        else
        {
            _logger.LogWarning("Invalid login attempt for {Email}", email);
        }

        try
        {
            _auditLogService.CreateBuilder()
                .ForCategory(AuditLogCategory.Security)
                .WithAction(AuditLogAction.Login)
                .BySystem()
                .OnTarget("User", "Unknown")
                .FromIpAddress(ipAddress ?? "Unknown")
                .WithDetails(new { email, success = false, reason = "Invalid credentials" })
                .Enqueue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry for failed login {Email}", email);
        }

        return new PasswordSignInOutcome.Failed("Invalid email or password. Please try again.");
    }
}
