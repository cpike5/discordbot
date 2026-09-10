namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the category for organizing application settings in the UI.
/// </summary>
public enum SettingCategory
{
    /// <summary>
    /// General bot settings (timezone, status message, bot enabled).
    /// </summary>
    General,

    /// <summary>
    /// Logging configuration (log level, retention).
    /// </summary>
    Logging,

    /// <summary>
    /// Feature toggles and behavior settings (rate limits, enabled modules).
    /// </summary>
    Features,

    /// <summary>
    /// Advanced/technical settings (debug mode, caching, data retention).
    /// </summary>
    Advanced,

    /// <summary>
    /// Appearance settings (themes, UI preferences).
    /// </summary>
    Appearance,

    /// <summary>
    /// Per-mode default OpenRouter model slugs (guild assistant, DM assistant, feature requests).
    /// Rendered as the "AI Models" tab's defaults panel, not a generic settings form - see
    /// <c>Pages/Admin/Settings.cshtml</c> and <c>wwwroot/js/llm-models.js</c>.
    /// </summary>
    AiModels
}
