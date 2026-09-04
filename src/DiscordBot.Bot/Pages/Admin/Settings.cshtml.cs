using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Bot.ViewModels.Components;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Application Settings page.
/// Allows administrators to configure bot settings through a web UI.
/// Data loading, saving, and audit logging are delegated to <see cref="ISettingsSectionService"/>
/// (General/Features/Advanced/Commands), <see cref="IAppearanceSettingsService"/> (Appearance,
/// SuperAdmin only) and <see cref="IBotControlService"/> (Bot Control tab); this page model
/// only handles request routing and view-model assembly.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISettingsSectionService _settingsSectionService;
    private readonly IAppearanceSettingsService _appearanceSettingsService;
    private readonly IBotControlService _botControlService;
    private readonly ILogger<SettingsModel> _logger;

    /// <summary>
    /// Gets the view model for the page.
    /// </summary>
    public SettingsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the bot control view model for the Bot Control tab.
    /// </summary>
    public BotControlViewModel BotControlViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the reset category confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ResetCategoryModal { get; private set; } = null!;

    /// <summary>
    /// Gets the reset all confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ResetAllModal { get; private set; } = null!;

    /// <summary>
    /// Gets the restart confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel RestartModal { get; private set; } = null!;

    /// <summary>
    /// Gets the shutdown typed confirmation modal configuration.
    /// </summary>
    public TypedConfirmationModalViewModel ShutdownModal { get; private set; } = null!;

    /// <summary>
    /// Form property for settings data from the client.
    /// </summary>
    [BindProperty]
    public Dictionary<string, string> FormSettings { get; set; } = new();

    /// <summary>
    /// Form property for the active category.
    /// </summary>
    [BindProperty]
    public string? ActiveCategory { get; set; }

    /// <summary>
    /// Form property for command module enabled states.
    /// </summary>
    [BindProperty]
    public Dictionary<string, bool> CommandModules { get; set; } = new();

    /// <summary>
    /// Form property for the selected default theme ID.
    /// </summary>
    [BindProperty]
    public int? SelectedThemeId { get; set; }

    /// <summary>
    /// Gets whether the current user is a SuperAdmin (can access Appearance tab).
    /// </summary>
    public bool IsSuperAdmin { get; private set; }

    /// <summary>
    /// Gets the list of available themes for the dropdown.
    /// </summary>
    public IReadOnlyList<SelectListItem> AvailableThemes { get; private set; } = new List<SelectListItem>();

    /// <summary>
    /// Gets the current default theme.
    /// </summary>
    public DiscordBot.Core.DTOs.ThemeDto? CurrentDefaultTheme { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsModel"/> class.
    /// </summary>
    public SettingsModel(
        ISettingsSectionService settingsSectionService,
        IAppearanceSettingsService appearanceSettingsService,
        IBotControlService botControlService,
        ILogger<SettingsModel> logger)
    {
        _settingsSectionService = settingsSectionService;
        _appearanceSettingsService = appearanceSettingsService;
        _botControlService = botControlService;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Settings page.
    /// </summary>
    /// <param name="category">Optional category to display (defaults to General).</param>
    public async Task OnGetAsync(string? category = null)
    {
        _logger.LogDebug("Settings page accessed by user {UserId}", User.Identity?.Name);

        IsSuperAdmin = await _appearanceSettingsService.IsSuperAdminAsync(User);

        ActiveCategory = category ?? "General";

        // If user requested Appearance tab but isn't SuperAdmin, redirect to General
        if (ActiveCategory == "Appearance" && !IsSuperAdmin)
        {
            ActiveCategory = "General";
        }

        ViewModel = await _settingsSectionService.LoadViewModelAsync(ActiveCategory);
        BuildResetModals();

        if (IsSuperAdmin)
        {
            var themeData = await _appearanceSettingsService.LoadThemeDataAsync();
            AvailableThemes = themeData.AvailableThemes;
            CurrentDefaultTheme = themeData.CurrentDefaultTheme;
            SelectedThemeId = themeData.SelectedThemeId;
        }

        BotControlViewModel = _botControlService.LoadViewModel();
        BuildBotControlModals();
    }

    /// <summary>
    /// Handles POST requests to save settings for a specific category.
    /// </summary>
    /// <param name="category">The category to save.</param>
    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        _logger.LogInformation("Settings save requested for category {Category} by user {UserId}", category, User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.SaveCategoryAsync(category, FormSettings, userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to save all settings across all categories.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAllAsync()
    {
        _logger.LogInformation("Save all settings requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.SaveAllAsync(FormSettings, userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to reset a category to default values.
    /// </summary>
    /// <param name="category">The category to reset.</param>
    public async Task<IActionResult> OnPostResetCategoryAsync(string category)
    {
        _logger.LogWarning("Reset category {Category} requested by user {UserId}", category, User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.ResetCategoryAsync(category, userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to reset all settings to defaults.
    /// </summary>
    public async Task<IActionResult> OnPostResetAllAsync()
    {
        _logger.LogCritical("Reset ALL settings requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.ResetAllAsync(userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to save command module configurations.
    /// </summary>
    public async Task<IActionResult> OnPostSaveCommandModulesAsync()
    {
        _logger.LogInformation("Command module settings save requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.SaveCommandModulesAsync(CommandModules, userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to restart the bot.
    /// </summary>
    public async Task<IActionResult> OnPostRestartBotAsync()
    {
        _logger.LogWarning("Bot restart requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _botControlService.RestartAsync(userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to shutdown the bot.
    /// </summary>
    public async Task<IActionResult> OnPostShutdownBotAsync()
    {
        _logger.LogCritical("Bot SHUTDOWN requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _botControlService.ShutdownAsync(userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to save appearance settings (SuperAdmin only).
    /// </summary>
    public async Task<IActionResult> OnPostSaveAppearanceAsync()
    {
        _logger.LogInformation("Appearance settings save requested by user {UserId}", User.Identity?.Name);

        if (!await _appearanceSettingsService.IsSuperAdminAsync(User))
        {
            _logger.LogWarning("Unauthorized attempt to save appearance settings by user {UserId}", User.Identity?.Name);
            return new ForbidResult();
        }

        if (!SelectedThemeId.HasValue)
        {
            return new JsonResult(new { success = false, message = "No theme selected." }) { StatusCode = 400 };
        }

        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _appearanceSettingsService.SaveThemeAsync(SelectedThemeId.Value, userId);

        if (result.Success)
        {
            return new JsonResult(new
            {
                success = true,
                message = result.Message,
                themeName = result.ThemeName
            });
        }

        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to reset appearance settings to default (SuperAdmin only).
    /// </summary>
    public async Task<IActionResult> OnPostResetAppearanceAsync()
    {
        _logger.LogWarning("Appearance settings reset requested by user {UserId}", User.Identity?.Name);

        if (!await _appearanceSettingsService.IsSuperAdminAsync(User))
        {
            _logger.LogWarning("Unauthorized attempt to reset appearance settings by user {UserId}", User.Identity?.Name);
            return new ForbidResult();
        }

        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _appearanceSettingsService.ResetThemeAsync(userId);
        return ToJsonResult(result);
    }

    private static IActionResult ToJsonResult(SettingsSectionResult result)
    {
        if (result.Success)
        {
            return new JsonResult(new
            {
                success = true,
                message = result.Message,
                restartRequired = result.RestartRequired
            });
        }

        if (result.Errors is { Count: > 0 })
        {
            return new JsonResult(new
            {
                success = false,
                message = result.Message,
                errors = result.Errors
            })
            {
                StatusCode = result.StatusCode
            };
        }

        return new JsonResult(new
        {
            success = false,
            message = result.Message
        })
        {
            StatusCode = result.StatusCode
        };
    }

    private void BuildResetModals()
    {
        ResetCategoryModal = new ConfirmationModalViewModel
        {
            Id = "resetCategoryModal",
            Title = "Reset Category",
            Message = "Are you sure you want to reset this category to default values? This action cannot be undone.",
            ConfirmText = "Reset Category",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "ResetCategory"
        };

        ResetAllModal = new ConfirmationModalViewModel
        {
            Id = "resetAllModal",
            Title = "Reset All Settings",
            Message = "Are you sure you want to reset ALL settings to their default values? This will affect all categories and cannot be undone.",
            ConfirmText = "Reset All Settings",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormHandler = "ResetAll"
        };
    }

    private void BuildBotControlModals()
    {
        RestartModal = new ConfirmationModalViewModel
        {
            Id = "restartModal",
            Title = "Restart Bot",
            Message = "Are you sure you want to restart the bot? This will briefly disconnect the bot from all servers. The bot will automatically reconnect after a few seconds.",
            ConfirmText = "Restart Bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "RestartBot"
        };

        ShutdownModal = new TypedConfirmationModalViewModel
        {
            Id = "shutdownModal",
            Title = "Shutdown Bot",
            Message = "This action will completely shut down the bot. The bot will NOT restart automatically and will need to be manually started from the server. This action is critical and should only be used when necessary.",
            RequiredText = "SHUTDOWN",
            InputLabel = "Type SHUTDOWN to confirm",
            ConfirmText = "Shutdown Bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormHandler = "ShutdownBot"
        };
    }
}
