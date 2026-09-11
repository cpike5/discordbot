namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Tier 5 (TTS authoring) additions to <see cref="IconPaths"/> — see the class-level remarks on
/// the Tier 1a partial (<c>IconPaths.cs</c>) for the naming convention. Harvested from
/// <c>_VoiceSelector.cshtml</c>, <c>_StyleSelector.cshtml</c>'s <c>styleSelector_getIconPath</c>
/// map, <c>_PresetBar.cshtml</c>'s <c>RenderPresetIcon</c>, <c>_ModeSwitcher.cshtml</c>'s
/// <c>RenderModeIcon</c>, <c>_SsmlPreview.cshtml</c>'s <c>RenderDocumentDuplicateIcon</c>, and
/// <c>_EmphasisToolbar.cshtml</c>'s <c>RenderBoltIcon</c>/<c>RenderPauseIcon</c>/<c>RenderCalendarIcon</c>.
/// A few of these are a different Heroicons revision (different literal coordinates) from a
/// same-named concept elsewhere in the file — each such case is called out below rather than
/// reusing a Tier 1a constant with different numbers, to keep every path byte-for-byte identical
/// to its source partial.
/// </summary>
public static partial class IconPaths
{
    // ---- Voice selector (_VoiceSelector.cshtml) ------------------------------------------------

    /// <summary>Checkmark. The voice-selector option's "selected" indicator.</summary>
    public const string Check = "M4.5 12.75l6 6 9-13.5";

    // ---- Style options (_StyleSelector.cshtml styleSelector_getIconPath / RenderStyleIcon) -----

    /// <summary>Smiling face. <c>cheerful</c> style.</summary>
    public const string FaceSmile = "M15.182 15.182a4.5 4.5 0 0 1-6.364 0M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0ZM9.75 9.75c0 .414-.168.75-.375.75S9 10.164 9 9.75 9.168 9 9.375 9s.375.336.375.75Zm-.375 0h.008v.015h-.008V9.75Zm5.625 0c0 .414-.168.75-.375.75s-.375-.336-.375-.75.168-.75.375-.75.375.336.375.75Zm-.375 0h.008v.015h-.008V9.75Z";

    /// <summary>Raised hand. <c>friendly</c> style.</summary>
    public const string HandRaised = "M10.05 4.575a1.575 1.575 0 1 0-3.15 0v3m3.15-3v-1.5a1.575 1.575 0 0 1 3.15 0v1.5m-3.15 0 .075 5.925m3.075.75V4.575m0 0a1.575 1.575 0 0 1 3.15 0V15M6.9 7.575a1.575 1.575 0 1 0-3.15 0v8.175a6.75 6.75 0 0 0 6.75 6.75h2.018a5.25 5.25 0 0 0 3.712-1.538l1.732-1.732a5.25 5.25 0 0 0 1.538-3.712l.003-2.024a.668.668 0 0 1 .198-.471 1.575 1.575 0 1 0-2.228-2.228 3.818 3.818 0 0 0-1.12 2.687M6.9 7.575V12m6.27 4.318A4.49 4.49 0 0 1 16.35 15";

    /// <summary>Frowning face. <c>sad</c> style.</summary>
    public const string FaceFrown = "M15.182 16.318A4.486 4.486 0 0 0 12.016 15a4.486 4.486 0 0 0-3.198 1.318M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9.75-3.75h.008v.008h-.008V8.25Zm6 0h.008v.008h-.008V8.25Z";

    /// <summary>Flame. <c>angry</c> style and the "Angry" preset.</summary>
    public const string Fire = "M15.362 5.214A8.252 8.252 0 0 1 12 21 8.25 8.25 0 0 1 6.038 7.047 8.287 8.287 0 0 0 9 9.601a8.983 8.983 0 0 1 3.361-6.867 8.21 8.21 0 0 0 3 2.48Z";

    /// <summary>Muted speaker. <c>whispering</c> style and the "Whisper" preset.</summary>
    public const string SpeakerXMark = "M17.25 9.75 19.5 12m0 0 2.25 2.25M19.5 12l2.25-2.25M19.5 12l-2.25 2.25m-10.5-6 4.72-4.72a.75.75 0 0 1 1.28.53v15.88a.75.75 0 0 1-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.009 9.009 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z";

    /// <summary>Speaker with sound waves. <c>shouting</c> style and the "Shouting" preset.</summary>
    public const string SpeakerWave = "M19.114 5.636a9 9 0 0 1 0 12.728M16.463 8.288a5.25 5.25 0 0 1 0 7.424M6.75 8.25l4.72-4.72a.75.75 0 0 1 1.28.53v15.88a.75.75 0 0 1-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.009 9.009 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z";

    /// <summary>Newspaper. <c>newscast</c> style.</summary>
    public const string Newspaper = "M12 7.5h1.5m-1.5 3h1.5m-7.5 3h7.5m-7.5 3h7.5m3-9h3.375c.621 0 1.125.504 1.125 1.125V18a2.25 2.25 0 0 1-2.25 2.25M16.5 7.5V18a2.25 2.25 0 0 0 2.25 2.25M16.5 7.5V4.875c0-.621-.504-1.125-1.125-1.125H4.125C3.504 3.75 3 4.254 3 4.875V18a2.25 2.25 0 0 0 2.25 2.25h13.5M6 7.5h3v3H6v-3Z";

    // ---- Preset bar (_PresetBar.cshtml RenderPresetIcon) ---------------------------------------

    /// <summary>Megaphone. The "Announcer" preset.</summary>
    public const string Megaphone = "M10.34 15.84c-.688-.06-1.386-.09-2.09-.09H7.5a4.5 4.5 0 1 1 0-9h.75c.704 0 1.402-.03 2.09-.09m0 9.18c.253.962.584 1.892.985 2.783.247.55.06 1.21-.463 1.511l-.657.38a.75.75 0 0 1-1.024-.272 21.37 21.37 0 0 1-1.26-2.672.747.747 0 0 1-.015-.065c-.255-.906-.47-1.834-.617-2.784m2.95 1.13c.62.057 1.246.09 1.879.09h.75a4.5 4.5 0 0 0 0-9h-.75c-.633 0-1.26.03-1.88.09m-.97 7.5v1.875c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V15.84m4.5 0V15.84";

    /// <summary>Desktop monitor. The "Robot" preset.</summary>
    public const string ComputerDesktop = "M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25";

    /// <summary>Microphone. The "Narrator" preset, and Simple mode's icon in <c>_ModeSwitcher.cshtml</c>
    /// (same path in both sources).</summary>
    public const string Microphone = "M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z";

    /// <summary>Five-pointed star. The custom-preset button icon (<c>presetBar_addCustomPresetButton</c>).</summary>
    public const string Star = "M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z";

    /// <summary>"Plus" (Heroicons v2 coordinates — distinct from <see cref="Plus"/>, which the
    /// Tier 1a <c>_EmptyState.cshtml</c> icon uses). The "Save preset" button.</summary>
    public const string PlusOutline = "M12 4.5v15m7.5-7.5h-15";

    // ---- Mode switcher (_ModeSwitcher.cshtml RenderModeIcon) ------------------------------------

    /// <summary>Sliders/adjustments. Standard mode's icon.</summary>
    public const string AdjustmentsHorizontal = "M10.5 6h9.75M10.5 6a1.5 1.5 0 1 1-3 0m3 0a1.5 1.5 0 1 0-3 0M3.75 6H7.5m3 12h9.75m-9.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-3.75 0H7.5m9-6h3.75m-3.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-9.75 0h9.75";

    /// <summary>Bolt (Heroicons v2 coordinates — distinct from <see cref="Bolt"/>, which
    /// <c>_EmphasisToolbar.cshtml</c>'s moderate-emphasis button uses). Pro mode's icon.</summary>
    public const string BoltOutline = "m3.75 13.5 10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75Z";

    // ---- Emphasis toolbar (_EmphasisToolbar.cshtml RenderBoltIcon/RenderPauseIcon/RenderCalendarIcon) --

    /// <summary>Bolt (Heroicons v1 coordinates). The moderate-emphasis toolbar button.</summary>
    public const string Bolt = "M13 10V3L4 14h7v7l9-11h-7z";

    /// <summary>Two vertical bars. The "insert pause" toolbar button.</summary>
    public const string Pause = "M6 4v16m12-16v16";

    /// <summary>Calendar grid. The "say as date" toolbar button.</summary>
    public const string Calendar = "M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5";

    // ---- SSML preview (_SsmlPreview.cshtml RenderDocumentDuplicateIcon) -------------------------

    /// <summary>Two overlapping documents. The "Copy SSML" button.</summary>
    public const string DocumentDuplicate = "M8 7v8a2 2 0 0 0 2 2h6M8 7V5a2 2 0 0 1 2-2h4.586a1 1 0 0 1 .707.293l4.414 4.414a1 1 0 0 1 .293.707V15a2 2 0 0 1-2 2h-2M8 7H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-2";
}
