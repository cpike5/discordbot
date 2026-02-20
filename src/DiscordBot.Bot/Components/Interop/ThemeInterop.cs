using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class ThemeInterop(IJSRuntime jsRuntime)
{
    public ValueTask ApplyThemeAsync(string theme) =>
        jsRuntime.InvokeVoidAsync("ThemeManager.applyTheme", theme);

    public ValueTask<string> GetCurrentThemeAsync() =>
        jsRuntime.InvokeAsync<string>("ThemeManager.getCurrentTheme");
}
