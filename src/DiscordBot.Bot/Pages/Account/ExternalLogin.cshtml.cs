using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Handles external OAuth login callbacks (e.g., Discord OAuth).
/// </summary>
[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDiscordTokenService _tokenService;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IDiscordTokenService tokenService,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Error message to display when external login fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The return URL after successful authentication.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// The external provider name (e.g., "Discord").
    /// </summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>
    /// Handles GET requests - redirects to login page since external login requires POST.
    /// </summary>
    public IActionResult OnGet() => RedirectToPage("./Login");

    /// <summary>
    /// Handles the OAuth callback from external providers.
    /// </summary>
    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!string.IsNullOrEmpty(remoteError))
        {
            _logger.LogWarning("External login failed with remote error: {RemoteError}", remoteError);
            ErrorMessage = $"Error from external provider: {remoteError}";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("External login info was null - user may have cancelled or callback failed");
            ErrorMessage = "Error loading external login information.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        ProviderDisplayName = info.ProviderDisplayName;
        _logger.LogInformation("External login callback received from {Provider}", info.LoginProvider);

        // Extract OAuth tokens BEFORE signing in (they come from external auth cookie)
        string? accessToken = null;
        string? refreshToken = null;
        string? expiresAt = null;
        if (info.LoginProvider == "Discord")
        {
            accessToken = await HttpContext.GetTokenAsync("access_token");
            refreshToken = await HttpContext.GetTokenAsync("refresh_token");
            expiresAt = await HttpContext.GetTokenAsync("expires_at");
        }

        // Try to sign in with existing external login
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: true,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            _logger.LogInformation("User logged in with {Provider} provider", info.LoginProvider);

            // Update user's Discord info and last login
            await UpdateUserDiscordInfoAsync(info);

            // Store OAuth tokens if this is a Discord login
            if (info.LoginProvider == "Discord")
            {
                await StoreOAuthTokensAsync(info, accessToken, refreshToken, expiresAt);
            }

            return LocalRedirect(returnUrl);
        }

        if (signInResult.IsLockedOut)
        {
            _logger.LogWarning("User account locked out during external login");
            return RedirectToPage("./Lockout");
        }

        // User doesn't have an account yet - create one
        _logger.LogInformation("Creating new user account for {Provider} login", info.LoginProvider);
        return await CreateUserFromExternalLoginAsync(info, returnUrl, accessToken, refreshToken, expiresAt);
    }

    /// <summary>
    /// Creates a new user from external login information.
    /// </summary>
    private async Task<IActionResult> CreateUserFromExternalLoginAsync(
        ExternalLoginInfo info,
        string returnUrl,
        string? accessToken,
        string? refreshToken,
        string? expiresAt)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var discordId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var discordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
        var avatarHash = info.Principal.FindFirstValue("urn:discord:avatar:hash");

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("External login did not provide email address");
            ErrorMessage = "Email address is required. Please ensure your Discord account has a verified email.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Check if a user with this email already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // Link the external login to the existing user
            _logger.LogInformation("Linking {Provider} login to existing user {Email}", info.LoginProvider, email);

            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                // Update Discord info
                await UpdateExistingUserDiscordInfoAsync(existingUser, discordId, discordUsername, avatarHash);

                // Store OAuth tokens BEFORE signing in if this is a Discord login
                if (info.LoginProvider == "Discord")
                {
                    await StoreOAuthTokensAsync(info, accessToken, refreshToken, expiresAt);
                }

                await _signInManager.SignInAsync(existingUser, isPersistent: true);
                _logger.LogInformation("User {Email} signed in after linking {Provider}", email, info.LoginProvider);

                return LocalRedirect(returnUrl);
            }

            foreach (var error in addLoginResult.Errors)
            {
                _logger.LogWarning("Error linking external login: {Error}", error.Description);
            }

            ErrorMessage = "Unable to link Discord account to existing user.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // Discord emails are verified
            DisplayName = discordUsername ?? email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Set Discord-specific fields
        if (ulong.TryParse(discordId, out var discordUserId))
        {
            user.DiscordUserId = discordUserId;
        }
        user.DiscordUsername = discordUsername;

        if (!string.IsNullOrEmpty(avatarHash) && !string.IsNullOrEmpty(discordId))
        {
            user.DiscordAvatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png";
        }

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                _logger.LogWarning("Error creating user: {Error}", error.Description);
            }

            ErrorMessage = "Unable to create user account. " + string.Join(" ", createResult.Errors.Select(e => e.Description));
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Add external login to the new user
        var addExternalLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addExternalLoginResult.Succeeded)
        {
            _logger.LogWarning("Failed to add external login to new user {Email}", email);
            // User was created but login wasn't linked - they can try again
            ErrorMessage = "Account created but Discord link failed. Please try logging in again.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        _logger.LogInformation("Created new user {Email} via {Provider} OAuth", email, info.LoginProvider);

        // Store OAuth tokens BEFORE signing in if this is a Discord login
        if (info.LoginProvider == "Discord")
        {
            await StoreOAuthTokensAsync(info, accessToken, refreshToken, expiresAt);
        }

        // Sign in the new user
        await _signInManager.SignInAsync(user, isPersistent: true);

        return LocalRedirect(returnUrl);
    }

    /// <summary>
    /// Updates Discord info for an existing user after successful external login.
    /// </summary>
    private async Task UpdateUserDiscordInfoAsync(ExternalLoginInfo info)
    {
        var userId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;

        // Find user by external login
        var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user == null)
            return;

        var discordUsername = info.Principal.FindFirstValue(ClaimTypes.Name);
        var avatarHash = info.Principal.FindFirstValue("urn:discord:avatar:hash");

        await UpdateExistingUserDiscordInfoAsync(user, userId, discordUsername, avatarHash);
    }

    /// <summary>
    /// Updates Discord-specific fields on an existing user.
    /// </summary>
    private async Task UpdateExistingUserDiscordInfoAsync(
        ApplicationUser user,
        string? discordId,
        string? discordUsername,
        string? avatarHash)
    {
        var needsUpdate = false;

        if (!string.IsNullOrEmpty(discordUsername) && user.DiscordUsername != discordUsername)
        {
            user.DiscordUsername = discordUsername;
            needsUpdate = true;
        }

        if (ulong.TryParse(discordId, out var discordUserId) && user.DiscordUserId != discordUserId)
        {
            user.DiscordUserId = discordUserId;
            needsUpdate = true;
        }

        if (!string.IsNullOrEmpty(avatarHash) && !string.IsNullOrEmpty(discordId))
        {
            var avatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png";
            if (user.DiscordAvatarUrl != avatarUrl)
            {
                user.DiscordAvatarUrl = avatarUrl;
                needsUpdate = true;
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        needsUpdate = true;

        if (needsUpdate)
        {
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Stores OAuth tokens for a Discord user.
    /// Tokens must be retrieved BEFORE calling SignInAsync as they come from the external auth cookie.
    /// </summary>
    /// <param name="info">The external login info from Discord.</param>
    /// <param name="accessToken">The OAuth access token.</param>
    /// <param name="refreshToken">The OAuth refresh token.</param>
    /// <param name="expiresAt">The token expiration time (ISO 8601 string).</param>
    private async Task StoreOAuthTokensAsync(ExternalLoginInfo info, string? accessToken, string? refreshToken, string? expiresAt)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Access token not available from {Provider} OAuth callback - user may have denied permissions", info.LoginProvider);
                return;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Refresh token not available from {Provider} OAuth callback", info.LoginProvider);
                return;
            }

            if (string.IsNullOrEmpty(expiresAt))
            {
                _logger.LogWarning("Token expiration time not available from {Provider} OAuth callback", info.LoginProvider);
                return;
            }

            // Parse Discord user ID
            var discordId = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ulong.TryParse(discordId, out var discordUserId))
            {
                _logger.LogWarning("Could not parse Discord user ID from external login claims");
                return;
            }

            // Find the ApplicationUser
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user == null)
            {
                _logger.LogWarning("Could not find user for external login {Provider}:{ProviderKey}", info.LoginProvider, info.ProviderKey);
                return;
            }

            // Parse expiration time (ISO 8601 format)
            if (!DateTime.TryParse(expiresAt, out var expirationTime))
            {
                _logger.LogWarning("Could not parse token expiration time: {ExpiresAt}", expiresAt);
                return;
            }

            // Get scopes - they should be in the authentication properties or we use the default
            var scopes = "identify email";
            _logger.LogDebug("Storing OAuth tokens for user {UserId}, expires at {ExpiresAt}", user.Id, expirationTime);

            // Store the tokens
            await _tokenService.StoreTokensAsync(
                user.Id,
                discordUserId,
                accessToken,
                refreshToken,
                expirationTime,
                scopes,
                HttpContext.RequestAborted);

            _logger.LogInformation("Successfully stored OAuth tokens for user {UserId}, Discord ID {DiscordUserId}", user.Id, discordUserId);
        }
        catch (Exception ex)
        {
            // Don't fail the login if token storage fails - user can still use the app
            _logger.LogError(ex, "Failed to store OAuth tokens for {Provider} login", info.LoginProvider);
        }
    }
}
