namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for typed confirmation modal dialogs.
/// User must type a specific phrase to enable the confirm button.
/// </summary>
public record TypedConfirmationModalViewModel
{
    /// <summary>
    /// Gets the unique identifier for the modal.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the title of the confirmation dialog.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message explaining the action and consequences.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exact text the user must type to confirm.
    /// </summary>
    public string RequiredText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the label for the input field.
    /// </summary>
    public string InputLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text for the confirm button.
    /// </summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>
    /// Gets the text for the cancel button.
    /// </summary>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>
    /// Gets the visual variant of the confirmation dialog.
    /// </summary>
    public ConfirmationVariant Variant { get; init; } = ConfirmationVariant.Danger;

    /// <summary>
    /// Gets the form action URL for POST submissions.
    /// </summary>
    public string? FormAction { get; init; }

    /// <summary>
    /// Gets the Razor Page handler name for the form.
    /// </summary>
    public string? FormHandler { get; init; }
}
