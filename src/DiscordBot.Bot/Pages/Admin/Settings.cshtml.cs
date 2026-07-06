using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Application Settings page. The page hosts the
/// <see cref="Blazor.Pages.AdminSettingsIsland"/> Blazor island, which owns all
/// interactive behavior (tabs, save/reset, command modules, appearance, bot control).
/// This model only wires up the island parameters and the restart banner.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISettingsService _settingsService;
    private readonly ICommandModuleConfigurationService _commandModuleConfigurationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<SettingsModel> _logger;

    /// <summary>
    /// Gets the view model for the page (active category + restart-pending state for the banner).
    /// </summary>
    public SettingsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets whether the current user is a SuperAdmin (can access Appearance tab).
    /// </summary>
    public bool IsSuperAdmin { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsModel"/> class.
    /// </summary>
    public SettingsModel(
        ISettingsService settingsService,
        ICommandModuleConfigurationService commandModuleConfigurationService,
        IAuthorizationService authorizationService,
        ILogger<SettingsModel> logger)
    {
        _settingsService = settingsService;
        _commandModuleConfigurationService = commandModuleConfigurationService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Settings page.
    /// </summary>
    /// <param name="category">Optional category to display (defaults to General).</param>
    public async Task OnGetAsync(string? category = null)
    {
        _logger.LogDebug("Settings page accessed by user {UserId}", User.Identity?.Name);

        // Check if user is SuperAdmin for Appearance tab access
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireSuperAdmin");
        IsSuperAdmin = authResult.Succeeded;

        var activeCategory = category ?? "General";

        // If user requested Appearance tab but isn't SuperAdmin, redirect to General
        if (activeCategory == "Appearance" && !IsSuperAdmin)
        {
            activeCategory = "General";
        }

        // Determine if restart is pending from either settings or command modules
        var isRestartPending = _settingsService.IsRestartPending || _commandModuleConfigurationService.IsRestartPending;

        ViewModel = new SettingsViewModel
        {
            ActiveCategory = activeCategory,
            IsRestartPending = isRestartPending
        };
    }
}
