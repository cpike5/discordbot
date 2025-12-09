// src/DiscordBot.Bot/ViewModels/Components/FormInputViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record FormInputViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string Type { get; init; } = "text"; // text, email, password, search, url, tel
    public string? Placeholder { get; init; }
    public string? Value { get; init; }
    public string? HelpText { get; init; }
    public InputSize Size { get; init; } = InputSize.Medium;
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool IsReadOnly { get; init; } = false;
    public string? IconLeft { get; init; }   // SVG icon path
    public string? IconRight { get; init; }
    public int? MaxLength { get; init; }
    public bool ShowCharacterCount { get; init; } = false;
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public enum InputSize
{
    Small,      // py-1.5 px-3 text-xs
    Medium,     // py-2.5 px-3.5 text-sm (default)
    Large       // py-3 px-4 text-base
}

public enum ValidationState
{
    None,
    Success,
    Warning,
    Error
}
