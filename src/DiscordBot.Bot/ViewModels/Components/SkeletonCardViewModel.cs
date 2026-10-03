// src/DiscordBot.Bot/ViewModels/Components/SkeletonCardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_SkeletonCard</c>. <c>wwwroot/js/skeleton.js</c> draws the same shapes from script.
/// </summary>
public record SkeletonCardViewModel
{
    public SkeletonCardType Type { get; init; } = SkeletonCardType.Stats;
    public bool ShowHeader { get; init; } = false;
    public string? CssClass { get; init; }

    /// <summary>
    /// Screen reader text for the placeholder (for example "Loading servers"). Leave it null when
    /// several cards share a region that announces once; the visual blocks are always hidden.
    /// </summary>
    public string? Label { get; init; }
}

public enum SkeletonCardType
{
    Stats,          // Stats card: icon + value + label
    Server,         // Server card: avatar + name + stats row
    Activity,       // Activity feed: icon + 2 lines
    Table,          // Table row: avatar + name + columns
    List,           // Avatar + two lines, repeated
    Form            // Labelled fields and a button
}
