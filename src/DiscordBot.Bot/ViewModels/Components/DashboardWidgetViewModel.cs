// src/DiscordBot.Bot/ViewModels/Components/DashboardWidgetViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record DashboardWidgetViewModel
{
    /// <summary>
    /// Widget title displayed in header
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Optional subtitle displayed below title
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// URL the header links to (nullable for no link)
    /// </summary>
    public string? DetailUrl { get; init; }

    /// <summary>
    /// Text for detail link (default "View All →")
    /// </summary>
    public string DetailLinkText { get; init; } = "View All →";

    /// <summary>
    /// Optional icon SVG path next to title
    /// </summary>
    public string? IconSvgPath { get; init; }

    /// <summary>
    /// Whether the feature is enabled (for showing enabled/disabled badge)
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Label for enabled status badge (default "Enabled")
    /// </summary>
    public string EnabledLabel { get; init; } = "Enabled";

    /// <summary>
    /// Label for disabled status badge (default "Disabled")
    /// </summary>
    public string DisabledLabel { get; init; } = "Disabled";

    /// <summary>
    /// HTML content for the widget body
    /// </summary>
    public string? BodyContent { get; init; }

    /// <summary>
    /// Optional EmptyStateViewModel for no-data state
    /// </summary>
    public EmptyStateViewModel? EmptyState { get; init; }

    /// <summary>
    /// Grid column span (1 or 2 for full-width)
    /// </summary>
    public int ColSpan { get; init; } = 1;

    /// <summary>
    /// Additional header action links (for Settings, Analytics, etc.)
    /// </summary>
    public List<WidgetHeaderAction>? HeaderActions { get; init; }
}

/// <summary>
/// Represents an additional header action link (e.g., Settings, Analytics)
/// </summary>
public record WidgetHeaderAction
{
    /// <summary>
    /// Text displayed for the action link
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// URL the action link navigates to
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
