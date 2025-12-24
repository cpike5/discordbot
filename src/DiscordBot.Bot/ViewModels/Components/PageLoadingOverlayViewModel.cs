// src/DiscordBot.Bot/ViewModels/Components/PageLoadingOverlayViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record PageLoadingOverlayViewModel
{
    public string Id { get; init; } = "pageLoadingOverlay";
    public SpinnerVariant Variant { get; init; } = SpinnerVariant.Simple;
    public SpinnerSize Size { get; init; } = SpinnerSize.Large;
    public string? Message { get; init; } = "Loading...";
    public string? SubMessage { get; init; }
    public bool ShowCancelButton { get; init; } = false;
    public string CancelText { get; init; } = "Cancel";
}
