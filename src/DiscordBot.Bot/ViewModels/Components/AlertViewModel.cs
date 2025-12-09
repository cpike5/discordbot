// src/DiscordBot.Bot/ViewModels/Components/AlertViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record AlertViewModel
{
    public AlertVariant Variant { get; init; } = AlertVariant.Info;
    public string? Title { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsDismissible { get; init; } = false;
    public bool ShowIcon { get; init; } = true;
    public string? DismissCallback { get; init; } // JavaScript function name
}

public enum AlertVariant
{
    Info,       // Cyan/blue - informational
    Success,    // Green - success/confirmation
    Warning,    // Amber - caution
    Error       // Red - error/danger
}
