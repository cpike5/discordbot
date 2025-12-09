// src/DiscordBot.Bot/ViewModels/Components/ButtonViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record ButtonViewModel
{
    public string Text { get; init; } = string.Empty;
    public ButtonVariant Variant { get; init; } = ButtonVariant.Primary;
    public ButtonSize Size { get; init; } = ButtonSize.Medium;
    public string? Type { get; init; } = "button"; // button, submit, reset
    public string? IconLeft { get; init; }  // SVG path or icon name
    public string? IconRight { get; init; }
    public bool IsDisabled { get; init; } = false;
    public bool IsLoading { get; init; } = false;
    public bool IsIconOnly { get; init; } = false;
    public string? AriaLabel { get; init; }
    public string? OnClick { get; init; } // JavaScript handler
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public enum ButtonVariant
{
    Primary,    // Orange accent - main CTAs
    Secondary,  // Outline - cancel/secondary actions
    Accent,     // Blue - informational actions
    Danger,     // Red - destructive actions
    Ghost       // Transparent - subtle actions
}

public enum ButtonSize
{
    Small,      // py-1.5 px-3 text-xs
    Medium,     // py-2.5 px-5 text-sm (default)
    Large       // py-3 px-6 text-base
}
