using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Enums;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Application Settings page.
/// Allows administrators to configure bot settings through a web UI.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISettingsService _settingsService;
    private readonly ICommandModuleConfigurationService _commandModuleConfigurationService;
    private readonly IThemeService _themeService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly IBotService _botService;
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
    public ThemeDto? CurrentDefaultTheme { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsModel"/> class.
    /// </summary>
    public SettingsModel(
        ISettingsService settingsService,
        ICommandModuleConfigurationService commandModuleConfigurationService,
        IThemeService themeService,
        IAuthorizationService authorizationService,
        IAuditLogQueue auditLogQueue,
        IBotService botService,
        ILogger<SettingsModel> logger)
    {
        _settingsService = settingsService;
        _commandModuleConfigurationService = commandModuleConfigurationService;
        _themeService = themeService;
        _authorizationService = authorizationService;
        _auditLogQueue = auditLogQueue;
        _botService = botService;
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

        ActiveCategory = category ?? "General";

        // If user requested Appearance tab but isn't SuperAdmin, redirect to General
        if (ActiveCategory == "Appearance" && !IsSuperAdmin)
        {
            ActiveCategory = "General";
        }

        await LoadViewModelAsync();
        await LoadThemeDataAsync();
        LoadBotControlViewModel();
    }

    /// <summary>
    /// Handles POST requests to save settings for a specific category.
    /// </summary>
    /// <param name="category">The category to save.</param>
    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        _logger.LogInformation("Settings save requested for category {Category} by user {UserId}", category, User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";
            var updateDto = new SettingsUpdateDto { Settings = FormSettings };

            var result = await _settingsService.UpdateSettingsAsync(updateDto, userId);

            if (result.Success)
            {
                _logger.LogInformation("Settings saved successfully for category {Category} by user {UserId}. Updated keys: {Keys}",
                    category, userId, string.Join(", ", result.UpdatedKeys));

                // Audit log the settings change with actual before/after values
                if (result.Changes.Count > 0)
                {
                    _auditLogQueue.Enqueue(new AuditLogCreateDto
                    {
                        Category = AuditLogCategory.Configuration,
                        Action = AuditLogAction.SettingChanged,
                        ActorType = AuditLogActorType.User,
                        ActorId = userId,
                        Details = JsonSerializer.Serialize(new
                        {
                            SettingsCategory = category,
                            Changes = result.Changes.Select(c => new
                            {
                                Key = c.Key,
                                DisplayName = c.Value.DisplayName,
                                OldValue = c.Value.OldValue,
                                NewValue = c.Value.NewValue
                            }),
                            RestartRequired = result.RestartRequired
                        })
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = result.Changes.Count > 0
                        ? $"Settings saved successfully. {result.Changes.Count} setting(s) updated."
                        : "No changes detected.",
                    restartRequired = result.RestartRequired
                });
            }
            else
            {
                _logger.LogWarning("Settings save failed for category {Category} by user {UserId}. Errors: {Errors}",
                    category, userId, string.Join(", ", result.Errors));

                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to save settings.",
                    errors = result.Errors
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving settings for category {Category}, requested by {UserId}",
                category, User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while saving settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to save all settings across all categories.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAllAsync()
    {
        _logger.LogInformation("Save all settings requested by user {UserId}", User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";
            var updateDto = new SettingsUpdateDto { Settings = FormSettings };

            var result = await _settingsService.UpdateSettingsAsync(updateDto, userId);

            if (result.Success)
            {
                _logger.LogInformation("All settings saved successfully by user {UserId}. Updated keys: {Keys}",
                    userId, string.Join(", ", result.UpdatedKeys));

                // Audit log the settings change with actual before/after values
                if (result.Changes.Count > 0)
                {
                    _auditLogQueue.Enqueue(new AuditLogCreateDto
                    {
                        Category = AuditLogCategory.Configuration,
                        Action = AuditLogAction.SettingChanged,
                        ActorType = AuditLogActorType.User,
                        ActorId = userId,
                        Details = JsonSerializer.Serialize(new
                        {
                            SettingsCategory = "All",
                            Changes = result.Changes.Select(c => new
                            {
                                Key = c.Key,
                                DisplayName = c.Value.DisplayName,
                                OldValue = c.Value.OldValue,
                                NewValue = c.Value.NewValue
                            }),
                            RestartRequired = result.RestartRequired
                        })
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = result.Changes.Count > 0
                        ? $"All settings saved successfully. {result.Changes.Count} setting(s) updated."
                        : "No changes detected.",
                    restartRequired = result.RestartRequired
                });
            }
            else
            {
                _logger.LogWarning("Save all settings failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));

                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to save all settings.",
                    errors = result.Errors
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving all settings, requested by {UserId}",
                User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while saving settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to reset a category to default values.
    /// </summary>
    /// <param name="category">The category to reset.</param>
    public async Task<IActionResult> OnPostResetCategoryAsync(string category)
    {
        _logger.LogWarning("Reset category {Category} requested by user {UserId}", category, User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            if (!Enum.TryParse<SettingCategory>(category, out var categoryEnum))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = $"Invalid category: {category}"
                })
                {
                    StatusCode = 400
                };
            }

            var result = await _settingsService.ResetCategoryAsync(categoryEnum, userId);

            if (result.Success)
            {
                _logger.LogInformation("Category {Category} reset to defaults by user {UserId}", category, userId);

                // Audit log the category reset
                _auditLogQueue.Enqueue(new AuditLogCreateDto
                {
                    Category = AuditLogCategory.Configuration,
                    Action = AuditLogAction.SettingChanged,
                    ActorType = AuditLogActorType.User,
                    ActorId = userId,
                    Details = JsonSerializer.Serialize(new
                    {
                        Operation = "ResetCategory",
                        SettingsCategory = category,
                        RestartRequired = result.RestartRequired
                    })
                });

                return new JsonResult(new
                {
                    success = true,
                    message = $"{category} settings have been reset to defaults.",
                    restartRequired = result.RestartRequired
                });
            }
            else
            {
                _logger.LogWarning("Reset category {Category} failed for user {UserId}. Errors: {Errors}",
                    category, userId, string.Join(", ", result.Errors));

                return new JsonResult(new
                {
                    success = false,
                    message = $"Failed to reset {category} settings.",
                    errors = result.Errors
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting category {Category}, requested by {UserId}",
                category, User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while resetting settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to reset all settings to defaults.
    /// </summary>
    public async Task<IActionResult> OnPostResetAllAsync()
    {
        _logger.LogCritical("Reset ALL settings requested by user {UserId}", User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";
            var result = await _settingsService.ResetAllAsync(userId);

            if (result.Success)
            {
                _logger.LogWarning("All settings reset to defaults by user {UserId}", userId);

                // Audit log the full reset
                _auditLogQueue.Enqueue(new AuditLogCreateDto
                {
                    Category = AuditLogCategory.Configuration,
                    Action = AuditLogAction.SettingChanged,
                    ActorType = AuditLogActorType.User,
                    ActorId = userId,
                    Details = JsonSerializer.Serialize(new
                    {
                        Operation = "ResetAll",
                        RestartRequired = result.RestartRequired
                    })
                });

                return new JsonResult(new
                {
                    success = true,
                    message = "All settings have been reset to defaults.",
                    restartRequired = result.RestartRequired
                });
            }
            else
            {
                _logger.LogWarning("Reset all settings failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));

                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to reset all settings.",
                    errors = result.Errors
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting all settings, requested by {UserId}",
                User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while resetting settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to save command module configurations.
    /// </summary>
    public async Task<IActionResult> OnPostSaveCommandModulesAsync()
    {
        _logger.LogInformation("Command module settings save requested by user {UserId}", User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            // Get current states for audit logging
            var currentModules = await _commandModuleConfigurationService.GetAllModulesAsync();
            var currentStates = currentModules.ToDictionary(m => m.ModuleName, m => m.IsEnabled);

            // Prepare the update DTO
            var updateDto = new CommandModuleConfigurationUpdateDto
            {
                Modules = CommandModules
            };

            var result = await _commandModuleConfigurationService.UpdateModulesAsync(updateDto, userId);

            if (result.Success || result.UpdatedModules.Count > 0)
            {
                _logger.LogInformation("Command module settings saved successfully by user {UserId}. Updated modules: {Modules}",
                    userId, string.Join(", ", result.UpdatedModules));

                // Audit log each module change (issue #1084)
                foreach (var moduleName in result.UpdatedModules)
                {
                    var previousState = currentStates.GetValueOrDefault(moduleName, true);
                    var newState = CommandModules.GetValueOrDefault(moduleName, true);

                    _auditLogQueue.Enqueue(new AuditLogCreateDto
                    {
                        Category = AuditLogCategory.Configuration,
                        Action = AuditLogAction.SettingChanged,
                        ActorType = AuditLogActorType.User,
                        ActorId = userId,
                        Details = JsonSerializer.Serialize(new
                        {
                            SettingsCategory = "Commands",
                            ModuleName = moduleName,
                            Change = new
                            {
                                Key = $"CommandModule:{moduleName}:IsEnabled",
                                DisplayName = $"Command module '{moduleName}'",
                                OldValue = previousState.ToString(),
                                NewValue = newState.ToString()
                            },
                            Description = $"Command module '{moduleName}' {(newState ? "enabled" : "disabled")}",
                            RestartRequired = result.RequiresRestart
                        })
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = result.UpdatedModules.Count > 0
                        ? $"Command module settings saved successfully. {result.UpdatedModules.Count} module(s) updated."
                        : "No changes detected.",
                    restartRequired = result.RequiresRestart
                });
            }
            else
            {
                _logger.LogWarning("Command module settings save failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));

                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to save command module settings.",
                    errors = result.Errors
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving command module settings, requested by {UserId}",
                User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while saving command module settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to restart the bot.
    /// </summary>
    public async Task<IActionResult> OnPostRestartBotAsync()
    {
        _logger.LogWarning("Bot restart requested by user {UserId}", User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            await _botService.RestartAsync();
            _logger.LogInformation("Bot restart completed successfully, initiated by {UserId}", userId);

            // Audit log the bot restart
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.Updated,
                ActorType = AuditLogActorType.User,
                ActorId = userId,
                Details = JsonSerializer.Serialize(new
                {
                    Operation = "BotRestart",
                    Description = "Bot restart initiated by administrator",
                    Timestamp = DateTime.UtcNow
                })
            });

            return new JsonResult(new
            {
                success = true,
                message = "Bot is restarting..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to restart bot. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to shutdown the bot.
    /// </summary>
    public async Task<IActionResult> OnPostShutdownBotAsync()
    {
        _logger.LogCritical("Bot SHUTDOWN requested by user {UserId}", User.Identity?.Name);

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            await _botService.ShutdownAsync();
            _logger.LogCritical("Bot shutdown initiated by {UserId}", userId);

            // Audit log the bot shutdown
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotStopped,
                ActorType = AuditLogActorType.User,
                ActorId = userId,
                Details = JsonSerializer.Serialize(new
                {
                    Operation = "ManualShutdown",
                    Reason = "Administrator initiated shutdown",
                    Timestamp = DateTime.UtcNow
                })
            });

            return new JsonResult(new
            {
                success = true,
                message = "Bot is shutting down..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown bot, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to shutdown bot. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    private async Task LoadViewModelAsync()
    {
        var generalSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.General);
        var featuresSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Features);
        var advancedSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Advanced);

        // Load command module configurations grouped by category
        var allModules = await _commandModuleConfigurationService.GetAllModulesAsync();
        var modulesByCategory = allModules
            .GroupBy(m => m.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CommandModuleConfigurationDto>)g.OrderBy(m => m.DisplayName).ToList());

        // Determine if restart is pending from either settings or command modules
        var isRestartPending = _settingsService.IsRestartPending || _commandModuleConfigurationService.IsRestartPending;

        ViewModel = new SettingsViewModel
        {
            ActiveCategory = ActiveCategory ?? "General",
            GeneralSettings = generalSettings,
            FeaturesSettings = featuresSettings,
            AdvancedSettings = advancedSettings,
            CommandModulesByCategory = modulesByCategory,
            IsRestartPending = isRestartPending
        };

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

        _logger.LogDebug("Settings ViewModel loaded: General={GeneralCount}, Features={FeaturesCount}, Advanced={AdvancedCount}, CommandModules={ModuleCount}, RestartPending={RestartPending}",
            generalSettings.Count, featuresSettings.Count, advancedSettings.Count, allModules.Count, isRestartPending);
    }

    /// <summary>
    /// Loads theme data for the Appearance tab.
    /// </summary>
    private async Task LoadThemeDataAsync()
    {
        if (!IsSuperAdmin) return;

        try
        {
            // Get available themes and current default
            var themes = await _themeService.GetActiveThemesAsync();
            CurrentDefaultTheme = await _themeService.GetDefaultThemeAsync();

            AvailableThemes = themes.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.DisplayName,
                Selected = t.Id == CurrentDefaultTheme?.Id
            }).ToList();

            SelectedThemeId = CurrentDefaultTheme?.Id;

            _logger.LogDebug("Loaded {ThemeCount} themes for Appearance tab, current default: {DefaultTheme}",
                themes.Count, CurrentDefaultTheme?.DisplayName ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading theme data for Appearance tab");
            AvailableThemes = new List<SelectListItem>();
        }
    }

    /// <summary>
    /// Handles POST requests to save appearance settings (SuperAdmin only).
    /// </summary>
    public async Task<IActionResult> OnPostSaveAppearanceAsync()
    {
        _logger.LogInformation("Appearance settings save requested by user {UserId}", User.Identity?.Name);

        // Verify SuperAdmin authorization
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireSuperAdmin");
        if (!authResult.Succeeded)
        {
            _logger.LogWarning("Unauthorized attempt to save appearance settings by user {UserId}", User.Identity?.Name);
            return new ForbidResult();
        }

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            if (!SelectedThemeId.HasValue)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "No theme selected."
                })
                {
                    StatusCode = 400
                };
            }

            // Get theme info for audit logging
            var selectedTheme = await _themeService.GetThemeByIdAsync(SelectedThemeId.Value);
            if (selectedTheme == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Selected theme not found."
                })
                {
                    StatusCode = 400
                };
            }

            var previousDefault = await _themeService.GetDefaultThemeAsync();

            var success = await _themeService.SetDefaultThemeAsync(SelectedThemeId.Value);

            if (success)
            {
                _logger.LogInformation("Default theme changed from {OldTheme} to {NewTheme} by user {UserId}",
                    previousDefault?.DisplayName ?? "none", selectedTheme.DisplayName, userId);

                // Audit log the theme change
                _auditLogQueue.Enqueue(new AuditLogCreateDto
                {
                    Category = AuditLogCategory.Configuration,
                    Action = AuditLogAction.SettingChanged,
                    ActorType = AuditLogActorType.User,
                    ActorId = userId,
                    Details = JsonSerializer.Serialize(new
                    {
                        SettingsCategory = "Appearance",
                        Change = new
                        {
                            Key = "DefaultTheme",
                            DisplayName = "Default Theme",
                            OldValue = previousDefault?.DisplayName ?? "Discord Dark",
                            NewValue = selectedTheme.DisplayName
                        }
                    })
                });

                return new JsonResult(new
                {
                    success = true,
                    message = $"Default theme updated. New users will see {selectedTheme.DisplayName} theme.",
                    themeName = selectedTheme.DisplayName
                });
            }
            else
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to update default theme."
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving appearance settings, requested by {UserId}",
                User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while saving appearance settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handles POST requests to reset appearance settings to default (SuperAdmin only).
    /// </summary>
    public async Task<IActionResult> OnPostResetAppearanceAsync()
    {
        _logger.LogWarning("Appearance settings reset requested by user {UserId}", User.Identity?.Name);

        // Verify SuperAdmin authorization
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireSuperAdmin");
        if (!authResult.Succeeded)
        {
            _logger.LogWarning("Unauthorized attempt to reset appearance settings by user {UserId}", User.Identity?.Name);
            return new ForbidResult();
        }

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";

            // Get the system default (Discord Dark, ID 1)
            var systemDefault = await _themeService.GetThemeByIdAsync(1);
            if (systemDefault == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "System default theme not found."
                })
                {
                    StatusCode = 500
                };
            }

            var previousDefault = await _themeService.GetDefaultThemeAsync();

            var success = await _themeService.SetDefaultThemeAsync(1);

            if (success)
            {
                _logger.LogInformation("Default theme reset from {OldTheme} to {NewTheme} by user {UserId}",
                    previousDefault?.DisplayName ?? "none", systemDefault.DisplayName, userId);

                // Audit log the reset
                _auditLogQueue.Enqueue(new AuditLogCreateDto
                {
                    Category = AuditLogCategory.Configuration,
                    Action = AuditLogAction.SettingChanged,
                    ActorType = AuditLogActorType.User,
                    ActorId = userId,
                    Details = JsonSerializer.Serialize(new
                    {
                        SettingsCategory = "Appearance",
                        Operation = "Reset",
                        Change = new
                        {
                            Key = "DefaultTheme",
                            DisplayName = "Default Theme",
                            OldValue = previousDefault?.DisplayName ?? "Discord Dark",
                            NewValue = systemDefault.DisplayName
                        }
                    })
                });

                return new JsonResult(new
                {
                    success = true,
                    message = $"Default theme reset to {systemDefault.DisplayName}."
                });
            }
            else
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Failed to reset default theme."
                })
                {
                    StatusCode = 400
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting appearance settings, requested by {UserId}",
                User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while resetting appearance settings. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Loads the bot control view model data.
    /// </summary>
    private void LoadBotControlViewModel()
    {
        var status = _botService.GetStatus();
        var config = _botService.GetConfiguration();

        BotControlViewModel = new BotControlViewModel
        {
            Status = BotStatusViewModel.FromDto(status),
            Configuration = config,
            CanRestart = true,
            CanShutdown = true
        };

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

        _logger.LogDebug("Bot Control ViewModel loaded: ConnectionState={ConnectionState}, GuildCount={GuildCount}",
            BotControlViewModel.Status.ConnectionState, BotControlViewModel.Status.GuildCount);
    }
}
