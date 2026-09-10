using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using IAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace DiscordBot.Bot.Services.Settings;

/// <summary>
/// Default implementation of <see cref="IAppearanceSettingsService"/>. Wraps
/// <see cref="IThemeService"/> and the SuperAdmin authorization check, plus audit logging.
/// </summary>
public class AppearanceSettingsService : IAppearanceSettingsService
{
    /// <summary>Theme ID of the built-in system default ("Discord Dark").</summary>
    private const int SystemDefaultThemeId = 1;

    private readonly IThemeService _themeService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly ILogger<AppearanceSettingsService> _logger;

    public AppearanceSettingsService(
        IThemeService themeService,
        IAuthorizationService authorizationService,
        IAuditLogQueue auditLogQueue,
        ILogger<AppearanceSettingsService> logger)
    {
        _themeService = themeService;
        _authorizationService = authorizationService;
        _auditLogQueue = auditLogQueue;
        _logger = logger;
    }

    public async Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)
    {
        var authResult = await _authorizationService.AuthorizeAsync(user, "RequireSuperAdmin");
        return authResult.Succeeded;
    }

    public async Task<AppearanceThemeData> LoadThemeDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var themes = await _themeService.GetActiveThemesAsync(cancellationToken);
            var currentDefaultTheme = await _themeService.GetDefaultThemeAsync(cancellationToken);

            var availableThemes = themes.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.DisplayName,
                Selected = t.Id == currentDefaultTheme?.Id
            }).ToList();

            _logger.LogDebug("Loaded {ThemeCount} themes for Appearance tab, current default: {DefaultTheme}",
                themes.Count, currentDefaultTheme?.DisplayName ?? "none");

            return new AppearanceThemeData
            {
                AvailableThemes = availableThemes,
                CurrentDefaultTheme = currentDefaultTheme,
                SelectedThemeId = currentDefaultTheme?.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading theme data for Appearance tab");
            return new AppearanceThemeData();
        }
    }

    public async Task<SettingsSectionResult> SaveThemeAsync(int themeId, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedTheme = await _themeService.GetThemeByIdAsync(themeId, cancellationToken);
            if (selectedTheme == null)
            {
                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "Selected theme not found.",
                    StatusCode = 400
                };
            }

            var previousDefault = await _themeService.GetDefaultThemeAsync(cancellationToken);
            var success = await _themeService.SetDefaultThemeAsync(themeId, cancellationToken);

            if (!success)
            {
                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "Failed to update default theme.",
                    StatusCode = 400
                };
            }

            _logger.LogInformation("Default theme changed from {OldTheme} to {NewTheme} by user {UserId}",
                previousDefault?.DisplayName ?? "none", selectedTheme.DisplayName, userId);

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

            return new SettingsSectionResult
            {
                Success = true,
                Message = $"Default theme updated. New users will see {selectedTheme.DisplayName} theme.",
                ThemeName = selectedTheme.DisplayName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while saving appearance settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while saving appearance settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }

    public async Task<SettingsSectionResult> ResetThemeAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var systemDefault = await _themeService.GetThemeByIdAsync(SystemDefaultThemeId, cancellationToken);
            if (systemDefault == null)
            {
                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "System default theme not found.",
                    StatusCode = 500
                };
            }

            var previousDefault = await _themeService.GetDefaultThemeAsync(cancellationToken);
            var success = await _themeService.SetDefaultThemeAsync(SystemDefaultThemeId, cancellationToken);

            if (!success)
            {
                return new SettingsSectionResult
                {
                    Success = false,
                    Message = "Failed to reset default theme.",
                    StatusCode = 400
                };
            }

            _logger.LogInformation("Default theme reset from {OldTheme} to {NewTheme} by user {UserId}",
                previousDefault?.DisplayName ?? "none", systemDefault.DisplayName, userId);

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

            return new SettingsSectionResult
            {
                Success = true,
                Message = $"Default theme reset to {systemDefault.DisplayName}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while resetting appearance settings, requested by {UserId}", userId);
            return new SettingsSectionResult
            {
                Success = false,
                Message = "An error occurred while resetting appearance settings. Please check logs for details.",
                StatusCode = 500
            };
        }
    }
}
