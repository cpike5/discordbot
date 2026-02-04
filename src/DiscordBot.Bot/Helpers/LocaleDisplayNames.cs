namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides human-readable display names for locale codes.
/// Supports case-insensitive lookup with fallback to raw locale code for unknown locales.
/// </summary>
public static class LocaleDisplayNames
{
    private static readonly Dictionary<string, string> LocaleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "en-US", "English (US)" },
        { "en-GB", "English (UK)" },
        { "ja-JP", "Japanese" },
        { "fr-FR", "French" },
        { "de-DE", "German" },
        { "it-IT", "Italian" },
        { "es-ES", "Spanish (Spain)" },
        { "es-MX", "Spanish (Mexico)" },
        { "hi-IN", "Hindi" },
        { "zh-CN", "Chinese (Mandarin)" },
        { "sv-SE", "Swedish" },
        { "ru-RU", "Russian" },
        { "ar-SA", "Arabic" }
    };

    /// <summary>
    /// Gets the human-readable display name for a locale code.
    /// </summary>
    /// <param name="localeCode">The locale code (e.g., "en-US")</param>
    /// <returns>The display name, or the raw locale code if not found</returns>
    public static string GetDisplayName(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
            return string.Empty;

        return LocaleMap.TryGetValue(localeCode, out var displayName)
            ? displayName
            : localeCode;
    }
}
