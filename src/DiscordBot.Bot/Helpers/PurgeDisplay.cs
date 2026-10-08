using DiscordBot.Core.Extensions;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Plain-language names for the per-table record counts the user purge reports (UX plan C-2). The
/// bulk-purge entity types are an enum and read through <c>DisplayName()</c> like every other one.
/// </summary>
public static class PurgeDisplay
{
    private static readonly Dictionary<string, string> CountLabels = new(StringComparer.Ordinal)
    {
        ["MessageLogs"] = "Message logs",
        ["CommandLogs"] = "Command logs",
        ["RatVotes"] = "Rat Watch votes",
        ["RatRecords"] = "Rat Watch records",
        ["RatWatches"] = "Rat Watches",
        ["Reminders"] = "Reminders",
        ["ModNotes"] = "Moderator notes",
        ["UserModTags"] = "Moderation tags",
        ["Watchlists"] = "Watchlist entries",
        ["SoundPlayLogs"] = "Sound play history",
        ["TtsMessages"] = "Text-to-speech messages",
        ["LlmUsageRecords"] = "Assistant usage records",
        ["AssistantInteractionLogs"] = "Assistant conversations",
        ["DmAssistantInteractionLogs"] = "Assistant DM conversations",
        ["DmAssistantUsageMetrics"] = "Assistant DM usage",
        ["GuildMembers"] = "Server memberships",
        ["UserConsents"] = "Consent records",
        ["Users"] = "User profile",
        ["UserGuildAccess"] = "Portal server access",
        ["UserDiscordGuilds"] = "Linked Discord servers",
        ["DiscordOAuthTokens"] = "Discord sign-in tokens",
        ["ApplicationUser"] = "Linked web account",
        ["UserPreferences"] = "Preferences",
        ["UserSoundFavorites"] = "Favorite sounds",
        ["UserTtsPresets"] = "Text-to-speech presets",
        ["TtsMessageHistory"] = "Text-to-speech history",
        ["VoxMessageHistory"] = "VOX history",
        ["AudioPlaybackLogs"] = "Audio playback history",
        ["DmConversationMessages"] = "Assistant DM history",
        ["DmAssistantNotes"] = "Assistant notes",
        ["UserActivityEvents"] = "Activity events",
        ["MemberActivitySnapshots"] = "Activity summaries",
        ["FeatureRequests"] = "Feature requests",
        ["FeatureRequestRejections"] = "Declined feature requests",
        ["VerificationCodes"] = "Verification codes",
        ["UserNotifications"] = "Notifications",
        ["UserActivityLogs"] = "Account activity log"
    };

    private const string AnonymizedSuffix = "_Anonymized";

    /// <summary>
    /// Label for a user-purge count key. A key ending in <c>_Anonymized</c> reads "(anonymized)".
    /// Unknown keys fall back to splitting the PascalCase name.
    /// </summary>
    public static string CountLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var anonymized = IsAnonymized(key);
        var baseKey = anonymized ? key[..^AnonymizedSuffix.Length] : key;
        var label = CountLabels.TryGetValue(baseKey, out var known) ? known : Split(baseKey);
        return anonymized ? $"{label} (anonymized)" : label;
    }

    /// <summary>True when a user-purge count key is anonymized rather than deleted.</summary>
    public static bool IsAnonymized(string key) => key.EndsWith(AnonymizedSuffix, StringComparison.Ordinal);

    /// <summary>"ModerationCases" becomes "Moderation cases" (see <see cref="EnumDisplayExtensions.Humanize"/>).</summary>
    public static string Split(string pascal) => EnumDisplayExtensions.Humanize(pascal);
}
