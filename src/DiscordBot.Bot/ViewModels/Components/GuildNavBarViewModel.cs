// src/DiscordBot.Bot/ViewModels/Components/GuildNavBarViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for guild navigation bar component with tab navigation.
/// </summary>
public record GuildNavBarViewModel
{
    /// <summary>
    /// Discord guild ID (Snowflake).
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// ID of the currently active tab.
    /// </summary>
    public string ActiveTab { get; init; } = string.Empty;

    /// <summary>
    /// List of navigation tabs to display.
    /// </summary>
    public List<GuildNavItem> Tabs { get; init; } = new();
}

/// <summary>
/// Represents a single navigation tab item.
/// </summary>
public record GuildNavItem
{
    /// <summary>
    /// Unique identifier for the tab (e.g., "overview", "members").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display label for the tab.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Full Razor page path to navigate to (e.g., "/Guilds/Details").
    /// </summary>
    public string PageName { get; init; } = string.Empty;

    /// <summary>
    /// SVG path for outline icon (used when tab is not active).
    /// </summary>
    public string? IconOutline { get; init; }

    /// <summary>
    /// SVG path for solid icon (used when tab is active).
    /// </summary>
    public string? IconSolid { get; init; }

    /// <summary>
    /// Display order for the tab.
    /// </summary>
    public int Order { get; init; }
}
