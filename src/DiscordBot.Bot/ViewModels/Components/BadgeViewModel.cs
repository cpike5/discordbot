// src/DiscordBot.Bot/ViewModels/Components/BadgeViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record BadgeViewModel
{
    public string Text { get; init; } = string.Empty;
    public BadgeVariant Variant { get; init; } = BadgeVariant.Default;
    public BadgeSize Size { get; init; } = BadgeSize.Medium;
    public BadgeStyle Style { get; init; } = BadgeStyle.Filled;
    public string? IconLeft { get; init; }
    public bool IsRemovable { get; init; } = false;
    public string? OnRemove { get; init; }
}

public enum BadgeVariant
{
    Default,    // Gray
    Orange,     // Primary accent
    Blue,       // Secondary accent
    Success,    // Green
    Warning,    // Amber
    Error,      // Red
    Info        // Cyan
}

public enum BadgeSize
{
    Small,      // px-2 py-0.5 text-[10px]
    Medium,     // px-3 py-1 text-xs
    Large       // px-4 py-1.5 text-sm
}

public enum BadgeStyle
{
    Filled,     // Solid background
    Outline,    // Border only
    Subtle      // Muted background with colored text (e.g., bg-success/10 text-success)
}
