using DiscordBot.Core.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Handles the Appearance tab of the Application Settings page (SuperAdmin only):
/// theme listing, selection, save, and reset, including audit logging and the
/// SuperAdmin authorization check. Extracted from
/// <c>DiscordBot.Bot.Pages.Admin.SettingsModel</c>.
/// </summary>
public interface IAppearanceSettingsService
{
    /// <summary>Checks whether the given user may access the Appearance tab.</summary>
    Task<bool> IsSuperAdminAsync(ClaimsPrincipal user);

    /// <summary>Loads the available themes and current default for the Appearance tab.</summary>
    Task<AppearanceThemeData> LoadThemeDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the default theme (SuperAdmin only — caller must have already authorized).</summary>
    Task<SettingsSectionResult> SaveThemeAsync(int themeId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Resets the default theme to the system default (SuperAdmin only).</summary>
    Task<SettingsSectionResult> ResetThemeAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>Theme data needed to render the Appearance tab.</summary>
public sealed record AppearanceThemeData
{
    public IReadOnlyList<SelectListItem> AvailableThemes { get; init; } = Array.Empty<SelectListItem>();
    public ThemeDto? CurrentDefaultTheme { get; init; }
    public int? SelectedThemeId { get; init; }
}
