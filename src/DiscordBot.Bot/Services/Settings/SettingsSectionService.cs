using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services.Settings;

/// <summary>
/// Default implementation of <see cref="ISettingsSectionService"/>. Wraps
/// <see cref="ISettingsService"/> and <see cref="ICommandModuleConfigurationService"/>,
/// performing the audit logging that used to live directly in the Settings page model.
/// </summary>
public class SettingsSectionService : ISettingsSectionService
{
    private readonly ISettingsService _settingsService;
    private readonly ICommandModuleConfigurationService _commandModuleConfigurationService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly ILogger<SettingsSectionService> _logger;

    public SettingsSectionService(
        ISettingsService settingsService,
        ICommandModuleConfigurationService commandModuleConfigurationService,
        IAuditLogQueue auditLogQueue,
        ILogger<SettingsSectionService> logger)
    {
        _settingsService = settingsService;
        _commandModuleConfigurationService = commandModuleConfigurationService;
        _auditLogQueue = auditLogQueue;
        _logger = logger;
    }

    public async Task<SettingsViewModel> LoadViewModelAsync(string activeCategory, CancellationToken cancellationToken = default)
    {
        var generalSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.General, cancellationToken);
        var featuresSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Features, cancellationToken);
        var advancedSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Advanced, cancellationToken);

        var allModules = await _commandModuleConfigurationService.GetAllModulesAsync(cancellationToken);
        var modulesByCategory = allModules
            .GroupBy(m => m.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CommandModuleConfigurationDto>)g.OrderBy(m => m.DisplayName).ToList());

        var isRestartPending = _settingsService.IsRestartPending || _commandModuleConfigurationService.IsRestartPending;

        _logger.LogDebug("Settings ViewModel loaded: General={GeneralCount}, Features={FeaturesCount}, Advanced={AdvancedCount}, CommandModules={ModuleCount}, RestartPending={RestartPending}",
            generalSettings.Count, featuresSettings.Count, advancedSettings.Count, allModules.Count, isRestartPending);

        return new SettingsViewModel
        {
            ActiveCategory = activeCategory,
            GeneralSettings = generalSettings,
            FeaturesSettings = featuresSettings,
            AdvancedSettings = advancedSettings,
            CommandModulesByCategory = modulesByCategory,
            IsRestartPending = isRestartPending
        };
    }

    public async Task<SettingsSectionResult> SaveCategoryAsync(string category, Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default)
        => await SaveInternalAsync(category, formSettings, userId, "Settings saved successfully.", "Failed to save settings.", cancellationToken);

    public async Task<SettingsSectionResult> SaveAllAsync(Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default)
        => await SaveInternalAsync("All", formSettings, userId, "All settings saved successfully.", "Failed to save all settings.", cancellationToken);

    private async Task<SettingsSectionResult> SaveInternalAsync(string category, Dictionary<string, string> formSettings, string userId, string successPrefix, string failureMessage, CancellationToken cancellationToken)
    {
        try
        {
            var updateDto = new SettingsUpdateDto { Settings = formSettings };
            var result = await _settingsService.UpdateSettingsAsync(updateDto, userId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Settings save failed for category {Category} by user {UserId}. Errors: {Errors}",
                    category, userId, string.Join(", ", result.Errors));

                return new SettingsSectionResult
                {
                    Success = false,
                    Message = failureMessage,
                    Errors = result.Errors,
                    StatusCode = 400
                };
            }

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

            _logger.LogInformation("Settings saved successfully for category {Category} by user {UserId}. Updated keys: {Keys}",
                category, userId, string.Join(", ", result.UpdatedKeys));

            return new SettingsSectionResult
            {
                Success = true,
                Message = result.Changes.Count > 0
                    ? $"{successPrefix} {result.Changes.Count} setting(s) updated."
                    : "No changes detected.",
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving settings for category {Category}, requested by {UserId}", category, userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while saving settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }

    public async Task<SettingsSectionResult> ResetCategoryAsync(string category, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<SettingCategory>(category, out var categoryEnum))
            {
                return new SettingsSectionResult
                {
                    Success = false,
                    Message = $"Invalid category: {category}",
                    StatusCode = 400
                };
            }

            var result = await _settingsService.ResetCategoryAsync(categoryEnum, userId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Reset category {Category} failed for user {UserId}. Errors: {Errors}",
                    category, userId, string.Join(", ", result.Errors));

                return new SettingsSectionResult
                {
                    Success = false,
                    Message = $"Failed to reset {category} settings.",
                    Errors = result.Errors,
                    StatusCode = 400
                };
            }

            _logger.LogInformation("Category {Category} reset to defaults by user {UserId}", category, userId);

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

            return new SettingsSectionResult
            {
                Success = true,
                Message = $"{category} settings have been reset to defaults.",
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting category {Category}, requested by {UserId}", category, userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while resetting settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }

    public async Task<SettingsSectionResult> ResetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _settingsService.ResetAllAsync(userId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Reset all settings failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));

                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "Failed to reset all settings.",
                    Errors = result.Errors,
                    StatusCode = 400
                };
            }

            _logger.LogWarning("All settings reset to defaults by user {UserId}", userId);

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

            return new SettingsSectionResult
            {
                Success = true,
                Message = "All settings have been reset to defaults.",
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting all settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while resetting settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }

    public async Task<SettingsSectionResult> SaveCommandModulesAsync(Dictionary<string, bool> commandModules, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentModules = await _commandModuleConfigurationService.GetAllModulesAsync(cancellationToken);
            var currentStates = currentModules.ToDictionary(m => m.ModuleName, m => m.IsEnabled);

            var updateDto = new CommandModuleConfigurationUpdateDto { Modules = commandModules };
            var result = await _commandModuleConfigurationService.UpdateModulesAsync(updateDto, userId, cancellationToken);

            if (!result.Success && result.UpdatedModules.Count == 0)
            {
                _logger.LogWarning("Command module settings save failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));

                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "Failed to save command module settings.",
                    Errors = result.Errors,
                    StatusCode = 400
                };
            }

            _logger.LogInformation("Command module settings saved successfully by user {UserId}. Updated modules: {Modules}",
                userId, string.Join(", ", result.UpdatedModules));

            foreach (var moduleName in result.UpdatedModules)
            {
                var previousState = currentStates.GetValueOrDefault(moduleName, true);
                var newState = commandModules.GetValueOrDefault(moduleName, true);

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

            return new SettingsSectionResult
            {
                Success = true,
                Message = result.UpdatedModules.Count > 0
                    ? $"Command module settings saved successfully. {result.UpdatedModules.Count} module(s) updated."
                    : "No changes detected.",
                RestartRequired = result.RequiresRestart
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving command module settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while saving command module settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }
}
