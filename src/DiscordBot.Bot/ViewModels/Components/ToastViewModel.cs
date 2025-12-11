// src/DiscordBot.Bot/ViewModels/Components/ToastViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for toast notification messages.
/// </summary>
public record ToastViewModel
{
    /// <summary>
    /// Gets the unique identifier for the toast.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the message text to display.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the visual variant of the toast.
    /// </summary>
    public ToastVariant Variant { get; init; } = ToastVariant.Info;

    /// <summary>
    /// Gets the duration in milliseconds before the toast auto-dismisses.
    /// </summary>
    public int DurationMs { get; init; } = 3000;

    /// <summary>
    /// Gets a value indicating whether the toast can be manually dismissed.
    /// </summary>
    public bool IsDismissible { get; init; } = true;
}

/// <summary>
/// Visual variants for toast notifications.
/// </summary>
public enum ToastVariant
{
    /// <summary>
    /// Success toast (green background).
    /// </summary>
    Success,

    /// <summary>
    /// Error toast (red background).
    /// </summary>
    Error,

    /// <summary>
    /// Warning toast (amber background).
    /// </summary>
    Warning,

    /// <summary>
    /// Informational toast (blue background).
    /// </summary>
    Info
}
