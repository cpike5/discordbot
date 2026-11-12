// src/DiscordBot.Bot/ViewModels/Components/PageHeaderViewModel.cs
using Microsoft.AspNetCore.Html;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for <c>_PageHeader</c>, the one page header every portal page uses: an optional
/// breadcrumb, an eyebrow, the page's single <c>&lt;h1&gt;</c>, a subtitle, a badge and actions.
/// </summary>
/// <remarks>
/// Top-level pages set <see cref="Eyebrow"/> to their sidebar group ("Overview", "Administration")
/// and pass no breadcrumb. A breadcrumb is drawn only for a page more than one level deep
/// (<see cref="ShowBreadcrumbs"/>), so <c>Home › Users</c> is never shown but
/// <c>Home › Users › Jane</c> is. <c>_GuildHeader</c> builds one of these for every guild page.
/// </remarks>
public record PageHeaderViewModel
{
    /// <summary>
    /// The trail to this page, Home first and this page last (its <see cref="BreadcrumbItem.Url"/> may be null).
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; init; } = [];

    /// <summary>
    /// Small uppercase label above the title: the sidebar group for top-level pages.
    /// </summary>
    public string? Eyebrow { get; init; }

    /// <summary>
    /// Markup drawn in the eyebrow's place, for a richer context line (the guild switcher).
    /// Takes precedence over <see cref="Eyebrow"/>.
    /// </summary>
    public IHtmlContent? EyebrowContent { get; init; }

    /// <summary>
    /// Markup drawn left of the text block, such as a guild icon or an avatar.
    /// </summary>
    public IHtmlContent? Leading { get; init; }

    /// <summary>
    /// The page title, rendered as the page's only <c>&lt;h1&gt;</c>.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The title's heading level: 1 on every real page. Only the /Components showcase, which draws
    /// several headers on one page, passes 2 so that page keeps a single <c>&lt;h1&gt;</c>.
    /// </summary>
    public int HeadingLevel { get; init; } = 1;

    /// <summary>
    /// Optional line below the title. It carries <c>data-page-subtitle</c> so a script can change it.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// Optional badge beside the title (a count, a status). Its wrapper carries <c>data-page-badge</c>.
    /// </summary>
    public BadgeViewModel? Badge { get; init; }

    /// <summary>
    /// Link or POST buttons on the right of the header.
    /// </summary>
    public IReadOnlyList<HeaderAction> Actions { get; init; } = [];

    /// <summary>
    /// Free-form markup on the right of the header, after <see cref="Actions"/>, for anything the
    /// typed actions cannot express (a confirm form, a status pill, a bulk-action group).
    /// </summary>
    public IHtmlContent? ActionsContent { get; init; }

    /// <summary>
    /// True when the breadcrumb is drawn: only for a page more than one level below Home.
    /// </summary>
    public bool ShowBreadcrumbs => Breadcrumbs.Count > 2;

    /// <summary>
    /// True when the right-hand action area has anything in it.
    /// </summary>
    public bool HasActions => Actions.Count > 0 || ActionsContent != null;
}
