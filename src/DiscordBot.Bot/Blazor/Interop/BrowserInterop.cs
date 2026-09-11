using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper around <c>wwwroot/js/blazor/browser.js</c>: localStorage, clipboard,
/// focus/scroll, a modal focus trap, a <c>beforeunload</c> dirty guard, <c>matchMedia</c>
/// watching, IANA timezone detection, click-outside detection, and textarea selection
/// manipulation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Call these methods only from <c>OnAfterRenderAsync</c> or an event handler, never
/// during prerendering.</b> The JS module is imported lazily on first use via
/// <see cref="IJSRuntime"/>, which requires a live circuit.
/// </para>
/// <para>
/// <see cref="TrapFocusAsync"/>, <see cref="MatchMediaAsync{TComponent}"/> and
/// <see cref="OnClickOutsideAsync{TComponent}"/> each register a JS-side listener and
/// return a handle; call the matching release method
/// (<see cref="ReleaseFocusAsync"/>/<see cref="UnwatchMediaAsync"/>/<see cref="OffClickOutsideAsync"/>)
/// from the component's dispose path or the listener leaks for the life of the circuit.
/// Any <see cref="DotNetObjectReference{TValue}"/> passed in is owned by the caller: dispose
/// it after releasing the corresponding JS listener.
/// </para>
/// <para>
/// Register via <c>AddBlazorInterop()</c> (<see cref="Extensions.BlazorInteropServiceExtensions"/>);
/// this class is scoped, one instance per circuit.
/// </para>
/// </remarks>
public sealed class BrowserInterop : IAsyncDisposable
{
    private const string ModulePath = "./js/blazor/browser.js";

    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _moduleTask;

    public BrowserInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private Task<IJSObjectReference> ModuleAsync()
        => _moduleTask ??= ImportModuleAsync();

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);

    /// <summary>Reads a localStorage value, or <see langword="null"/> if absent or storage is unavailable.</summary>
    public async Task<string?> StorageGetAsync(string key)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<string?>("storageGet", key);
    }

    /// <summary>Writes a localStorage value. Returns <see langword="false"/> instead of throwing if storage is unavailable.</summary>
    public async Task<bool> StorageSetAsync(string key, string value)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<bool>("storageSet", key, value);
    }

    /// <summary>Removes a localStorage value. Returns <see langword="false"/> instead of throwing if storage is unavailable.</summary>
    public async Task<bool> StorageRemoveAsync(string key)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<bool>("storageRemove", key);
    }

    /// <summary>Writes <paramref name="text"/> to the clipboard. Returns whether it succeeded.</summary>
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<bool>("copyToClipboard", text);
    }

    /// <summary>Focuses an element.</summary>
    public async Task FocusElementAsync(ElementReference element)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("focusElement", element);
    }

    /// <summary>Scrolls an element into view.</summary>
    /// <param name="element">The element to scroll into view.</param>
    /// <param name="behavior">A CSS <c>scroll-behavior</c> value; defaults to <c>"smooth"</c>.</param>
    public async Task ScrollIntoViewAsync(ElementReference element, string? behavior = null)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("scrollIntoView", element, behavior);
    }

    /// <summary>
    /// Traps Tab/Shift+Tab focus cycling within <paramref name="element"/> (for a modal) and
    /// focuses its first focusable descendant.
    /// </summary>
    /// <returns>A handle to pass to <see cref="ReleaseFocusAsync"/>.</returns>
    public async Task<int> TrapFocusAsync(ElementReference element)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("trapFocus", element);
    }

    /// <summary>Releases a focus trap registered by <see cref="TrapFocusAsync"/>.</summary>
    public async Task ReleaseFocusAsync(int handle)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("releaseFocus", handle);
    }

    /// <summary>
    /// Enables or disables the page-wide <c>beforeunload</c> "unsaved changes" prompt. There
    /// is one guard per page (mirrors the single-form-at-a-time assumption of the legacy
    /// <c>settings.js</c>/<c>moderation-settings.js</c> guards); set it <see langword="false"/>
    /// when a dirty form is saved, navigated away from, or the owning component disposes.
    /// </summary>
    public async Task SetBeforeUnloadGuardAsync(bool enabled)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setBeforeUnloadGuard", enabled);
    }

    /// <summary>
    /// Watches a <c>matchMedia</c> query. <paramref name="dotNetRef"/>'s
    /// <c>[JSInvokable] OnMediaChanged(bool matches)</c> fires on every change.
    /// </summary>
    /// <returns>The watch handle and whether the query matches right now.</returns>
    public async Task<MediaWatchResult> MatchMediaAsync<TComponent>(string query, DotNetObjectReference<TComponent> dotNetRef)
        where TComponent : class
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<MediaWatchResult>("matchMedia", query, dotNetRef);
    }

    /// <summary>Stops watching a query registered by <see cref="MatchMediaAsync{TComponent}"/>.</summary>
    public async Task UnwatchMediaAsync(int handle)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("unwatchMedia", handle);
    }

    /// <summary>Returns the browser's IANA timezone identifier (e.g. <c>"America/New_York"</c>), or <c>"UTC"</c> on failure.</summary>
    public async Task<string> GetTimeZoneAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<string>("getTimeZone");
    }

    /// <summary>
    /// Invokes <paramref name="dotNetRef"/>'s <c>[JSInvokable] OnClickOutside()</c> when a
    /// click lands outside <paramref name="element"/> (for closing a dropdown/menu).
    /// </summary>
    /// <returns>A handle to pass to <see cref="OffClickOutsideAsync"/>.</returns>
    public async Task<int> OnClickOutsideAsync<TComponent>(ElementReference element, DotNetObjectReference<TComponent> dotNetRef)
        where TComponent : class
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("onClickOutside", element, dotNetRef);
    }

    /// <summary>Removes a click-outside listener registered by <see cref="OnClickOutsideAsync{TComponent}"/>.</summary>
    public async Task OffClickOutsideAsync(int handle)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("offClickOutside", handle);
    }

    /// <summary>Reads a textarea's current selection.</summary>
    public async Task<TextSelectionResult> GetSelectionAsync(ElementReference textarea)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<TextSelectionResult>("getSelection", textarea);
    }

    /// <summary>Sets a textarea's selection range and focuses it.</summary>
    public async Task SetSelectionAsync(ElementReference textarea, int start, int end)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setSelection", textarea, start, end);
    }

    /// <summary>
    /// Replaces a textarea's current selection with <paramref name="text"/>, moves the caret
    /// to the end of the inserted text, and dispatches a bubbling <c>input</c> event so
    /// Blazor two-way bindings on the textarea observe the change.
    /// </summary>
    public async Task InsertAtSelectionAsync(ElementReference textarea, string text)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("insertAtSelection", textarea, text);
    }

    /// <summary>
    /// Disposes the imported JS module reference. Safe to call even if the circuit has
    /// already disconnected; <see cref="JSDisconnectedException"/> is swallowed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_moduleTask is null)
        {
            return;
        }

        try
        {
            var module = await _moduleTask;
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
