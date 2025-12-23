namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for a toggle switch form control.
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
}
