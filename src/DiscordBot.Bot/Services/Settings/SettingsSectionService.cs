using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
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
    private readonly ILlmModelRepository _llmModelRepository;
    private readonly ILlmModelResolver _llmModelResolver;
    private readonly ILogger<SettingsSectionService> _logger;

    public SettingsSectionService(
        ISettingsService settingsService,
        ICommandModuleConfigurationService commandModuleConfigurationService,
        IAuditLogQueue auditLogQueue,
        ILlmModelRepository llmModelRepository,
        ILlmModelResolver llmModelResolver,
        ILogger<SettingsSectionService> logger)
    {
        _settingsService = settingsService;
        _commandModuleConfigurationService = commandModuleConfigurationService;
        _auditLogQueue = auditLogQueue;
        _llmModelRepository = llmModelRepository;
        _llmModelResolver = llmModelResolver;
        _logger = logger;
    }

    public async Task<SettingsViewModel> LoadViewModelAsync(string activeCategory, CancellationToken cancellationToken = default)
    {
        var generalSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.General, cancellationToken);
        var featuresSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Features, cancellationToken);
        var advancedSettings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.Advanced, cancellationToken);
        var (aiModelsSettings, aiModelsConfiguredSlugs, aiModelsEnabledSlugCount) = await LoadAiModelsSettingsAsync(cancellationToken);

        var allModules = await _commandModuleConfigurationService.GetAllModulesAsync(cancellationToken);
        var modulesByCategory = allModules
            .GroupBy(m => m.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CommandModuleConfigurationDto>)g.OrderBy(m => m.DisplayName).ToList());

        var isRestartPending = _settingsService.IsRestartPending || _commandModuleConfigurationService.IsRestartPending;

        _logger.LogDebug("Settings ViewModel loaded: General={GeneralCount}, Features={FeaturesCount}, Advanced={AdvancedCount}, AiModels={AiModelsCount}, CommandModules={ModuleCount}, RestartPending={RestartPending}",
            generalSettings.Count, featuresSettings.Count, advancedSettings.Count, aiModelsSettings.Count, allModules.Count, isRestartPending);

        return new SettingsViewModel
        {
            ActiveCategory = activeCategory,
            GeneralSettings = generalSettings,
            FeaturesSettings = featuresSettings,
            AdvancedSettings = advancedSettings,
            AiModelsSettings = aiModelsSettings,
            AiModelsConfiguredSlugs = aiModelsConfiguredSlugs,
            AiModelsEnabledSlugCount = aiModelsEnabledSlugCount,
            CommandModulesByCategory = modulesByCategory,
            IsRestartPending = isRestartPending
        };
    }

    /// <summary>
    /// Loads the AiModels category and post-processes each of the three mode settings'
    /// <see cref="SettingDto.AllowedValues"/> to the currently enabled catalog slugs (sorted), so
    /// <c>_SettingField</c> can render a &lt;select&gt;. The current value is included even when it
    /// is not enabled (disabled, unavailable, or never seen by a refresh) so the select still shows
    /// the real current value instead of silently substituting the first option. "" is always the
    /// first allowed value - it means "use the configured value" (see
    /// <see cref="DiscordBot.Infrastructure.Services.SettingDefinitions"/>'s AiModels entries) - so
    /// the web layer can render it as a distinct "Use configured value" option.
    /// </summary>
    private async Task<(IReadOnlyList<SettingDto> Settings, IReadOnlyDictionary<string, string> ConfiguredSlugs, int EnabledSlugCount)>
        LoadAiModelsSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSettingsByCategoryAsync(SettingCategory.AiModels, cancellationToken);

        var enabledSlugs = (await _llmModelRepository.GetEnabledAsync(cancellationToken))
            .Select(m => m.Id)
            .OrderBy(slug => slug, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The "" option means "use the configured value" - resolve what that actually is per mode
        // (via ILlmModelResolver, the same resolution path used at message-send time) so the view
        // can label it e.g. "Use configured value (anthropic/claude-sonnet-4)" instead of a bare "".
        var configuredSlugs = new Dictionary<string, string>();
        foreach (var mode in LlmModeSettings.All)
        {
            var resolved = await _llmModelResolver.ResolveAsync(mode, cancellationToken);
            configuredSlugs[LlmModeSettings.KeyFor(mode)] = resolved.ConfiguredSlug;
        }

        var settingDtos = settings
            .Select(setting =>
            {
                var allowedValues = new List<string> { "" };
                allowedValues.AddRange(enabledSlugs);
                if (!string.IsNullOrWhiteSpace(setting.Value)
                    && !allowedValues.Contains(setting.Value, StringComparer.OrdinalIgnoreCase))
                {
                    allowedValues.Add(setting.Value);
                }

                return setting with { AllowedValues = allowedValues };
            })
            .ToList();

        return (settingDtos, configuredSlugs, enabledSlugs.Count);
    }

    /// <summary>
    /// Validation hook for the three AiModels mode settings: when <paramref name="formSettings"/>
    /// contains one of <see cref="LlmModeSettings"/>'s keys, the submitted slug must exist in the
    /// local catalog, be <see cref="LlmModel.IsEnabled"/>, and <see cref="LlmModel.SupportsTools"/>
    /// (every mode sends tools, and <c>provider.require_parameters</c> would otherwise fail the
    /// call at send time). Returns the first failure's message, or null when every submitted mode
    /// key (if any) is valid. Kept as a single small method - not a generic per-category validator
    /// interface - so a later phase can extend it without new plumbing; nothing else in this class
    /// needs to change if another category grows a similar rule.
    /// </summary>
    private async Task<string?> ValidateAiModelSelectionsAsync(
        Dictionary<string, string> formSettings, CancellationToken cancellationToken)
    {
        foreach (var mode in LlmModeSettings.All)
        {
            var key = LlmModeSettings.KeyFor(mode);
            if (!formSettings.TryGetValue(key, out var slug) || string.IsNullOrWhiteSpace(slug))
            {
                // "" (or missing) means "use the configured value" - SettingDefinitions' AiModels
                // entries default to it, and it is never a catalog slug, so there is nothing to
                // validate against the catalog. Skip it rather than rejecting a legitimate reset.
                continue;
            }

            var model = await _llmModelRepository.GetByIdAsync(slug, cancellationToken);
            if (model is null)
            {
                return $"'{slug}' is not in the model catalog. Refresh the catalog or pick a different model for {LlmModeSettings.LabelFor(mode)}.";
            }

            if (!model.IsEnabled)
            {
                return $"'{slug}' is not enabled. Enable it on the AI Models tab before setting it as the {LlmModeSettings.LabelFor(mode)} default.";
            }

            if (!model.SupportsTools)
            {
                return $"'{slug}' does not support tool calling, which {LlmModeSettings.LabelFor(mode)} requires. Pick a tool-capable model.";
            }
        }

        return null;
    }

    public async Task<SettingsSectionResult> SaveCategoryAsync(string category, Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<SettingCategory>(category, out var categoryEnum) || !Enum.IsDefined(categoryEnum))
        {
            return new SettingsSectionResult
            {
                Success = false,
                Message = $"Unknown settings tab: {category}.",
                StatusCode = 400
            };
        }

        // Save only this tab's settings. A client that posts the whole page (or a stale one)
        // must not overwrite settings that belong to other tabs.
        var categoryKeys = (await _settingsService.GetSettingsByCategoryAsync(categoryEnum, cancellationToken))
            .Select(s => s.Key)
            .ToHashSet(StringComparer.Ordinal);
        var scoped = formSettings
            .Where(pair => categoryKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return await SaveInternalAsync(category, scoped, userId, "Saved", "The settings could not be saved.", cancellationToken);
    }

    public async Task<SettingsSectionResult> SaveAllAsync(Dictionary<string, string> formSettings, string userId, CancellationToken cancellationToken = default)
        => await SaveInternalAsync("All", formSettings, userId, "Saved", "The settings could not be saved.", cancellationToken);

    /// <summary>The tab's name as users read it ("AiModels" is "AI Models").</summary>
    internal static string CategoryLabel(string category) => category switch
    {
        "AiModels" => "AI Models",
        "BotControl" => "Bot Control",
        _ => category
    };

    /// <summary>"Saved 1 setting." / "Saved 3 settings." / "Nothing changed: ...".</summary>
    internal static string SavedMessage(string prefix, int changeCount, string noun = "setting") =>
        changeCount == 0
            ? "Nothing changed. These values were already saved."
            : $"{prefix} {changeCount} {(changeCount == 1 ? noun : noun + "s")}.";

    private async Task<SettingsSectionResult> SaveInternalAsync(string category, Dictionary<string, string> formSettings, string userId, string successPrefix, string failureMessage, CancellationToken cancellationToken)
    {
        try
        {
            var validationError = await ValidateAiModelSelectionsAsync(formSettings, cancellationToken);
            if (validationError != null)
            {
                _logger.LogWarning("Settings save rejected for category {Category} by user {UserId}: {Error}",
                    category, userId, validationError);

                return new SettingsSectionResult
                {
                    Success = false,
                    Message = failureMessage,
                    Errors = new List<string> { validationError },
                    StatusCode = 400
                };
            }

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
                Message = SavedMessage(successPrefix, result.Changes.Count),
                ChangeCount = result.Changes.Count,
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving settings for category {Category}, requested by {UserId}", category, userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "The settings could not be saved because of a server error. Check the logs.",
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
                    Message = $"Unknown settings tab: {category}.",
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
                    Message = $"The {CategoryLabel(category)} settings could not be reset.",
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
                Message = $"{CategoryLabel(category)} settings were reset to their defaults.",
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting category {Category}, requested by {UserId}", category, userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "The settings could not be reset because of a server error. Check the logs.",
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
                    Message = "The settings could not be reset.",
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
                Message = "All settings were reset to their defaults.",
                RestartRequired = result.RestartRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting all settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "The settings could not be reset because of a server error. Check the logs.",
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
                    Message = "The command modules could not be saved.",
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
                Message = SavedMessage("Saved", result.UpdatedModules.Count, "command module"),
                ChangeCount = result.UpdatedModules.Count,
                RestartRequired = result.RequiresRestart
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving command module settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "The command modules could not be saved because of a server error. Check the logs.",
                StatusCode = 500
            };
        }
    }
}
