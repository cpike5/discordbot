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

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Application Settings page.
/// Allows administrators to configure bot settings through a web UI.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly ILogger<SettingsModel> _logger;

    /// <summary>
    /// Gets the view model for the page.
    /// </summary>
    public SettingsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the reset category confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ResetCategoryModal { get; private set; } = null!;

    /// <summary>
    /// Gets the reset all confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ResetAllModal { get; private set; } = null!;

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
    /// Initializes a new instance of the <see cref="SettingsModel"/> class.
    /// </summary>
    public SettingsModel(
        ISettingsService settingsService,
        IAuditLogQueue auditLogQueue,
        ILogger<SettingsModel> logger)
    {
        _settingsService = settingsService;
        _auditLogQueue = auditLogQueue;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Settings page.
    /// </summary>
    /// <param name="category">Optional category to display (defaults to General).</param>
    public async Task OnGetAsync(string? category = null)
    {
        _logger.LogDebug("Settings page accessed by user {UserId}", User.Identity?.Name);

        ActiveCategory = category ?? "General";
        await LoadViewModelAsync();
    }

    /// <summary>
    /// Handles POST requests to save settings for a specific category.
    /// </summary>
    /// <param name="category">The category to save.</param>
    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to save settings", User.Identity?.Name);
            return Forbid();
        }

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

                // Audit log the settings change
                if (result.UpdatedKeys.Count > 0)
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
                            UpdatedKeys = result.UpdatedKeys,
                            RestartRequired = result.RestartRequired
                        })
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = $"Settings saved successfully. {result.UpdatedKeys.Count} setting(s) updated.",
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
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to save all settings", User.Identity?.Name);
            return Forbid();
        }

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

                // Audit log the settings change
                if (result.UpdatedKeys.Count > 0)
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
                            UpdatedKeys = result.UpdatedKeys,
                            RestartRequired = result.RestartRequired
                        })
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    message = $"All settings saved successfully. {result.UpdatedKeys.Count} setting(s) updated.",
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
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to reset category {Category}", User.Identity?.Name, category);
            return Forbid();
        }

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
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to reset all settings", User.Identity?.Name);
            return Forbid();
        }

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

    private async Task LoadViewModelAsync()
    {
        var generalSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.General);
        var featuresSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Features);
        var advancedSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Advanced);

        ViewModel = new SettingsViewModel
        {
            ActiveCategory = ActiveCategory ?? "General",
            GeneralSettings = generalSettings,
            FeaturesSettings = featuresSettings,
            AdvancedSettings = advancedSettings,
            IsRestartPending = _settingsService.IsRestartPending
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

        _logger.LogDebug("Settings ViewModel loaded: General={GeneralCount}, Features={FeaturesCount}, Advanced={AdvancedCount}, RestartPending={RestartPending}",
            generalSettings.Count, featuresSettings.Count, advancedSettings.Count, _settingsService.IsRestartPending);
    }
}
