using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
    public IReadOnlyList<ThemeDto> AvailableThemes { get; set; } = Array.Empty<ThemeDto>();

    /// <summary>
    /// The theme the user chose, or null to follow the system's light or dark setting
    /// ("Match my system"). Posted by the theme radios; the empty value binds to null.
    /// </summary>
    [BindProperty]
    public int? SelectedThemeId { get; set; }

    /// <summary>
    /// The theme radios: "Match my system" first (value empty, which saves no choice), then each
    /// active theme.
    /// </summary>
    public RadioCardGroupViewModel ThemeChoices
    {
        get
        {
            const string systemIcon = "M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z";
            const string sunIcon = "M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z";
            const string moonIcon = "M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z";

            var options = new List<RadioCardViewModel>
            {
                new()
                {
                    Id = "theme-system",
                    Value = string.Empty,
                    Title = "Match my system",
                    Description = "Use light or dark to match your device, and switch when it does.",
                    IconPath = systemIcon
                }
            };
            options.AddRange(AvailableThemes.Select(theme => new RadioCardViewModel
            {
                Id = $"theme-{theme.Id}",
                Value = theme.Id.ToString(),
                Title = theme.DisplayName,
                Description = theme.Description,
                IconPath = theme.ThemeKey == ThemeAppearance.LightThemeKey ? sunIcon
                    : theme.ThemeKey == ThemeAppearance.DarkThemeKey ? moonIcon
                    : null
            }));

            return new RadioCardGroupViewModel
            {
                Id = "theme",
                Name = nameof(SelectedThemeId),
                Legend = "Color scheme",
                Columns = 1,
                Options = options,
                SelectedValue = SelectedThemeId?.ToString() ?? string.Empty
            };
        }
    }

    /// <summary>
    /// Whether the user has saved a theme choice. False means pages follow the browser's
    /// <c>prefers-color-scheme</c>.
    /// </summary>
    public bool HasSavedTheme => CurrentThemeSource == ThemeSource.User;

    /// <summary>
    /// The display name of the theme the user's saved choice names, or of the site default when
    /// no choice is saved.
    /// </summary>
    public string CurrentThemeName { get; set; } = string.Empty;

    /// <summary>
    /// The source of the current theme (User, Admin, System).
    /// </summary>
    public ThemeSource CurrentThemeSource { get; set; }

    /// <summary>
    /// Set across the redirect after the user chose "Match my system", so the page can clear the theme
    /// this browser remembered in localStorage (profile.js) and other open tabs follow along.
    /// </summary>
    [TempData]
    public bool ThemeCleared { get; set; }

    /// <summary>
    /// Page-state error shown when the theme preferences failed to load.
    /// Action results from the POST handler are shown as toasts instead.
    /// </summary>
    public string? ErrorMessage { get; set; }

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

        // A value that is not a number must not be mistaken for "no theme"
        if (!ModelState.IsValid)
        {
            TempData.SetErrorToast("The selected theme is not available.");
            return RedirectToPage();
        }

        // No theme selected means "Match my system": forget the saved choice so pages follow the
        // browser's light or dark setting again
        if (!SelectedThemeId.HasValue)
        {
            return await ClearThemePreferenceAsync(user.Id);
        }

        // Validate theme exists and is active
        var theme = await _themeService.GetThemeByIdAsync(SelectedThemeId.Value);
        if (theme == null || !theme.IsActive)
        {
            _logger.LogWarning("User {UserId} attempted to select invalid theme {ThemeId}",
                user.Id, SelectedThemeId.Value);
            TempData.SetErrorToast("The selected theme is not available.");
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

                TempData.SetSuccessToast("Theme preference saved successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to update theme preference for user {UserId}", user.Id);
                TempData.SetErrorToast("Failed to save theme preference. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving theme preference for user {UserId}", user.Id);
            TempData.SetErrorToast("An error occurred while saving your preferences.");
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> ClearThemePreferenceAsync(string userId)
    {
        _logger.LogInformation("User {UserId} clearing their theme preference to follow the system", userId);

        try
        {
            if (await _themeService.SetUserThemeAsync(userId, null))
            {
                Response.Cookies.Delete(IThemeService.ThemePreferenceCookieName);
                ThemeCleared = true;
                TempData.SetSuccessToast("Your theme now follows your system's light or dark setting.");
            }
            else
            {
                _logger.LogWarning("Failed to clear theme preference for user {UserId}", userId);
                TempData.SetErrorToast("Could not switch to your system's setting. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing theme preference for user {UserId}", userId);
            TempData.SetErrorToast("An error occurred while saving your preferences.");
        }

        return RedirectToPage();
    }

    private async Task LoadThemeDataAsync(string userId)
    {
        try
        {
            // Load available themes
            var themes = await _themeService.GetActiveThemesAsync();
            AvailableThemes = themes;

            // Load current theme. Only a saved choice is "selected"; otherwise the user follows the system.
            var currentTheme = await _themeService.GetUserThemeAsync(userId);
            CurrentThemeName = currentTheme.Theme.DisplayName;
            CurrentThemeSource = currentTheme.Source;
            SelectedThemeId = currentTheme.Source == ThemeSource.User ? currentTheme.Theme.Id : null;

            _logger.LogDebug("Loaded {ThemeCount} themes. Current theme: {ThemeName} (Source: {Source})",
                themes.Count, CurrentThemeName, CurrentThemeSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading theme data for user {UserId}", userId);
            AvailableThemes = Array.Empty<ThemeDto>();
            ErrorMessage = "Failed to load theme preferences.";
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
