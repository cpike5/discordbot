using System.Text;
using System.Text.RegularExpressions;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Plain-language names for the purge pages (UX plan C-2): the bulk-purge entity types and the
/// per-table record counts the user purge reports. A seed for the Phase 16 display-name helper.
/// </summary>
public static class PurgeDisplay
{
    /// <summary>Label for a bulk-purge entity type ("Moderation cases").</summary>
    public static string EntityLabel(BulkPurgeEntityType type) => type switch
    {
        BulkPurgeEntityType.Messages => "Message logs",
        BulkPurgeEntityType.AuditLogs => "Audit logs",
        BulkPurgeEntityType.CommandLogs => "Command logs",
        BulkPurgeEntityType.ModerationCases => "Moderation cases",
        _ => Split(type.ToString())
    };

    /// <summary>One-line description of what a bulk-purge entity type holds.</summary>
    public static string EntityDescription(BulkPurgeEntityType type) => type switch
    {
        BulkPurgeEntityType.Messages => "Discord message history",
        BulkPurgeEntityType.AuditLogs => "System audit trail",
        BulkPurgeEntityType.CommandLogs => "Command execution logs",
        BulkPurgeEntityType.ModerationCases => "Moderation case records",
        _ => string.Empty
    };

    /// <summary>Lower-case noun for a sentence ("12 message logs", "3 moderation cases").</summary>
    public static string EntityNoun(BulkPurgeEntityType type) => EntityLabel(type).ToLowerInvariant();

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
        ["ApplicationUser"] = "Linked web account"
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

    /// <summary>"ModerationCases" becomes "Moderation cases".</summary>
    public static string Split(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        var spaced = Regex.Replace(pascal.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
        if (spaced.Length == 0) return string.Empty;
        var sb = new StringBuilder(spaced.ToLowerInvariant());
        sb[0] = char.ToUpperInvariant(sb[0]);
        return sb.ToString();
    }
}
