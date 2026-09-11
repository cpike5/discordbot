namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Icon paths added by the Tier 2 (Forms) component group - see the "Icons" summary on
/// <see cref="IconPaths"/> in <c>IconPaths.cs</c>. Split into its own file per
/// <c>docs/articles/blazor-components.md</c> ("Icon paths"): each tier adds a partial file rather
/// than editing a shared one, so parallel tiers don't collide on the same source file.
/// </summary>
public static partial class IconPaths
{
    // ---- Forms / filters (_FormInput, _FormSelect, _SortDropdown, FilterPanelTagHelper,
    // _AutocompleteInput) ------------------------------------------------------------------------

    /// <summary>Exclamation inside a circle (Heroicons v1 outline). Used by
    /// <c>_FormInput.cshtml</c>/<c>_FormSelect.cshtml</c> for the <c>ValidationState.Error</c>
    /// message icon. Visually distinct from <see cref="ExclamationCircle"/> (a different, rounder
    /// Heroicons v2 path already in this class for <c>EmptyStateType.Error</c>) - both partials
    /// ship in this codebase, so both paths are kept as separate named constants rather than
    /// collapsing them.</summary>
    public const string ExclamationCircleOutline = "M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Funnel. From <c>FilterPanelTagHelper</c>'s header icon.</summary>
    public const string Funnel = "M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z";

    /// <summary>Ascending/descending bars with an arrow ("bars-arrow-down"). From
    /// <c>_SortDropdown.cshtml</c>'s toggle button.</summary>
    public const string BarsArrowDown = "M3 4h13M3 8h9m-9 4h6m4 0l4-4m0 0l4 4m-4-4v12";

    /// <summary>Checkmark. From <c>_SortDropdown.cshtml</c>'s selected-option indicator.</summary>
    public const string Check = "M5 13l4 4L19 7";

    /// <summary>Circular arrow ("arrow-path" / refresh). From <c>_SettingField.cshtml</c>'s
    /// "Restart Required" badge icon.</summary>
    public const string ArrowPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15";

    /// <summary>Single person silhouette. From <c>autocomplete.js</c>'s <c>getDefaultIcon()</c>
    /// (the default autocomplete result icon, used when a result isn't a channel).</summary>
    public const string User = "M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z";

    /// <summary>Speaker with sound waves. From <c>autocomplete.js</c>'s <c>getChannelIcon()</c>
    /// for a voice/stage channel result.</summary>
    public const string SpeakerWave = "M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z";

    /// <summary>Hashtag. From <c>autocomplete.js</c>'s <c>getChannelIcon()</c> for a text channel
    /// result.</summary>
    public const string Hashtag = "M7 20l4-16m2 16l4-16M6 9h14M4 15h14";
}
