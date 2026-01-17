// src/DiscordBot.Bot/ViewModels/Components/GuildBreadcrumbViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for guild breadcrumb navigation component.
/// </summary>
public record GuildBreadcrumbViewModel
{
    /// <summary>
    /// List of breadcrumb items to display.
    /// </summary>
    public List<BreadcrumbItem> Items { get; init; } = new();
}

/// <summary>
/// Represents a single item in the breadcrumb navigation.
/// </summary>
public record BreadcrumbItem
{
    /// <summary>
    /// Display text for the breadcrumb item.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// URL to navigate to. Null for the current page.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Whether this is the current page (last item in breadcrumb).
    /// </summary>
    public bool IsCurrent { get; init; }
}
