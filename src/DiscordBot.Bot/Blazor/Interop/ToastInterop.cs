using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper over the existing <c>toast.js</c> notification system so Blazor
/// components raise the same toasts as the rest of the portal. Calls the
/// window-attached shim in <c>blazor-interop.js</c> (which forwards to
/// <c>ToastManager.show</c>).
/// </summary>
/// <remarks>
/// Registered as a scoped service. Interop must only be invoked after the first
/// render (the JS runtime is not available during prerendering), so callers
/// should invoke these from event handlers or <c>OnAfterRenderAsync</c>.
/// </remarks>
public sealed class ToastInterop
{
    private readonly IJSRuntime _js;

    public ToastInterop(IJSRuntime js) => _js = js;

    /// <summary>Shows a success toast (auto-dismiss).</summary>
    public ValueTask SuccessAsync(string message, string? title = null)
        => ShowAsync("success", message, title);

    /// <summary>Shows an error toast (persists until dismissed).</summary>
    public ValueTask ErrorAsync(string message, string? title = null)
        => ShowAsync("error", message, title);

    /// <summary>Shows a warning toast.</summary>
    public ValueTask WarningAsync(string message, string? title = null)
        => ShowAsync("warning", message, title);

    /// <summary>Shows an informational toast.</summary>
    public ValueTask InfoAsync(string message, string? title = null)
        => ShowAsync("info", message, title);

    /// <summary>
    /// Shows a toast of the given <paramref name="type"/>
    /// (<c>success</c>, <c>error</c>, <c>warning</c>, or <c>info</c>).
    /// </summary>
    public ValueTask ShowAsync(string type, string message, string? title = null)
        => _js.InvokeVoidAsync("blazorInterop.toast", type, message, title);
}
