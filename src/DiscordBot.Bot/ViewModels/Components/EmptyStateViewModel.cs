// src/DiscordBot.Bot/ViewModels/Components/EmptyStateViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_EmptyState</c>. <c>wwwroot/js/empty-state.js</c> draws the same markup from
/// script, so a change to the layout or classes belongs in both.
/// </summary>
public record EmptyStateViewModel
{
    public EmptyStateType Type { get; init; } = EmptyStateType.NoData;
    public string Title { get; init; } = "No Data";
    public string Description { get; init; } = "There are no items to display.";

    /// <summary>Custom SVG path (24x24 outline icon) that replaces the icon the type would pick.</summary>
    public string? IconSvgPath { get; init; }

    /// <summary>The primary action's label. The action is a link when <see cref="PrimaryActionUrl"/> is set, else a button.</summary>
    public string? PrimaryActionText { get; init; }
    public string? PrimaryActionUrl { get; init; }

    /// <summary>
    /// Inline handler for the primary button. Static script only, never user data; prefer
    /// <see cref="PrimaryActionAttributes"/> (for example <c>data-action="retry"</c>) and a delegated listener.
    /// </summary>
    public string? PrimaryActionOnClick { get; init; }

    /// <summary>
    /// SVG path for the primary action's icon. Null keeps the default plus sign; an empty string
    /// shows no icon (right for "Retry" or "Clear filters").
    /// </summary>
    public string? PrimaryActionIconPath { get; init; }

    /// <summary>Extra attributes for the primary action (<c>data-*</c>, <c>id</c>). Values are encoded.</summary>
    public Dictionary<string, string>? PrimaryActionAttributes { get; init; }

    public string? SecondaryActionText { get; init; }
    public string? SecondaryActionUrl { get; init; }
    public EmptyStateSize Size { get; init; } = EmptyStateSize.Default;

    /// <summary>
    /// Heading level for the title (1-6). Default 3; set it to follow the page's heading
    /// outline rather than skip a level.
    /// </summary>
    public int HeadingLevel { get; init; } = 3;

    /// <summary>
    /// Marks the block as a polite live region (<c>role="status"</c>). Use it when the empty
    /// state appears after the page loaded (an AJAX list, a filter result), so it is announced.
    /// </summary>
    public bool Announce { get; init; } = false;

    /// <summary>Optional element ID, so a script can replace or clear the block.</summary>
    public string? Id { get; init; }
}

public enum EmptyStateType
{
    NoData,         // Folder icon - generic empty
    NoResults,      // Search icon with X - no search results
    FirstTime,      // Rocket/stars icon - onboarding
    Error,          // Warning icon - error loading
    NoPermission,   // Lock icon - access restricted
    Offline         // Wifi-off icon - no connection
}

public enum EmptyStateSize
{
    Compact,    // Smaller padding, icon, text
    Default,    // Standard size
    Large       // For full-page empty states
}
