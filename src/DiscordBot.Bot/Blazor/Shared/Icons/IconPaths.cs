using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Heroicons-outline (24x24, <c>stroke</c>-based) <c>d</c> path constants, harvested from the
/// Razor Pages partials as they're ported to Blazor (see
/// <c>docs/articles/blazor-components.md</c> "Component contract", point 8). Named by Heroicon
/// name, not by which partial first used them - all tiers add to this single class rather than
/// introducing a second icon-constants file (it was briefly split into a per-tier <c>partial</c>
/// file each during Tiers 1-5 so parallel work didn't collide on one source file; those files were
/// merged back into this one at the end of Phase 2 once nine names collided under that scheme -
/// see the merge note below). Consume via <c>&lt;Icon Path="IconPaths.XMark" /&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// This does <b>not</b> cover every icon in the codebase: <c>_Badge.cshtml</c>'s <c>IconLeft</c>
/// is a 20x20 <i>filled</i> Heroicon sized by the <c>.badge svg</c> CSS rule, not the 24x24
/// stroke-outline family these paths belong to, so <c>Badge.razor</c> reproduces that markup
/// literally instead of going through <see cref="Icon"/>/<see cref="IconPaths"/>.
/// </para>
/// <para>
/// Merge note (<c>docs/plans/blazor-port-plan.md</c> §5 Phase 2 step 1): five names collided
/// across tiers with byte-identical path data (<see cref="ArrowPath"/>,
/// <see cref="ExclamationCircleOutline"/>, <see cref="ChevronRight"/>, and one of the two
/// <see cref="Check"/> collisions) and were trivially merged to one constant. Four names collided
/// with <i>different</i> path data because two source partials render the same Heroicon concept
/// from different Heroicons revisions (<see cref="Check"/> the second time, <see cref="ShieldCheck"/>,
/// <see cref="SpeakerWave"/>, <see cref="Microphone"/>) or a hand-approximated variant that matches
/// no published Heroicons revision (<see cref="Pause"/>); for those, the constant now holds
/// whichever candidate verifies byte-for-byte against the current published Heroicons 24x24
/// outline SVG (<c>github.com/tailwindlabs/heroicons</c>, <c>optimized/24/outline/*.svg</c>) -
/// <see cref="Pause"/>, which matches neither the v1 nor the v2 outline exactly, keeps the
/// candidate that reproduces the real Heroicons v1 <c>pause-circle</c> bars rather than the other
/// candidate's non-canonical coordinates. Every component that referenced the dropped variant
/// keeps compiling unchanged (same constant name) but now renders the kept shape; each affected
/// component's file header carries a "Fidelity deviation" note pointing back here.
/// </para>
/// </remarks>
public static class IconPaths
{
    /// <summary>Heroicon <c>adjustments-horizontal</c>. Standard mode's icon in <c>_ModeSwitcher.cshtml</c>.</summary>
    public const string AdjustmentsHorizontal = "M10.5 6h9.75M10.5 6a1.5 1.5 0 1 1-3 0m3 0a1.5 1.5 0 1 0-3 0M3.75 6H7.5m3 12h9.75m-9.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-3.75 0H7.5m9-6h3.75m-3.75 0a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m-9.75 0h9.75";

    /// <summary>Heroicon <c>arrow-down</c>. Downward trend in <c>_HeroMetricCard.cshtml</c>.</summary>
    public const string ArrowDown = "M19 14l-7 7m0 0l-7-7m7 7V3";

    /// <summary>Heroicon <c>arrow-path</c>. "Restart Required" badges (<c>_SettingField.cshtml</c>, <c>_RestartBanner.cshtml</c>).</summary>
    public const string ArrowPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15";

    /// <summary>Heroicon <c>arrow-right</c>. <c>_GuildContextSelector.cshtml</c>'s "Open in {guild}" trailing icon.</summary>
    public const string ArrowRight = "M13 7l5 5m0 0l-5 5m5-5H6";

    /// <summary>Heroicon <c>arrow-right-on-rectangle</c>. "Leave" voice channel button in <c>_VoiceChannelPanel.cshtml</c>.</summary>
    public const string ArrowRightOnRectangle = "M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1";

    /// <summary>Heroicon <c>arrow-up</c>. Upward trend in <c>_HeroMetricCard.cshtml</c>.</summary>
    public const string ArrowUp = "M5 10l7-7m0 0l7 7m-7-7v18";

    /// <summary>Heroicon <c>bars-arrow-down</c>. <c>_SortDropdown.cshtml</c>'s toggle button.</summary>
    public const string BarsArrowDown = "M3 4h13M3 8h9m-9 4h6m4 0l4-4m0 0l4 4m-4-4v12";

    /// <summary>Heroicon <c>bell</c>. Navbar notification bell button and its empty state.</summary>
    public const string Bell = "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9";

    /// <summary>Heroicon <c>bolt</c> (v1 coordinates). <c>_EmphasisToolbar.cshtml</c>'s moderate-emphasis button.</summary>
    public const string Bolt = "M13 10V3L4 14h7v7l9-11h-7z";

    /// <summary>Heroicon <c>bolt</c> (v2 coordinates, distinct from <see cref="Bolt"/>). Pro mode's icon in <c>_ModeSwitcher.cshtml</c>.</summary>
    public const string BoltOutline = "m3.75 13.5 10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75Z";

    /// <summary>Heroicon <c>book-open</c>. Activity feed empty state.</summary>
    public const string BookOpen = "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2";

    /// <summary>Heroicon <c>calendar</c>. <c>_EmphasisToolbar.cshtml</c>'s "say as date" button.</summary>
    public const string Calendar = "M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5";

    /// <summary>Heroicon <c>chart-bar</c>. Command usage card header.</summary>
    public const string ChartBar = "M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z";

    /// <summary>Heroicon <c>chart-bar-square</c>. Total commands executed metric.</summary>
    public const string ChartBarSquare = "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z";

    /// <summary>Heroicon <c>check</c> (v2 coordinates, verified against the published outline SVG). <c>_VoiceSelector.cshtml</c>'s selected indicator, <c>_SortDropdown.cshtml</c>'s selected option, <c>_PresetBar.cshtml</c>, the notification bell "mark all read" button.</summary>
    public const string Check = "M4.5 12.75l6 6 9-13.5";

    /// <summary>Heroicon <c>check-circle</c>. <see cref="AlertVariant.Success"/>.</summary>
    public const string CheckCircle = "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>chevron-double-left</c>. "First page" pagination control (<see cref="DiscordBot.Bot.ViewModels.Components.PaginationViewModel.ShowFirstLast"/>).</summary>
    public const string ChevronDoubleLeft = "M11 19l-7-7 7-7m8 14l-7-7 7-7";

    /// <summary>Heroicon <c>chevron-double-right</c>. "Last page" pagination control.</summary>
    public const string ChevronDoubleRight = "M13 5l7 7-7 7M5 5l7 7-7 7";

    /// <summary>Heroicon <c>chevron-down</c>. <c>_Card</c>/<c>_EnhancedCard</c>'s collapsible-header toggle.</summary>
    public const string ChevronDown = "M19 9l-7 7-7-7";

    /// <summary>Heroicon <c>chevron-left</c>. "Previous page" pagination controls.</summary>
    public const string ChevronLeft = "M15 19l-7-7 7-7";

    /// <summary>Heroicon <c>chevron-right</c>. Breadcrumb separators, pagination "next", "view all" links.</summary>
    public const string ChevronRight = "M9 5l7 7-7 7";

    /// <summary>Heroicon <c>clipboard-document</c>-family "copy" glyph. "Copy Server ID" action in <c>_ConnectedServersWidget.cshtml</c>.</summary>
    public const string ClipboardCopy = "M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z";

    /// <summary>Heroicon <c>clock</c>. Recent Activity card header.</summary>
    public const string Clock = "M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>code-bracket</c>. "CommandError" notification type icon.</summary>
    public const string CodeBracket = "M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4";

    /// <summary>Heroicon <c>computer-desktop</c>. The "Robot" TTS preset.</summary>
    public const string ComputerDesktop = "M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25";

    /// <summary>Heroicon <c>document-duplicate</c>. <c>_SsmlPreview.cshtml</c>'s "Copy SSML" button.</summary>
    public const string DocumentDuplicate = "M8 7v8a2 2 0 0 0 2 2h6M8 7V5a2 2 0 0 1 2-2h4.586a1 1 0 0 1 .707.293l4.414 4.414a1 1 0 0 1 .293.707V15a2 2 0 0 1-2 2h-2M8 7H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-2";

    /// <summary>Heroicon <c>document-text</c>. Audit log card header/empty state and the command stats "document" empty state.</summary>
    public const string DocumentText = "M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z";

    /// <summary>Heroicon <c>ellipsis-vertical</c>. Connected Servers per-row action menu trigger.</summary>
    public const string EllipsisVertical = "M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z";

    /// <summary>Heroicon <c>envelope-open</c>. Per-item "mark as read" notification action.</summary>
    public const string EnvelopeOpen = "M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z";

    /// <summary>Heroicon <c>eye</c> (single <c>&lt;path&gt;</c> combining the icon's two subpaths - safe because both share the same stroke/no-fill styling). Connected Servers "view details" action.</summary>
    public const string Eye = "M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z";

    /// <summary>Heroicon <c>exclamation-circle</c> (v2 coordinates). <see cref="EmptyStateType.Error"/>.</summary>
    public const string ExclamationCircle = "M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z";

    /// <summary>Heroicon <c>exclamation-circle</c> (v1 coordinates, distinct from <see cref="ExclamationCircle"/>). <c>_FormInput.cshtml</c>/<c>_FormSelect.cshtml</c>'s validation-error icon and <c>_RuleTypeIcon.cshtml</c>'s <see cref="DiscordBot.Core.Enums.RuleType.Spam"/> glyph.</summary>
    public const string ExclamationCircleOutline = "M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>exclamation-triangle</c>. <see cref="AlertVariant.Warning"/>.</summary>
    public const string ExclamationTriangle = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z";

    /// <summary>Heroicon <c>face-frown</c>. The <c>sad</c> TTS style.</summary>
    public const string FaceFrown = "M15.182 16.318A4.486 4.486 0 0 0 12.016 15a4.486 4.486 0 0 0-3.198 1.318M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9.75-3.75h.008v.008h-.008V8.25Zm6 0h.008v.008h-.008V8.25Z";

    /// <summary>Heroicon <c>face-smile</c>. The <c>cheerful</c> TTS style.</summary>
    public const string FaceSmile = "M15.182 15.182a4.5 4.5 0 0 1-6.364 0M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0ZM9.75 9.75c0 .414-.168.75-.375.75S9 10.164 9 9.75 9.168 9 9.375 9s.375.336.375.75Zm-.375 0h.008v.015h-.008V9.75Zm5.625 0c0 .414-.168.75-.375.75s-.375-.336-.375-.75.168-.75.375-.75.375.336.375.75Zm-.375 0h.008v.015h-.008V9.75Z";

    /// <summary>Heroicon <c>fire</c>. The <c>angry</c> TTS style and "Angry" preset.</summary>
    public const string Fire = "M15.362 5.214A8.252 8.252 0 0 1 12 21 8.25 8.25 0 0 1 6.038 7.047 8.287 8.287 0 0 0 9 9.601a8.983 8.983 0 0 1 3.361-6.867 8.21 8.21 0 0 0 3 2.48Z";

    /// <summary>Heroicon <c>folder-open</c>. <see cref="EmptyStateType.NoData"/> (default).</summary>
    public const string FolderOpen = "M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z";

    /// <summary>Heroicon <c>funnel</c>. <c>FilterPanelTagHelper</c>'s header icon.</summary>
    public const string Funnel = "M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z";

    /// <summary>Heroicon <c>hand-raised</c>. The <c>friendly</c> TTS style.</summary>
    public const string HandRaised = "M10.05 4.575a1.575 1.575 0 1 0-3.15 0v3m3.15-3v-1.5a1.575 1.575 0 0 1 3.15 0v1.5m-3.15 0 .075 5.925m3.075.75V4.575m0 0a1.575 1.575 0 0 1 3.15 0V15M6.9 7.575a1.575 1.575 0 1 0-3.15 0v8.175a6.75 6.75 0 0 0 6.75 6.75h2.018a5.25 5.25 0 0 0 3.712-1.538l1.732-1.732a5.25 5.25 0 0 0 1.538-3.712l.003-2.024a.668.668 0 0 1 .198-.471 1.575 1.575 0 1 0-2.228-2.228 3.818 3.818 0 0 0-1.12 2.687M6.9 7.575V12m6.27 4.318A4.49 4.49 0 0 1 16.35 15";

    /// <summary>Heroicon <c>hashtag</c>. Text-channel autocomplete results.</summary>
    public const string Hashtag = "M7 20l4-16m2 16l4-16M6 9h14M4 15h14";

    /// <summary>Heroicon <c>home</c>. <c>_GuildContextSelector.cshtml</c>'s "Open in {guild}" link / dropdown trigger.</summary>
    public const string Home = "M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25";

    /// <summary>Heroicon <c>information-circle</c>. <see cref="AlertVariant.Info"/> (default).</summary>
    public const string InformationCircle = "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>bolt</c> (same shape as <see cref="Bolt"/>, kept under its own name). Live Activity card header.</summary>
    public const string Lightning = "M13 10V3L4 14h7v7l9-11h-7z";

    /// <summary>Heroicon <c>lock-closed</c>. <see cref="EmptyStateType.NoPermission"/>.</summary>
    public const string LockClosed = "M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z";

    /// <summary>Heroicon <c>magnifying-glass</c>. <see cref="EmptyStateType.NoResults"/>.</summary>
    public const string MagnifyingGlass = "M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z";

    /// <summary>Heroicon <c>megaphone</c>. The "Announcer" TTS preset.</summary>
    public const string Megaphone = "M10.34 15.84c-.688-.06-1.386-.09-2.09-.09H7.5a4.5 4.5 0 1 1 0-9h.75c.704 0 1.402-.03 2.09-.09m0 9.18c.253.962.584 1.892.985 2.783.247.55.06 1.21-.463 1.511l-.657.38a.75.75 0 0 1-1.024-.272 21.37 21.37 0 0 1-1.26-2.672.747.747 0 0 1-.015-.065c-.255-.906-.47-1.834-.617-2.784m2.95 1.13c.62.057 1.246.09 1.879.09h.75a4.5 4.5 0 0 0 0-9h-.75c-.633 0-1.26.03-1.88.09m-.97 7.5v1.875c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V15.84m4.5 0V15.84";

    /// <summary>Heroicon <c>microphone</c> (v2 coordinates, verified against the published outline SVG). <c>_VoiceChannelPanel.cshtml</c>'s connection status, the "Narrator" TTS preset, and Simple mode's icon.</summary>
    public const string Microphone = "M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z";

    /// <summary>Heroicon <c>minus</c>. <see cref="DiscordBot.Bot.ViewModels.Components.TrendDirection.Neutral"/> (no arrowhead).</summary>
    public const string Minus = "M20 12H4";

    /// <summary>Heroicon <c>newspaper</c>. The <c>newscast</c> TTS style.</summary>
    public const string Newspaper = "M12 7.5h1.5m-1.5 3h1.5m-7.5 3h7.5m-7.5 3h7.5m3-9h3.375c.621 0 1.125.504 1.125 1.125V18a2.25 2.25 0 0 1-2.25 2.25M16.5 7.5V18a2.25 2.25 0 0 0 2.25 2.25M16.5 7.5V4.875c0-.621-.504-1.125-1.125-1.125H4.125C3.504 3.75 3 4.254 3 4.875V18a2.25 2.25 0 0 0 2.25 2.25h13.5M6 7.5h3v3H6v-3Z";

    /// <summary>Heroicon <c>pause</c> (bars-only, matching the real Heroicons v1 <c>pause-circle</c> glyph with its ring stripped - the other candidate matched no published Heroicons revision). Activity feed pause/resume toggle and <c>_EmphasisToolbar.cshtml</c>'s "insert pause" button.</summary>
    public const string Pause = "M10 9v6m4-6v6";

    /// <summary>Heroicon <c>play</c>. Activity feed pause/resume toggle, playing state.</summary>
    public const string Play = "M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z";

    /// <summary>Heroicon <c>plus</c> (v1 coordinates). <see cref="EmptyStateType.NoData"/>'s primary action button (<c>_EmptyState.cshtml</c>).</summary>
    public const string Plus = "M12 4v16m8-8H4";

    /// <summary>Heroicon <c>plus</c> (v2 coordinates, distinct from <see cref="Plus"/>). <c>_PresetBar.cshtml</c>'s "Save preset" button.</summary>
    public const string PlusOutline = "M12 4.5v15m7.5-7.5h-15";

    /// <summary>Heroicon <c>question-mark-circle</c>. The unmapped/unknown <c>RuleType</c> fallback.</summary>
    public const string QuestionMarkCircle = "M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>queue-list</c> (empty variant). "No sounds queued" state in <c>_VoiceChannelPanel.cshtml</c>.</summary>
    public const string QueueListEmpty = "M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10";

    /// <summary>Heroicon <c>arrow-path</c> (same shape as <see cref="ArrowPath"/>, kept under its own name). Refresh buttons on the Recent Activity and Bot Status widgets.</summary>
    public const string Refresh = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15";

    /// <summary>Heroicon <c>server</c>. <c>_GuildStatsCard.cshtml</c>'s guild-count icon badge.</summary>
    public const string Server = "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01";

    /// <summary>Heroicon <c>server-stack</c>. Connected Servers empty state.</summary>
    public const string ServerStack = "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01";

    /// <summary>Heroicon <c>shield-check</c> (v2 coordinates, verified against the published outline SVG). <c>_RuleTypeIcon.cshtml</c>'s <see cref="DiscordBot.Core.Enums.RuleType.Raid"/> glyph, the Bot Status online state, and the "BotStatus" notification type icon.</summary>
    public const string ShieldCheck = "M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z";

    /// <summary>Heroicon <c>shield-exclamation</c>. Bot Status offline/disconnected state.</summary>
    public const string ShieldExclamation = "M20.618 5.984A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016zM12 9v2m0 4h.01";

    /// <summary>Heroicon <c>signal-slash</c>. <see cref="EmptyStateType.Offline"/>.</summary>
    public const string SignalSlash = "M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808 9.98-3.808 13.788 0M1.924 8.674c5.565-5.565 14.587-5.565 20.152 0M12.53 18.22l-.53.53-.53-.53a.75.75 0 011.06 0z";

    /// <summary>Heroicon <c>sparkles</c>. <see cref="EmptyStateType.FirstTime"/>.</summary>
    public const string Sparkles = "M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 00-2.456 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z";

    /// <summary>Heroicon <c>speaker-wave</c> (v2 coordinates, verified against the published outline SVG). The <c>shouting</c> TTS style, the "Shouting" preset and the "speaker-wave" preset icon.</summary>
    public const string SpeakerWave = "M19.114 5.636a9 9 0 0 1 0 12.728M16.463 8.288a5.25 5.25 0 0 1 0 7.424M6.75 8.25l4.72-4.72a.75.75 0 0 1 1.28.53v15.88a.75.75 0 0 1-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.009 9.009 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z";

    /// <summary>Heroicon <c>speaker-x-mark</c>. The <c>whispering</c> TTS style and "Whisper" preset.</summary>
    public const string SpeakerXMark = "M17.25 9.75 19.5 12m0 0 2.25 2.25M19.5 12l2.25-2.25M19.5 12l-2.25 2.25m-10.5-6 4.72-4.72a.75.75 0 0 1 1.28.53v15.88a.75.75 0 0 1-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.009 9.009 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z";

    /// <summary>Heroicon <c>star</c>. The custom-preset button icon in <c>_PresetBar.cshtml</c>.</summary>
    public const string Star = "M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z";

    /// <summary>Heroicon <c>user</c>. <c>autocomplete.js</c>'s default result icon.</summary>
    public const string User = "M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z";

    /// <summary>Heroicon <c>users</c>. "GuildEvent" notification type icon.</summary>
    public const string Users = "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z";

    /// <summary>Heroicon <c>x-circle</c>. <see cref="AlertVariant.Error"/>.</summary>
    public const string XCircle = "M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Heroicon <c>x-mark</c>. <c>_Button</c>'s loading-state icon slot, <c>_Alert</c>'s dismiss button, <c>_Card</c>/<c>_EnhancedCard</c>'s collapse chevron sibling.</summary>
    public const string XMark = "M6 18L18 6M6 6l12 12";
}
