namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_RadioCardGroup</c>: a fieldset of radio cards sharing one name.
/// </summary>
public record RadioCardGroupViewModel
{
    /// <summary>The shared <c>name</c> of the radios.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The group's label (the fieldset legend). Required for an accessible group.</summary>
    public string Legend { get; init; } = string.Empty;

    public string? HelpText { get; init; }

    /// <summary>The cards. <c>Name</c> and <c>IsChecked</c> on each are set by the group.</summary>
    public List<RadioCardViewModel> Options { get; init; } = new();

    /// <summary>The <c>Value</c> of the selected card, or null for none.</summary>
    public string? SelectedValue { get; init; }

    /// <summary>Columns from the <c>sm</c> breakpoint up (1-4). One column on phones.</summary>
    public int Columns { get; init; } = 2;

    public bool IsRequired { get; init; }

    /// <summary>An error for the group ("Choose how to purge"). Turns the cards red and is announced.</summary>
    public string? ValidationMessage { get; init; }

    /// <summary>Prefix for the legend, help and error IDs. Defaults to the name.</summary>
    public string? Id { get; init; }

    public string ResolvedId => !string.IsNullOrEmpty(Id) ? Id : Name;
}
