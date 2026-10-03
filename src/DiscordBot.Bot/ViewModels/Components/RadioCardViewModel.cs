namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for one <c>_RadioCard</c>: a radio the user picks by clicking a card. The input is
/// visually hidden but stays focusable, and the card shows focus, hover, selected and disabled.
/// </summary>
public record RadioCardViewModel
{
    /// <summary>The radio's <c>name</c>; every card in a group shares it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The value posted when this card is selected.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>The card heading, also the radio's accessible name.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Supporting text; announced as the radio's description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional SVG path (24x24 outline icon) drawn before the text.</summary>
    public string? IconPath { get; init; }

    public bool IsChecked { get; init; }
    public bool IsDisabled { get; init; }

    /// <summary>Optional element ID; defaults to <c>{Name}-{Value}</c> made safe for an ID.</summary>
    public string? Id { get; init; }

    /// <summary>Extra attributes for the radio input (for example <c>data-*</c>). Values are encoded.</summary>
    public Dictionary<string, string>? AdditionalAttributes { get; init; }

    /// <summary>The element ID the radio gets.</summary>
    public string ResolvedId => !string.IsNullOrEmpty(Id)
        ? Id
        : System.Text.RegularExpressions.Regex.Replace($"{Name}-{Value}", "[^A-Za-z0-9_-]", "_");
}
