using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Settings page.
/// Organizes settings by category with state for restart requirements and messages.
/// </summary>
public class SettingsViewModel
{
    /// <summary>
    /// Gets or sets the currently active category tab.
    /// </summary>
    public string ActiveCategory { get; set; } = "General";

    /// <summary>
    /// Gets or sets the settings for the General category.
    /// </summary>
    public IReadOnlyList<SettingDto> GeneralSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the Features category.
    /// </summary>
    public IReadOnlyList<SettingDto> FeaturesSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the Advanced category.
    /// </summary>
    public IReadOnlyList<SettingDto> AdvancedSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets the settings for the AI Models category (per-mode default OpenRouter model
    /// slugs). Each <see cref="SettingDto.AllowedValues"/> is post-processed to the currently
    /// enabled catalog slugs (sorted), plus the current value itself if it isn't among them, so
    /// the AI Models tab can render each as a &lt;select&gt; that always shows the real current value.
    /// </summary>
    public IReadOnlyList<SettingDto> AiModelsSettings { get; set; } = new List<SettingDto>();

    /// <summary>
    /// Gets or sets, per AI Models setting key, the slug that "" (the "use configured value"
    /// option) resolves to - the bound configuration value, or <c>OpenRouter:DefaultModel</c> when
    /// nothing is configured. Resolved via <c>ILlmModelResolver</c> so it matches the value actually
    /// used at message-send time even while a DB override shadows it for that mode.
    /// </summary>
    public IReadOnlyDictionary<string, string> AiModelsConfiguredSlugs { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets how many catalog models are currently enabled. Drives the AI Models tab's
    /// "no models enabled yet" hint, which must reflect this directly rather than
    /// <c>SettingDto.AllowedValues.Count</c> - that list also always carries "" and, sometimes, the
    /// current (possibly disabled) value, so its count alone cannot tell "no models enabled" apart
    /// from "one model enabled" or "the current value merely isn't enabled".
    /// </summary>
    public int AiModelsEnabledSlugCount { get; set; }

    /// <summary>
    /// Gets or sets the command module configurations grouped by category.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<CommandModuleConfigurationDto>> CommandModulesByCategory { get; set; }
        = new Dictionary<string, IReadOnlyList<CommandModuleConfigurationDto>>();

    /// <summary>
    /// Gets or sets whether a restart is pending due to settings changes.
    /// </summary>
    public bool IsRestartPending { get; set; }

    /// <summary>
    /// Gets or sets a success message to display to the user.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets an error message to display to the user.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
