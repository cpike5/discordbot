// src/DiscordBot.Bot/ViewModels/Components/SkeletonCardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record SkeletonCardViewModel
{
    public SkeletonCardType Type { get; init; } = SkeletonCardType.Stats;
    public bool ShowHeader { get; init; } = false;
    public string? CssClass { get; init; }
}

public enum SkeletonCardType
{
    Stats,          // Stats card: icon + value + label
    Server,         // Server card: avatar + name + stats row
    Activity,       // Activity feed: icon + 2 lines
    Table           // Table row: avatar + name + columns
}
