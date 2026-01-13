using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for linking and unlinking Discord accounts from user profiles.
/// Allows authenticated users to manage their Discord OAuth connection and bot verification.
/// </summary>
[Authorize]
public class LinkDiscordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IDiscordTokenService _tokenService;
    private readonly IDiscordUserInfoService _userInfoService;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly IVerificationService _verificationService;
    private readonly DiscordOAuthSettings _oauthSettings;
    private readonly ILogger<LinkDiscordModel> _logger;

    public LinkDiscordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IDiscordTokenService tokenService,
        IDiscordUserInfoService userInfoService,
        IGuildMembershipService guildMembershipService,
        IUserDiscordGuildService userDiscordGuildService,
        IVerificationService verificationService,
        DiscordOAuthSettings oauthSettings,
        ILogger<LinkDiscordModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _userInfoService = userInfoService;
        _guildMembershipService = guildMembershipService;
        _userDiscordGuildService = userDiscordGuildService;
        _verificationService = verificationService;
        _oauthSettings = oauthSettings;
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether the current user has a Discord account linked.
    /// </summary>
    public bool IsDiscordLinked { get; set; }

    /// <summary>
    /// Indicates whether Discord OAuth is configured and available.
    /// </summary>
    public bool IsDiscordOAuthConfigured => _oauthSettings.IsConfigured;

    /// <summary>
    /// The Discord username of the linked account.
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// URL to the Discord avatar image.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// The Discord user ID (snowflake) of the linked account.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// Indicates whether the user has a valid OAuth token.
    /// </summary>
    public bool HasValidToken { get; set; }

    /// <summary>
    /// List of guilds where the user has administrative permissions.
    /// </summary>
    public IReadOnlyList<DiscordGuildDto> UserGuilds { get; set; } = Array.Empty<DiscordGuildDto>();

    /// <summary>
    /// Indicates whether the user has a pending bot verification.
    /// </summary>
    public bool HasPendingVerification { get; set; }

    /// <summary>
    /// The pending verification code information, if any.
    /// </summary>
    public VerificationCode? PendingVerification { get; set; }

    /// <summary>
    /// The verification code entered by the user.
    /// </summary>
    [BindProperty]
    public string? VerificationCode { get; set; }

    /// <summary>
    /// Status message to display to the user (success or error).
    /// </summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Indicates whether the status message is a success message.
    /// </summary>
    [TempData]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Handles GET requests to display the Discord link status page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnGetAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during LinkDiscord page load");
            return NotFound("User not found.");
        }

        _logger.LogDebug("Loading Discord link status for user {UserId}", user.Id);

        // Check if Discord is linked
        IsDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUserId = user.DiscordUserId;
        DiscordUsername = user.DiscordUsername;
        DiscordAvatarUrl = user.DiscordAvatarUrl;

        if (IsDiscordLinked)
        {
            _logger.LogDebug("User {UserId} has Discord linked (Discord ID: {DiscordUserId})", user.Id, user.DiscordUserId);

            // Check if valid token exists
            try
            {
                HasValidToken = await _tokenService.HasValidTokenAsync(user.Id);
                _logger.LogDebug("User {UserId} has valid token: {HasValidToken}", user.Id, HasValidToken);

                // If has valid token, fetch guilds
                if (HasValidToken)
                {
                    _logger.LogDebug("Fetching administered guilds for user {UserId}", user.Id);
                    UserGuilds = await _guildMembershipService.GetAdministeredGuildsAsync(user.Id);
                    _logger.LogInformation("User {UserId} has administrative access to {GuildCount} guilds", user.Id, UserGuilds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token validity or fetching guilds for user {UserId}", user.Id);
                // Continue to render page even if token check fails
            }
        }
        else
        {
            _logger.LogDebug("User {UserId} does not have Discord linked", user.Id);

            // Check for pending verification
            PendingVerification = await _verificationService.GetPendingVerificationAsync(user.Id);
            HasPendingVerification = PendingVerification != null;

            if (HasPendingVerification)
            {
                _logger.LogDebug("User {UserId} has pending verification (ID: {VerificationId}, Expires: {ExpiresAt})",
                    user.Id, PendingVerification!.Id, PendingVerification.ExpiresAt);
            }
        }

        return Page();
    }

    /// <summary>
    /// Handles POST requests to initiate Discord OAuth linking flow.
    /// </summary>
    public IActionResult OnPostLinkAsync()
    {
        if (!_oauthSettings.IsConfigured)
        {
            _logger.LogWarning("Discord OAuth link attempted but OAuth is not configured");
            StatusMessage = "Discord OAuth is not configured on this server.";
            IsSuccess = false;
            return RedirectToPage();
        }

        _logger.LogInformation("User {UserId} initiating Discord OAuth link", _userManager.GetUserId(User));

        // Build return URL to this page
        var returnUrl = Url.Page("./LinkDiscord");

        // Configure external authentication properties
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            "Discord",
            Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl }));

        return new ChallengeResult("Discord", properties);
    }

    /// <summary>
    /// Handles POST requests to unlink Discord account from user profile.
    /// </summary>
    public async Task<IActionResult> OnPostUnlinkAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostUnlinkAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during Discord unlink");
            return NotFound("User not found.");
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to unlink Discord but no Discord account is linked", user.Id);
            StatusMessage = "No Discord account is currently linked.";
            IsSuccess = false;
            return RedirectToPage();
        }

        var discordUserId = user.DiscordUserId.Value;
        _logger.LogInformation("User {UserId} unlinking Discord account (Discord ID: {DiscordUserId})", user.Id, discordUserId);

        try
        {
            // Clear Discord-specific fields
            user.DiscordUserId = null;
            user.DiscordUsername = null;
            user.DiscordAvatarUrl = null;

            // Delete OAuth tokens
            await _tokenService.DeleteTokensAsync(user.Id);
            _logger.LogDebug("Deleted OAuth tokens for user {UserId}", user.Id);

            // Delete stored guild memberships
            await _userDiscordGuildService.DeleteUserGuildsAsync(user.Id);
            _logger.LogDebug("Deleted stored guild memberships for user {UserId}", user.Id);

            // Invalidate cache
            _userInfoService.InvalidateCache(user.Id);
            _logger.LogDebug("Invalidated Discord user info cache for user {UserId}", user.Id);

            // Remove external login
            var logins = await _userManager.GetLoginsAsync(user);
            var discordLogin = logins.FirstOrDefault(l => l.LoginProvider == "Discord");
            if (discordLogin != null)
            {
                var removeLoginResult = await _userManager.RemoveLoginAsync(user, discordLogin.LoginProvider, discordLogin.ProviderKey);
                if (removeLoginResult.Succeeded)
                {
                    _logger.LogDebug("Removed Discord external login for user {UserId}", user.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to remove Discord external login for user {UserId}: {Errors}",
                        user.Id, string.Join(", ", removeLoginResult.Errors.Select(e => e.Description)));
                }
            }

            // Update user
            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                _logger.LogInformation("Successfully unlinked Discord account for user {UserId}", user.Id);
                StatusMessage = "Discord account unlinked successfully.";
                IsSuccess = true;
            }
            else
            {
                _logger.LogError("Failed to update user {UserId} after unlinking Discord: {Errors}",
                    user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                StatusMessage = "Failed to unlink Discord account. Please try again.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking Discord account for user {UserId}", user.Id);
            StatusMessage = "An error occurred while unlinking Discord account.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to initiate bot verification.
    /// Creates a pending verification record for the user.
    /// </summary>
    public async Task<IActionResult> OnPostInitiateBotVerificationAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostInitiateBotVerificationAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during bot verification initiation");
            return NotFound("User not found.");
        }

        _logger.LogInformation("User {UserId} initiating bot verification", user.Id);

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _verificationService.InitiateVerificationAsync(user.Id, ipAddress);

            if (result.Succeeded)
            {
                _logger.LogInformation("Bot verification initiated successfully for user {UserId}, verification ID: {VerificationId}",
                    user.Id, result.VerificationId);
                StatusMessage = "Verification initiated. Run /verify-account in Discord to continue.";
                IsSuccess = true;
            }
            else
            {
                _logger.LogWarning("Failed to initiate bot verification for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);
                StatusMessage = result.ErrorMessage ?? "Failed to initiate verification.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating bot verification for user {UserId}", user.Id);
            StatusMessage = "An error occurred while initiating verification.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to verify a code entered by the user.
    /// Links the Discord account if the code is valid.
    /// </summary>
    public async Task<IActionResult> OnPostVerifyCodeAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostVerifyCodeAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during code verification");
            return NotFound("User not found.");
        }

        if (string.IsNullOrWhiteSpace(VerificationCode))
        {
            _logger.LogWarning("User {UserId} submitted empty verification code", user.Id);
            StatusMessage = "Please enter a verification code.";
            IsSuccess = false;
            return RedirectToPage();
        }

        // Remove any formatting (hyphens, spaces) from the code
        var cleanCode = VerificationCode.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        _logger.LogInformation("User {UserId} attempting to verify code", user.Id);

        try
        {
            var result = await _verificationService.ValidateCodeAsync(user.Id, cleanCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("Code verified successfully for user {UserId}, linked Discord user {DiscordUserId}",
                    user.Id, result.LinkedDiscordUserId);
                StatusMessage = $"Discord account successfully linked! Welcome, {result.LinkedDiscordUsername ?? "Discord User"}!";
                IsSuccess = true;
            }
            else
            {
                _logger.LogWarning("Code verification failed for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);
                StatusMessage = result.ErrorMessage ?? "Invalid verification code.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying code for user {UserId}", user.Id);
            StatusMessage = "An error occurred while verifying the code.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to cancel pending verification.
    /// </summary>
    public async Task<IActionResult> OnPostCancelVerificationAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostCancelVerificationAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during verification cancellation");
            return NotFound("User not found.");
        }

        _logger.LogInformation("User {UserId} cancelling pending verification", user.Id);

        try
        {
            await _verificationService.CancelPendingVerificationAsync(user.Id);
            _logger.LogInformation("Verification cancelled successfully for user {UserId}", user.Id);
            StatusMessage = "Verification cancelled.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling verification for user {UserId}", user.Id);
            StatusMessage = "An error occurred while cancelling verification.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to refresh Discord guild data from the Discord API.
    /// Fetches the latest guild memberships and updates the local cache.
    /// </summary>
    public async Task<IActionResult> OnPostRefreshDiscordDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostRefreshDiscordDataAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during Discord data refresh");
            return NotFound("User not found.");
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to refresh Discord data but no Discord account is linked", user.Id);
            StatusMessage = "No Discord account is currently linked.";
            IsSuccess = false;
            return RedirectToPage();
        }

        _logger.LogInformation("User {UserId} refreshing Discord guild data", user.Id);

        try
        {
            await _userDiscordGuildService.RefreshUserGuildsAsync(user.Id, cancellationToken);

            // Invalidate user info cache for consistency
            _userInfoService.InvalidateCache(user.Id);

            _logger.LogInformation("User {UserId} refreshed Discord guild data successfully", user.Id);
            StatusMessage = "Discord data refreshed successfully.";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Discord data for user {UserId}", user.Id);
            StatusMessage = "An error occurred while refreshing Discord data. Please try again.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }
}
