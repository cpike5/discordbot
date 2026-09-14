using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper around <c>wwwroot/js/blazor/theme.js</c>: applies a theme's <c>data-theme</c>
/// attribute, cookie and localStorage entry client-side, mirroring <c>wwwroot/js/theme.js</c>'s
/// <c>ThemeManager</c> contract (same cookie name, same localStorage key, same 1-year cookie
/// lifetime, same <c>themechange</c> browser <c>CustomEvent</c>) so the legacy
/// and Blazor shells agree on theme state while they coexist. See "Deferred from Phase 1" in
/// <c>docs/plans/blazor-port-plan.md</c> §5 Phase 1 and "Theme" in
/// <c>docs/articles/blazor-interop.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Call these methods only from <c>OnAfterRenderAsync</c> or an event handler, never during
/// prerendering</b> - same rule as <see cref="BrowserInterop"/>/<see cref="ChartInterop"/>/
/// <see cref="AudioInterop"/> (see "The prerender rule" in <c>docs/articles/blazor-interop.md</c>).
/// The JS module is imported lazily on first use, which requires a live circuit.
/// </para>
/// <para>
/// This does <b>not</b> call any REST endpoint - it only ever touches the current browser's
/// cookie/localStorage/DOM. Persisting a user's choice server-side (so it survives to their next
/// session/device) is a separate call to <see cref="DiscordBot.Core.Interfaces.IThemeService.SetUserThemeAsync"/>
/// the caller makes itself, same as <c>Blazor/Pages/Admin/BlazorProbe.razor</c>'s theme-switcher
/// demo does. Call both together for a real "change my theme" flow: <see cref="ApplyAsync"/> for
/// the immediate client-side effect, <c>SetUserThemeAsync</c> for the durable preference.
/// </para>
/// <para>
/// Register via <c>AddBlazorInterop()</c> (<see cref="Extensions.BlazorInteropServiceExtensions"/>);
/// this class is scoped, one instance per circuit.
/// </para>
/// </remarks>
public sealed class ThemeInterop : IAsyncDisposable
{
    private const string ModulePath = "./js/blazor/theme.js";

    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _moduleTask;

    public ThemeInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        var task = _moduleTask ??= ImportModuleAsync();
        try
        {
            return await task;
        }
        catch
        {
            // Do not cache a failed import forever: clear the field so the next call retries
            // instead of forever awaiting this same faulted task. Only clear it if it still holds
            // this failed attempt - a concurrent caller may already have started (and cached) a
            // fresh one, which must not be clobbered.
            if (ReferenceEquals(_moduleTask, task))
            {
                _moduleTask = null;
            }

            throw;
        }
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);

    /// <summary>
    /// Applies <paramref name="themeKey"/> immediately: sets <c>data-theme</c> on
    /// <c>&lt;html&gt;</c>, writes the <c>theme-preference</c> cookie (1-year max-age, path
    /// <c>/</c>, <c>SameSite=Lax</c>) and localStorage entry, and dispatches a browser
    /// <c>themechange</c> event. Does not persist server-side - see the remarks on this class.
    /// </summary>
    public async Task ApplyAsync(string themeKey)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("apply", themeKey);
    }

    /// <summary>
    /// Clears the theme preference (cookie, localStorage and the <c>data-theme</c> attribute),
    /// reverting to the system default the next time a page resolves a theme server-side.
    /// </summary>
    public async Task ClearAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clear");
    }

    /// <summary>
    /// Returns the theme key currently applied client-side (cookie if set, else localStorage,
    /// else <see langword="null"/>) - not necessarily the same value <c>IThemeService</c> would
    /// resolve server-side for an authenticated user with a saved preference.
    /// </summary>
    public async Task<string?> GetCurrentAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<string?>("getCurrent");
    }

    /// <summary>
    /// Disposes the imported JS module reference. A no-op if the module was never imported or the
    /// import failed. Safe to call even if the circuit has already disconnected;
    /// <see cref="JSDisconnectedException"/>, <see cref="OperationCanceledException"/> and
    /// <see cref="ObjectDisposedException"/> are swallowed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_moduleTask is not { IsCompletedSuccessfully: true })
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
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
