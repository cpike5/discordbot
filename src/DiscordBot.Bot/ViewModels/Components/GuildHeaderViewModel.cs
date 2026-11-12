// src/DiscordBot.Bot/ViewModels/Components/GuildHeaderViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for guild header component displaying guild info, page title, and action buttons.
/// </summary>
public record GuildHeaderViewModel
{
    /// <summary>
    /// Discord guild ID (Snowflake).
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Guild name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// URL to guild icon image. If null, displays initials fallback.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Main page title displayed in the header.
    /// </summary>
    public string PageTitle { get; init; } = string.Empty;

    /// <summary>
    /// Optional description text displayed below the title.
    /// </summary>
    public string? PageDescription { get; init; }

    /// <summary>
    /// Optional list of action buttons to display in the header.
    /// </summary>
    public List<HeaderAction>? Actions { get; set; }

    /// <summary>
    /// Optional status badge displayed in the header (separate from action buttons).
    /// </summary>
    public BadgeViewModel? StatusBadge { get; set; }

    /// <summary>
    /// The guilds the user can switch to, the current one included, in display order. Filled by
    /// <c>_GuildLayout</c> from <c>IGuildSwitcherService</c>, so every guild page gets it.
    /// </summary>
    public IReadOnlyList<GuildSwitcherOption> SwitchableGuilds { get; init; } = [];

    /// <summary>
    /// The breadcrumb trail drawn above the header. Filled by <c>_GuildLayout</c> from the page's
    /// <c>Breadcrumb</c>, so page models keep setting that one property.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; init; } = [];

    /// <summary>
    /// True when there is another guild to switch to: the guild name becomes the switcher button.
    /// </summary>
    public bool CanSwitchGuild => SwitchableGuilds.Any(g => !g.IsCurrent);
}

/// <summary>
/// One guild in the guild switcher.
/// </summary>
public record GuildSwitcherOption
{
    /// <summary>
    /// Discord guild ID, as a string (snowflakes do not fit a JavaScript number).
    /// </summary>
    public string GuildId { get; init; } = string.Empty;

    /// <summary>
    /// Guild name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Guild icon URL, or null for the initials fallback.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Where choosing this guild goes (see <c>GuildSwitchTarget</c>).
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// True for the guild the page is about.
    /// </summary>
    public bool IsCurrent { get; init; }
}

/// <summary>
/// Represents an action button in the guild header.
/// </summary>
public record HeaderAction
{
    /// <summary>
    /// Button label text.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// URL to navigate to when clicked.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Optional SVG icon path or CSS class.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Visual style of the button.
    /// </summary>
    public HeaderActionStyle Style { get; init; } = HeaderActionStyle.Secondary;

    /// <summary>
    /// Whether to open the link in a new tab.
    /// </summary>
    public bool OpenInNewTab { get; init; }

    /// <summary>
    /// When true, the action is rendered as a button that POSTs (with an antiforgery token)
    /// to <see cref="Url"/> instead of a link. Use for actions that change state.
    /// </summary>
    public bool IsPost { get; init; }
}

/// <summary>
/// Visual styles for header action buttons.
/// </summary>
public enum HeaderActionStyle
{
    /// <summary>
    /// Orange accent button with solid background.
    /// </summary>
    Primary,

    /// <summary>
    /// Bordered button with secondary background.
    /// </summary>
    Secondary,

    /// <summary>
    /// Text link style.
    /// </summary>
    Link
}
