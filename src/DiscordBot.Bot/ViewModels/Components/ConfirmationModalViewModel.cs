// src/DiscordBot.Bot/ViewModels/Components/ConfirmationModalViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for confirmation modal dialogs.
/// </summary>
public record ConfirmationModalViewModel
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
    public ConfirmationVariant Variant { get; init; } = ConfirmationVariant.Warning;

    /// <summary>
    /// Gets the form action URL for POST submissions.
    /// </summary>
    public string? FormAction { get; init; }

    /// <summary>
    /// Gets the Razor Page handler name for the form.
    /// </summary>
    public string? FormHandler { get; init; }

    /// <summary>
    /// Gets the SVG path for the icon (if different from default variant icon).
    /// </summary>
    public string? CustomIconPath { get; init; }
}

/// <summary>
/// Visual variants for confirmation modals.
/// </summary>
public enum ConfirmationVariant
{
    /// <summary>
    /// Informational dialog (blue accent).
    /// </summary>
    Info,

    /// <summary>
    /// Warning dialog (amber/warning color).
    /// </summary>
    Warning,

    /// <summary>
    /// Dangerous/destructive action (red/error color).
    /// </summary>
    Danger
}
