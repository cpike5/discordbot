using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper over the existing <c>theme.js</c> manager so Blazor components
/// apply themes through the same code path (and persistence) as the rest of the
/// portal. Calls the window-attached shim in <c>blazor-interop.js</c>.
/// </summary>
/// <remarks>Registered as a scoped service. Invoke only after first render.</remarks>
public sealed class ThemeInterop
{
    private readonly IJSRuntime _js;

    public ThemeInterop(IJSRuntime js) => _js = js;

    /// <summary>
    /// Applies the given theme key, optionally persisting the preference to the
    /// server (mirrors <c>ThemeManager.applyTheme(key, persist)</c>).
    /// </summary>
    public ValueTask ApplyThemeAsync(string themeKey, bool persistToServer = false)
        => _js.InvokeVoidAsync("blazorInterop.applyTheme", themeKey, persistToServer);
}
