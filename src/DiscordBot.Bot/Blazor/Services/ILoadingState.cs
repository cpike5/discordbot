namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Scoped, reference-counted "is something loading" flag shared by a page and its
/// <c>LoadingOverlay</c>. Multiple concurrent operations can each hold a scope; the overlay stays
/// visible until every scope disposes.
/// </summary>
public interface ILoadingState
{
    /// <summary>Gets whether at least one scope from <see cref="Begin"/> is still open.</summary>
    bool IsLoading { get; }

    /// <summary>
    /// Gets the message for the most recently opened, still-open scope, or <see langword="null"/>
    /// if none was given one or nothing is loading.
    /// </summary>
    string? Message { get; }

    /// <summary>Raised whenever <see cref="IsLoading"/> or <see cref="Message"/> changes.</summary>
    event Action? Changed;

    /// <summary>
    /// Opens a loading scope. Dispose it (typically in a <c>finally</c> or a component's
    /// <c>Dispose</c>) when that operation finishes; disposing is idempotent.
    /// </summary>
    /// <param name="message">Optional status text shown while this scope is open.</param>
    /// <returns>A disposable that closes the scope.</returns>
    IDisposable Begin(string? message = null);
}
