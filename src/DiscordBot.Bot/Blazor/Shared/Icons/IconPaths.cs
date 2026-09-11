using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Heroicons-outline (24x24, <c>stroke</c>-based) <c>d</c> path constants, harvested from the
/// Razor Pages partials as they're ported to Blazor (see
/// <c>docs/articles/blazor-components.md</c> "Component contract", point 8). Named by Heroicon
/// name, not by which partial first used them - later tiers add to this class rather than
/// introducing a second icon-constants file. Consume via <c>&lt;Icon Path="IconPaths.XMark" /&gt;</c>.
/// </summary>
/// <remarks>
/// This does <b>not</b> cover every icon in the codebase: <c>_Badge.cshtml</c>'s <c>IconLeft</c>
/// is a 20x20 <i>filled</i> Heroicon sized by the <c>.badge svg</c> CSS rule, not the 24x24
/// stroke-outline family these paths belong to, so <c>Badge.razor</c> reproduces that markup
/// literally instead of going through <see cref="Icon"/>/<see cref="IconPaths"/>.
/// </remarks>
public static partial class IconPaths
{
    // ---- Generic ------------------------------------------------------------------------------

    /// <summary>Close/dismiss "X". From <c>_Button</c>'s loading state icon slot pattern,
    /// <c>_Alert</c>'s dismiss button, and <c>_Card</c>/<c>_EnhancedCard</c>'s collapse chevron
    /// sibling elements.</summary>
    public const string XMark = "M6 18L18 6M6 6l12 12";

    /// <summary>Downward chevron. From <c>_Card</c>/<c>_EnhancedCard</c>'s collapsible-header
    /// toggle.</summary>
    public const string ChevronDown = "M19 9l-7 7-7-7";

    /// <summary>Plus. From <c>_EmptyState</c>'s primary action button.</summary>
    public const string Plus = "M12 4v16m8-8H4";

    // ---- Alert variants (_Alert.cshtml) --------------------------------------------------------

    /// <summary>Check inside a circle. <see cref="AlertVariant.Success"/>.</summary>
    public const string CheckCircle = "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Exclamation inside a triangle. <see cref="AlertVariant.Warning"/>.</summary>
    public const string ExclamationTriangle = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z";

    /// <summary>"X" inside a circle. <see cref="AlertVariant.Error"/>.</summary>
    public const string XCircle = "M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>"i" inside a circle. <see cref="AlertVariant.Info"/> (default).</summary>
    public const string InformationCircle = "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    // ---- Empty state types (_EmptyState.cshtml) ------------------------------------------------

    /// <summary>Open folder. <see cref="EmptyStateType.NoData"/> (default).</summary>
    public const string FolderOpen = "M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z";

    /// <summary>Magnifying glass. <see cref="EmptyStateType.NoResults"/>.</summary>
    public const string MagnifyingGlass = "M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z";

    /// <summary>Three sparkle bursts. <see cref="EmptyStateType.FirstTime"/>.</summary>
    public const string Sparkles = "M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 00-2.456 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z";

    /// <summary>Exclamation inside a circle. <see cref="EmptyStateType.Error"/>.</summary>
    public const string ExclamationCircle = "M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z";

    /// <summary>Closed padlock. <see cref="EmptyStateType.NoPermission"/>.</summary>
    public const string LockClosed = "M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z";

    /// <summary>Wifi arcs with a slash. <see cref="EmptyStateType.Offline"/>.</summary>
    public const string SignalSlash = "M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808 9.98-3.808 13.788 0M1.924 8.674c5.565-5.565 14.587-5.565 20.152 0M12.53 18.22l-.53.53-.53-.53a.75.75 0 011.06 0z";
}
