// src/DiscordBot.Bot/ViewModels/Components/StatusIndicatorViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record StatusIndicatorViewModel
{
    public StatusType Status { get; init; } = StatusType.Offline;
    public string? Text { get; init; }  // Optional text label
    public StatusDisplayStyle DisplayStyle { get; init; } = StatusDisplayStyle.DotWithText;
    public bool IsPulsing { get; init; } = false;
    public StatusSize Size { get; init; } = StatusSize.Medium;
}

public enum StatusType
{
    Online,     // Green
    Idle,       // Yellow/Amber
    Busy,       // Red (Do Not Disturb)
    Offline     // Gray
}

public enum StatusDisplayStyle
{
    DotOnly,        // Just the colored dot
    DotWithText,    // Dot + status text
    BadgeStyle      // Pill badge with dot
}

public enum StatusSize
{
    Small,      // w-1.5 h-1.5
    Medium,     // w-2 h-2
    Large       // w-3 h-3
}
