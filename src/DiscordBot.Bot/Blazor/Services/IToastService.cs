namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Severity of a toast, mirroring the four variants <c>wwwroot/js/toast.js</c> renders
/// (<c>toast-success</c>, <c>toast-info</c>, <c>toast-warning</c>, <c>toast-error</c>).
/// </summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// One queued toast. Immutable snapshot handed to a <c>ToastHost</c> component.
/// </summary>
/// <param name="Id">Identifies this toast for <see cref="IToastService.Dismiss"/>.</param>
/// <param name="Level">Severity, driving icon and color.</param>
/// <param name="Message">Body text.</param>
/// <param name="Title">Optional bold heading shown above <paramref name="Message"/>.</param>
/// <param name="CreatedAt">When the toast was shown.</param>
/// <param name="Duration">
/// Auto-dismiss delay, or <see langword="null"/> for a toast that stays until dismissed
/// (the default for <see cref="ToastLevel.Error"/>, matching the JS toast manager).
/// </param>
public sealed record ToastMessage(
    Guid Id,
    ToastLevel Level,
    string Message,
    string? Title,
    DateTimeOffset CreatedAt,
    TimeSpan? Duration);

/// <summary>
/// Scoped Blazor toast queue, the server-side equivalent of <c>wwwroot/js/toast.js</c>'s
/// <c>ToastManager</c>. A <c>ToastHost</c> component subscribes to <see cref="Changed"/>, renders
/// <see cref="Toasts"/>, and calls <see cref="Dismiss"/> when the user closes one or a duration
/// elapses.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Gets the toasts currently queued, oldest first. At most 5 are kept; showing a 6th evicts
    /// the oldest, mirroring the JS toast manager's <c>maxToasts</c> limit.
    /// </summary>
    IReadOnlyList<ToastMessage> Toasts { get; }

    /// <summary>
    /// Raised whenever <see cref="Toasts"/> changes (shown, dismissed, or auto-expired). May fire
    /// off the Blazor renderer's thread (from an auto-dismiss timer); a subscribing component must
    /// marshal back with <c>InvokeAsync(StateHasChanged)</c>.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Queues a toast.
    /// </summary>
    /// <param name="level">Severity.</param>
    /// <param name="message">Body text.</param>
    /// <param name="title">Optional heading.</param>
    /// <param name="duration">
    /// Auto-dismiss delay. Omit for the level's default (3s success, 5s info/warning, no
    /// auto-dismiss for error); pass <see cref="TimeSpan.Zero"/> or a negative value for no
    /// auto-dismiss regardless of level.
    /// </param>
    void Show(ToastLevel level, string message, string? title = null, TimeSpan? duration = null);

    /// <summary>Queues a <see cref="ToastLevel.Success"/> toast.</summary>
    void Success(string message, string? title = null, TimeSpan? duration = null);

    /// <summary>Queues an <see cref="ToastLevel.Info"/> toast.</summary>
    void Info(string message, string? title = null, TimeSpan? duration = null);

    /// <summary>Queues a <see cref="ToastLevel.Warning"/> toast.</summary>
    void Warning(string message, string? title = null, TimeSpan? duration = null);

    /// <summary>Queues an <see cref="ToastLevel.Error"/> toast.</summary>
    void Error(string message, string? title = null, TimeSpan? duration = null);

    /// <summary>Removes one toast, e.g. from its close button or an elapsed auto-dismiss timer.</summary>
    void Dismiss(Guid id);
}
