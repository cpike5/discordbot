// src/DiscordBot.Bot/ViewModels/Components/LoadingSpinnerViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record LoadingSpinnerViewModel
{
    public SpinnerVariant Variant { get; init; } = SpinnerVariant.Simple;
    public SpinnerSize Size { get; init; } = SpinnerSize.Medium;
    public string? Message { get; init; }
    public string? SubMessage { get; init; }
    public SpinnerColor Color { get; init; } = SpinnerColor.Blue;
    public bool IsOverlay { get; init; } = false;  // Full container overlay
}

public enum SpinnerVariant
{
    Simple,     // Rotating circle
    Dots,       // Three bouncing dots
    Pulse       // Pulsing circle with ring
}

public enum SpinnerSize
{
    Small,      // 24px
    Medium,     // 40px
    Large       // 64px
}

public enum SpinnerColor
{
    Blue,       // accent-blue (default)
    Orange,     // accent-orange
    White       // For dark backgrounds
}
