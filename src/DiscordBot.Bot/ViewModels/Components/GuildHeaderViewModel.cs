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
    public List<HeaderAction>? Actions { get; init; }

    /// <summary>
    /// Optional status badge displayed in the header (separate from action buttons).
    /// </summary>
    public BadgeViewModel? StatusBadge { get; init; }
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
