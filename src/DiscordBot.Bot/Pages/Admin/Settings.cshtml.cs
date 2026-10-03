using DiscordBot.Bot.Extensions;
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
    /// <summary>The tabs, in display order, by the id used in <c>?category=</c>.</summary>
    public static readonly IReadOnlyList<string> Tabs =
        new[] { "General", "Features", "Commands", "Advanced", "BotControl", "AiModels", "Appearance" };

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

        ActiveCategory = ResolveTab(category, IsSuperAdmin);

        ViewModel = await _settingsSectionService.LoadViewModelAsync(ActiveCategory);

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
        if (await IsAppearanceRefusedAsync(category))
        {
            return new ForbidResult();
        }

        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.SaveCategoryAsync(category, FormSettings, userId);
        return ToJsonResult(result);
    }

    /// <summary>
    /// Handles POST requests to reset a category to default values. The values on the page
    /// change, so this redirects (the confirm dialog reloads the page once) and reports the
    /// outcome as a toast that survives the reload.
    /// </summary>
    /// <param name="category">The category to reset.</param>
    public async Task<IActionResult> OnPostResetCategoryAsync(string category)
    {
        _logger.LogWarning("Reset category {Category} requested by user {UserId}", category, User.Identity?.Name);
        if (await IsAppearanceRefusedAsync(category))
        {
            return new ForbidResult();
        }

        var userId = User.Identity?.Name ?? "Unknown";
        var result = await _settingsSectionService.ResetCategoryAsync(category, userId);
        return RedirectWithToast(result, category);
    }

    /// <summary>
    /// Handles POST requests to reset all settings to defaults.
    /// </summary>
    public async Task<IActionResult> OnPostResetAllAsync(string? category = null)
    {
        _logger.LogCritical("Reset ALL settings requested by user {UserId}", User.Identity?.Name);
        var userId = User.Identity?.Name ?? "Unknown";

        // Appearance (the default theme) is SuperAdmin-only everywhere else, so "reset all" leaves it
        // alone for anyone else, as the per-category handlers refuse it
        var includeAppearance = await _appearanceSettingsService.IsSuperAdminAsync(User);
        var result = await _settingsSectionService.ResetAllAsync(userId, includeAppearance: includeAppearance);
        return RedirectWithToast(result, category);
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
        return RedirectWithToast(result, "Appearance");
    }

    /// <summary>
    /// True when <paramref name="category"/> is Appearance (the default theme) and the caller is not
    /// a SuperAdmin. The generic save and reset handlers take any category name, so they must not
    /// become a way around the SuperAdmin-only Appearance handlers.
    /// </summary>
    private async Task<bool> IsAppearanceRefusedAsync(string? category)
    {
        // Parse the way the service does (it accepts numeric ids too), so no spelling slips past
        if (!Enum.TryParse<DiscordBot.Core.Enums.SettingCategory>(category, ignoreCase: true, out var parsed)
            || parsed != DiscordBot.Core.Enums.SettingCategory.Appearance
            || await _appearanceSettingsService.IsSuperAdminAsync(User))
        {
            return false;
        }

        _logger.LogWarning("Unauthorized attempt to change Appearance settings through the generic handler by user {UserId}", User.Identity?.Name);
        return true;
    }

    private static IActionResult ToJsonResult(SettingsSectionResult result)
    {
        if (result.Success)
        {
            return new JsonResult(new
            {
                success = true,
                message = result.Message,
                changeCount = result.ChangeCount,
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

    /// <summary>
    /// The tab to show for a <c>?category=</c> value: the value when it names a tab this user can
    /// see, otherwise General. Appearance is for SuperAdmins only.
    /// </summary>
    public static string ResolveTab(string? category, bool isSuperAdmin)
    {
        var match = Tabs.FirstOrDefault(t => string.Equals(t, category, StringComparison.OrdinalIgnoreCase));
        if (match == null || (match == "Appearance" && !isSuperAdmin))
        {
            return "General";
        }

        return match;
    }

    private IActionResult RedirectWithToast(SettingsSectionResult result, string? category)
    {
        if (result.Success)
        {
            TempData.SetSuccessToast(result.Message);
        }
        else
        {
            var detail = result.Errors is { Count: > 0 } ? $" {string.Join(" ", result.Errors)}" : string.Empty;
            TempData.SetErrorToast(result.Message + detail);
        }

        // The tab name is user input; keep it only when it names a tab, so it cannot steer the redirect.
        var tab = Tabs.FirstOrDefault(t => string.Equals(t, category, StringComparison.OrdinalIgnoreCase)) ?? "General";
        return RedirectToPage(new { category = tab });
    }

    private void BuildBotControlModals()
    {
        RestartModal = new ConfirmationModalViewModel
        {
            Id = "restartModal",
            Title = "Restart the bot?",
            Message = "The bot will disconnect from every server for a few seconds, then reconnect by itself.",
            ConfirmText = "Restart bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "RestartBot"
        };

        ShutdownModal = new TypedConfirmationModalViewModel
        {
            Id = "shutdownModal",
            Title = "Shut down the bot?",
            Message = "The bot will stop completely and will not come back by itself. Someone has to start it again on the server.",
            RequiredText = "SHUTDOWN",
            InputLabel = "Type SHUTDOWN to confirm",
            ConfirmText = "Shut down bot",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormHandler = "ShutdownBot"
        };
    }
}
