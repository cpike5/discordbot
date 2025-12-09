// src/DiscordBot.Bot/ViewModels/Components/FormSelectViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record FormSelectViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Placeholder { get; init; } = "Select an option";
    public string? SelectedValue { get; init; }
    public List<SelectOption> Options { get; init; } = new();
    public List<SelectOptionGroup>? OptionGroups { get; init; }
    public string? HelpText { get; init; }
    public InputSize Size { get; init; } = InputSize.Medium;
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool AllowMultiple { get; init; } = false;
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public record SelectOption
{
    public string Value { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsDisabled { get; init; } = false;
}

public record SelectOptionGroup
{
    public string Label { get; init; } = string.Empty;
    public List<SelectOption> Options { get; init; } = new();
}
