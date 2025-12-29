// src/DiscordBot.Bot/ViewModels/Components/QuickActionItemViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for a single quick action item.
/// </summary>
public record QuickActionItemViewModel
{
    /// <summary>
    /// Gets the unique identifier for this action.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the label text displayed for the action.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SVG path for the icon.
    /// </summary>
    public string IconPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the color theme for the action.
    /// </summary>
    public QuickActionColor Color { get; init; } = QuickActionColor.Blue;

    /// <summary>
    /// Gets the type of action (Link or PostAction).
    /// </summary>
    public QuickActionType ActionType { get; init; } = QuickActionType.Link;

    /// <summary>
    /// Gets the URL for link actions.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Gets the POST handler name for post actions.
    /// </summary>
    public string? Handler { get; init; }

    /// <summary>
    /// Gets a value indicating whether this action requires confirmation.
    /// </summary>
    public bool RequiresConfirmation { get; init; } = false;

    /// <summary>
    /// Gets the confirmation modal ID to show.
    /// </summary>
    public string? ConfirmationModalId { get; init; }

    /// <summary>
    /// Gets a value indicating whether this action is admin-only.
    /// </summary>
    public bool IsAdminOnly { get; init; } = false;
}

/// <summary>
/// Color themes for quick action items.
/// </summary>
public enum QuickActionColor
{
    /// <summary>
    /// Orange accent color (primary actions).
    /// </summary>
    Orange,

    /// <summary>
    /// Blue accent color (informational actions).
    /// </summary>
    Blue,

    /// <summary>
    /// Green/success color (positive actions).
    /// </summary>
    Success,

    /// <summary>
    /// Warning/amber color (cautionary actions).
    /// </summary>
    Warning,

    /// <summary>
    /// Info/cyan color (analytics and data).
    /// </summary>
    Info,

    /// <summary>
    /// Error/red color (destructive actions).
    /// </summary>
    Error,

    /// <summary>
    /// Gray/neutral color (settings and configuration).
    /// </summary>
    Gray
}

/// <summary>
/// Types of quick actions.
/// </summary>
public enum QuickActionType
{
    /// <summary>
    /// Navigation link to another page.
    /// </summary>
    Link,

    /// <summary>
    /// POST action to the server.
    /// </summary>
    PostAction
}
