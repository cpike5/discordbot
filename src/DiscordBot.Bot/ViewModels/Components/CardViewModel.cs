// src/DiscordBot.Bot/ViewModels/Components/CardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record CardViewModel
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public string? HeaderContent { get; init; }  // For custom header HTML
    public string? HeaderActions { get; init; }  // Buttons in header HTML
    public string? BodyContent { get; init; }    // Main content HTML
    public string? FooterContent { get; init; }  // Footer content HTML
    public CardVariant Variant { get; init; } = CardVariant.Default;
    public bool IsInteractive { get; init; } = false;
    public bool IsCollapsible { get; init; } = false;
    public bool IsExpanded { get; init; } = true;
    public string? OnClick { get; init; }
    public string? CssClass { get; init; }
}

public enum CardVariant
{
    Default,    // Standard bordered card
    Flat,       // No border, subtle background
    Elevated    // With shadow
}
