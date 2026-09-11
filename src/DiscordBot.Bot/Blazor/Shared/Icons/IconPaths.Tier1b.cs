namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Tier 1b additions to <see cref="IconPaths"/> - see that file's remarks for the naming
/// convention. Kept in its own file (per <c>docs/articles/blazor-components.md</c>, "Component
/// contract") so later tiers building in parallel don't all edit <c>IconPaths.cs</c>.
/// </summary>
public static partial class IconPaths
{
    // ---- Pagination / breadcrumb chevrons (_Pagination.cshtml, _Breadcrumb.cshtml family) -----

    /// <summary>Rightward chevron. Breadcrumb separators and "next page"/single-chevron
    /// pagination controls.</summary>
    public const string ChevronRight = "M9 5l7 7-7 7";

    /// <summary>Leftward chevron. "Previous page" pagination controls.</summary>
    public const string ChevronLeft = "M15 19l-7-7 7-7";

    /// <summary>Double leftward chevron. "First page" pagination control
    /// (<see cref="DiscordBot.Bot.ViewModels.Components.PaginationViewModel.ShowFirstLast"/>).</summary>
    public const string ChevronDoubleLeft = "M11 19l-7-7 7-7m8 14l-7-7 7-7";

    /// <summary>Double rightward chevron. "Last page" pagination control.</summary>
    public const string ChevronDoubleRight = "M13 5l7 7-7 7M5 5l7 7-7 7";

    // ---- Trend arrows (_HeroMetricCard.cshtml) ------------------------------------------------

    /// <summary>Upward arrow. <see cref="DiscordBot.Bot.ViewModels.Components.TrendDirection.Up"/>.</summary>
    public const string ArrowUp = "M5 10l7-7m0 0l7 7m-7-7v18";

    /// <summary>Downward arrow. <see cref="DiscordBot.Bot.ViewModels.Components.TrendDirection.Down"/>.</summary>
    public const string ArrowDown = "M19 14l-7 7m0 0l-7-7m7 7V3";

    /// <summary>Horizontal line (no arrowhead).
    /// <see cref="DiscordBot.Bot.ViewModels.Components.TrendDirection.Neutral"/>.</summary>
    public const string Minus = "M20 12H4";

    // ---- _GuildStatsCard.cshtml -----------------------------------------------------------------

    /// <summary>Stacked server racks. The guild-count icon badge.</summary>
    public const string Server = "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01";

    // ---- _RuleTypeIcon.cshtml -------------------------------------------------------------------

    /// <summary>Exclamation inside a circle (Heroicons v1 outline shape - distinct path data from
    /// <see cref="ExclamationCircle"/>, which is the v2 shape <c>_EmptyState</c> uses).
    /// <see cref="DiscordBot.Core.Enums.RuleType.Spam"/>.</summary>
    public const string ExclamationCircleOutline = "M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";

    /// <summary>Shield with a checkmark. <see cref="DiscordBot.Core.Enums.RuleType.Raid"/>.</summary>
    public const string ShieldCheck = "M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z";

    /// <summary>Question mark inside a circle. The unmapped/unknown <c>RuleType</c> fallback.</summary>
    public const string QuestionMarkCircle = "M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z";
}
