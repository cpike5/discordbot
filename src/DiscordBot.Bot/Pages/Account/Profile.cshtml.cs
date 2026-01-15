using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for managing user profile settings including theme preferences.
/// </summary>
[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IThemeService _themeService;
    private readonly ILogger<ProfileModel> _logger;

    public ProfileModel(
        UserManager<ApplicationUser> userManager,
        IThemeService themeService,
        ILogger<ProfileModel> logger)
    {
        _userManager = userManager;
        _themeService = themeService;
        _logger = logger;
    }

    /// <summary>
    /// The display name of the current user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The email of the current user.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Whether the user has a linked Discord account.
    /// </summary>
    public bool HasDiscordLinked { get; set; }

    /// <summary>
    /// The user's Discord username if linked.
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// The user's Discord ID if linked.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// The user's Discord avatar URL if linked.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// The user's highest role name (e.g., SuperAdmin, Admin, Moderator, Viewer).
    /// </summary>
    public string UserRole { get; set; } = "Viewer";

    /// <summary>
    /// The date the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Available themes for selection.
    /// </summary>
    public SelectList AvailableThemes { get; set; } = null!;

    /// <summary>
    /// The currently selected theme ID.
    /// </summary>
    [BindProperty]
    public int? SelectedThemeId { get; set; }

    /// <summary>
    /// The current theme's display name.
    /// </summary>
    public string CurrentThemeName { get; set; } = string.Empty;

    /// <summary>
    /// The source of the current theme (User, Admin, System).
    /// </summary>
    public ThemeSource CurrentThemeSource { get; set; }

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
    /// Handles GET requests to display the profile page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnGetAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during Profile page load");
            return NotFound("User not found.");
        }

        _logger.LogDebug("Loading profile for user {UserId}", user.Id);

        DisplayName = user.DisplayName ?? user.Email ?? "User";
        Email = user.Email;

        // Discord account info
        HasDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUsername = user.DiscordUsername;
        DiscordUserId = user.DiscordUserId;
        DiscordAvatarUrl = user.DiscordAvatarUrl;

        // Account dates
        CreatedAt = user.CreatedAt;
        LastLoginAt = user.LastLoginAt;

        // Get the user's highest role
        var roles = await _userManager.GetRolesAsync(user);
        UserRole = GetHighestRole(roles);

        await LoadThemeDataAsync(user.Id);

        return Page();
    }

    /// <summary>
    /// Handles POST requests to save the user's theme preference.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        _logger.LogTrace("Entering {MethodName} with SelectedThemeId={ThemeId}",
            nameof(OnPostAsync), SelectedThemeId);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during profile save");
            return NotFound("User not found.");
        }

        // Validate that a theme is selected
        if (!SelectedThemeId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to save without selecting a theme", user.Id);
            StatusMessage = "Please select a theme.";
            IsSuccess = false;
            return RedirectToPage();
        }

        // Validate theme exists and is active
        var theme = await _themeService.GetThemeByIdAsync(SelectedThemeId.Value);
        if (theme == null || !theme.IsActive)
        {
            _logger.LogWarning("User {UserId} attempted to select invalid theme {ThemeId}",
                user.Id, SelectedThemeId.Value);
            StatusMessage = "The selected theme is not available.";
            IsSuccess = false;
            return RedirectToPage();
        }

        _logger.LogInformation("User {UserId} setting theme preference to {ThemeId} ({ThemeName})",
            user.Id, theme.Id, theme.DisplayName);

        try
        {
            var success = await _themeService.SetUserThemeAsync(user.Id, SelectedThemeId.Value);

            if (success)
            {
                // Set cookie for SSR on next page load
                Response.Cookies.Append(IThemeService.ThemePreferenceCookieName, theme.ThemeKey, new CookieOptions
                {
                    Path = "/",
                    MaxAge = TimeSpan.FromDays(365),
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true
                });

                _logger.LogInformation("Successfully updated theme preference for user {UserId} to {ThemeName}",
                    user.Id, theme.DisplayName);

                StatusMessage = "Theme preference saved successfully.";
                IsSuccess = true;
            }
            else
            {
                _logger.LogWarning("Failed to update theme preference for user {UserId}", user.Id);
                StatusMessage = "Failed to save theme preference. Please try again.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving theme preference for user {UserId}", user.Id);
            StatusMessage = "An error occurred while saving your preferences.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    private async Task LoadThemeDataAsync(string userId)
    {
        try
        {
            // Load available themes
            var themes = await _themeService.GetActiveThemesAsync();
            AvailableThemes = new SelectList(themes, nameof(ThemeDto.Id), nameof(ThemeDto.DisplayName));

            // Load current theme
            var currentTheme = await _themeService.GetUserThemeAsync(userId);
            SelectedThemeId = currentTheme.Theme.Id;
            CurrentThemeName = currentTheme.Theme.DisplayName;
            CurrentThemeSource = currentTheme.Source;

            _logger.LogDebug("Loaded {ThemeCount} themes. Current theme: {ThemeName} (Source: {Source})",
                themes.Count, CurrentThemeName, CurrentThemeSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading theme data for user {UserId}", userId);
            AvailableThemes = new SelectList(Enumerable.Empty<ThemeDto>(), nameof(ThemeDto.Id), nameof(ThemeDto.DisplayName));
            StatusMessage = "Failed to load theme preferences.";
            IsSuccess = false;
        }
    }

    /// <summary>
    /// Gets the highest role from the user's role list based on role hierarchy.
    /// </summary>
    private static string GetHighestRole(IList<string> roles)
    {
        // Role hierarchy: SuperAdmin > Admin > Moderator > Viewer
        if (roles.Contains("SuperAdmin")) return "SuperAdmin";
        if (roles.Contains("Admin")) return "Admin";
        if (roles.Contains("Moderator")) return "Moderator";
        return "Viewer";
    }
}
