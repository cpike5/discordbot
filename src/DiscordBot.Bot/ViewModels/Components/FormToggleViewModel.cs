namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for a toggle switch form control (<c>_FormToggle</c>, over the <c>.toggle</c> classes).
/// </summary>
public record FormToggleViewModel
{
    /// <summary>
    /// Gets the unique identifier for the toggle input.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name attribute for form submission.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the label text displayed next to the toggle.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets the description/help text displayed below the label.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether the toggle is currently in the checked/on state.
    /// </summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// Gets whether the toggle is disabled (not interactive).
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets whether an unchecked toggle posts <c>false</c>. An unchecked checkbox posts nothing,
    /// so a form cannot tell "turned off" from "not on the form" and a bound <c>bool</c> never
    /// becomes false. When true (the default) a hidden <c>false</c> input follows the checkbox with
    /// the same name; a checked toggle posts <c>true,false</c> and the model binder reads the first.
    /// Not rendered while the toggle is disabled, because a disabled field posts nothing.
    /// Set it to false for scripts that read <c>checked</c> themselves.
    /// </summary>
    public bool PostsFalseWhenOff { get; init; } = true;

    /// <summary>
    /// Gets extra attributes for the checkbox (for example <c>data-setting-toggle</c>).
    /// Values are HTML-encoded.
    /// </summary>
    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}
