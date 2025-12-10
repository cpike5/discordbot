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
/// Allows authenticated users to manage their Discord OAuth connection.
/// </summary>
[Authorize]
public class LinkDiscordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IDiscordTokenService _tokenService;
    private readonly IDiscordUserInfoService _userInfoService;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly DiscordOAuthSettings _oauthSettings;
    private readonly ILogger<LinkDiscordModel> _logger;

    public LinkDiscordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IDiscordTokenService tokenService,
        IDiscordUserInfoService userInfoService,
        IGuildMembershipService guildMembershipService,
        DiscordOAuthSettings oauthSettings,
        ILogger<LinkDiscordModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _userInfoService = userInfoService;
        _guildMembershipService = guildMembershipService;
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
}
