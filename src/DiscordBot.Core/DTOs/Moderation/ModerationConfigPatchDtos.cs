using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// A partial update to a moderation config section. Every property is optional: a property that is
/// <c>null</c> was not sent and leaves the saved value alone. That is how the settings page saves
/// only the fields a moderator edited, and why settings the page does not show (such as the
/// allowed link domains) are never wiped by a save.
/// </summary>
/// <typeparam name="TTarget">The saved section the patch applies to.</typeparam>
public interface IModerationConfigPatch<in TTarget>
{
    /// <summary>
    /// Checks the sent values. Keys are the camelCase JSON property names, so the page can put each
    /// message on its field; an empty result means the patch is valid.
    /// </summary>
    IReadOnlyDictionary<string, string> Validate();

    /// <summary>
    /// Copies the sent values onto <paramref name="target"/>. Call <see cref="Validate"/> first.
    /// </summary>
    void ApplyTo(TTarget target);
}

/// <summary>
/// Partial update for <see cref="SpamDetectionConfigDto"/>.
/// </summary>
public class SpamConfigPatchDto : IModerationConfigPatch<SpamDetectionConfigDto>
{
    /// <summary>Allowed range for messages per window.</summary>
    public const int MaxMessagesMin = 1, MaxMessagesMax = 100;

    /// <summary>Allowed range for the window, in seconds.</summary>
    public const int WindowSecondsMin = 1, WindowSecondsMax = 60;

    /// <summary>Allowed range for mentions per message.</summary>
    public const int MentionsMin = 1, MentionsMax = 50;

    /// <summary>Allowed range for the duplicate similarity (a fraction, shown as a percentage).</summary>
    public const double SimilarityMin = 0.5, SimilarityMax = 1.0;

    /// <summary>Whether spam detection is on.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Messages allowed in the window.</summary>
    public int? MaxMessagesPerWindow { get; set; }

    /// <summary>The window, in seconds.</summary>
    public int? WindowSeconds { get; set; }

    /// <summary>Mentions allowed in one message.</summary>
    public int? MaxMentionsPerMessage { get; set; }

    /// <summary>Similarity (0.5 to 1) at which messages count as duplicates.</summary>
    public double? DuplicateMessageThreshold { get; set; }

    /// <summary>What to do when spam is detected.</summary>
    public AutoAction? AutoAction { get; set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Validate()
    {
        var errors = new Dictionary<string, string>();
        ConfigPatchRules.Range(errors, "maxMessagesPerWindow", MaxMessagesPerWindow, MaxMessagesMin, MaxMessagesMax, "Messages");
        ConfigPatchRules.Range(errors, "windowSeconds", WindowSeconds, WindowSecondsMin, WindowSecondsMax, "The time window");
        ConfigPatchRules.Range(errors, "maxMentionsPerMessage", MaxMentionsPerMessage, MentionsMin, MentionsMax, "Mentions");

        if (DuplicateMessageThreshold is { } threshold &&
            (double.IsNaN(threshold) || threshold < SimilarityMin || threshold > SimilarityMax))
        {
            errors["duplicateMessageThreshold"] = "Similarity must be between 50 and 100 percent.";
        }

        if (AutoAction is { } action && !Enum.IsDefined(action))
        {
            errors["autoAction"] = "Choose one of the listed actions.";
        }

        return errors;
    }

    /// <inheritdoc/>
    public void ApplyTo(SpamDetectionConfigDto target)
    {
        if (Enabled.HasValue) target.Enabled = Enabled.Value;
        if (MaxMessagesPerWindow.HasValue) target.MaxMessagesPerWindow = MaxMessagesPerWindow.Value;
        if (WindowSeconds.HasValue) target.WindowSeconds = WindowSeconds.Value;
        if (MaxMentionsPerMessage.HasValue) target.MaxMentionsPerMessage = MaxMentionsPerMessage.Value;
        if (DuplicateMessageThreshold.HasValue) target.DuplicateMessageThreshold = DuplicateMessageThreshold.Value;
        if (AutoAction.HasValue) target.AutoAction = AutoAction.Value;
    }
}

/// <summary>
/// Partial update for <see cref="ContentFilterConfigDto"/>. The allowed link domains and the
/// "block unlisted links" switch are not on the settings page, so a save never touches them.
/// </summary>
public class ContentFilterPatchDto : IModerationConfigPatch<ContentFilterConfigDto>
{
    /// <summary>Most blocklist entries a guild may keep.</summary>
    public const int MaxProhibitedWords = 500;

    /// <summary>Longest single blocklist entry.</summary>
    public const int MaxWordLength = 100;

    /// <summary>The actions the content filter offers (there is no kick for deleted content).</summary>
    public static readonly IReadOnlyList<AutoAction> AllowedActions =
        new[] { Core.DTOs.AutoAction.None, Core.DTOs.AutoAction.Delete, Core.DTOs.AutoAction.Warn, Core.DTOs.AutoAction.Mute, Core.DTOs.AutoAction.Ban };

    /// <summary>Whether the content filter is on.</summary>
    public bool? Enabled { get; set; }

    /// <summary>The blocklist, replacing the saved one when sent.</summary>
    public List<string>? ProhibitedWords { get; set; }

    /// <summary>Whether Discord invite links are blocked.</summary>
    public bool? BlockInviteLinks { get; set; }

    /// <summary>What to do when prohibited content is found.</summary>
    public AutoAction? AutoAction { get; set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Validate()
    {
        var errors = new Dictionary<string, string>();

        if (ProhibitedWords != null)
        {
            if (ProhibitedWords.Count > MaxProhibitedWords)
            {
                errors["prohibitedWords"] = $"The blocklist can hold up to {MaxProhibitedWords} entries.";
            }
            else if (ProhibitedWords.Any(w => w != null && w.Trim().Length > MaxWordLength))
            {
                errors["prohibitedWords"] = $"Each blocklist entry must be {MaxWordLength} characters or fewer.";
            }
        }

        if (AutoAction is { } action && !AllowedActions.Contains(action))
        {
            errors["autoAction"] = "Choose one of the listed actions.";
        }

        return errors;
    }

    /// <inheritdoc/>
    public void ApplyTo(ContentFilterConfigDto target)
    {
        if (Enabled.HasValue) target.Enabled = Enabled.Value;
        if (ProhibitedWords != null)
        {
            target.ProhibitedWords = ProhibitedWords
                .Select(w => w?.Trim() ?? string.Empty)
                .Where(w => w.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        if (BlockInviteLinks.HasValue) target.BlockInviteLinks = BlockInviteLinks.Value;
        if (AutoAction.HasValue) target.AutoAction = AutoAction.Value;
    }
}

/// <summary>
/// Partial update for <see cref="RaidProtectionConfigDto"/>.
/// </summary>
public class RaidProtectionPatchDto : IModerationConfigPatch<RaidProtectionConfigDto>
{
    /// <summary>Allowed range for joins per window.</summary>
    public const int MaxJoinsMin = 5, MaxJoinsMax = 100;

    /// <summary>Allowed range for the window, in seconds.</summary>
    public const int WindowSecondsMin = 10, WindowSecondsMax = 300;

    /// <summary>Allowed range for the minimum account age, in hours.</summary>
    public const int AccountAgeMin = 0, AccountAgeMax = 720;

    /// <summary>Whether raid protection is on.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Joins allowed in the window.</summary>
    public int? MaxJoinsPerWindow { get; set; }

    /// <summary>The window, in seconds.</summary>
    public int? WindowSeconds { get; set; }

    /// <summary>Minimum account age in hours (0 for no restriction).</summary>
    public int? MinAccountAgeHours { get; set; }

    /// <summary>What to do when a raid is detected.</summary>
    public RaidAutoAction? AutoAction { get; set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Validate()
    {
        var errors = new Dictionary<string, string>();
        ConfigPatchRules.Range(errors, "maxJoinsPerWindow", MaxJoinsPerWindow, MaxJoinsMin, MaxJoinsMax, "Joins");
        ConfigPatchRules.Range(errors, "windowSeconds", WindowSeconds, WindowSecondsMin, WindowSecondsMax, "The time window");
        ConfigPatchRules.Range(errors, "minAccountAgeHours", MinAccountAgeHours, AccountAgeMin, AccountAgeMax, "Account age");

        if (AutoAction is { } action && !Enum.IsDefined(action))
        {
            errors["autoAction"] = "Choose one of the listed actions.";
        }

        return errors;
    }

    /// <inheritdoc/>
    public void ApplyTo(RaidProtectionConfigDto target)
    {
        if (Enabled.HasValue) target.Enabled = Enabled.Value;
        if (MaxJoinsPerWindow.HasValue) target.MaxJoinsPerWindow = MaxJoinsPerWindow.Value;
        if (WindowSeconds.HasValue) target.WindowSeconds = WindowSeconds.Value;
        if (MinAccountAgeHours.HasValue) target.MinAccountAgeHours = MinAccountAgeHours.Value;
        if (AutoAction.HasValue) target.AutoAction = AutoAction.Value;
    }
}

/// <summary>
/// Shared checks for the patch DTOs.
/// </summary>
internal static class ConfigPatchRules
{
    public static void Range(IDictionary<string, string> errors, string key, int? value, int min, int max, string subject)
    {
        if (value is { } v && (v < min || v > max))
        {
            errors[key] = $"{subject} must be between {min} and {max}.";
        }
    }
}
