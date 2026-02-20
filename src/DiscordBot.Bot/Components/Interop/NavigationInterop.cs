using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class NavigationInterop(IJSRuntime jsRuntime)
{
    public ValueTask ScrollToTopAsync() =>
        jsRuntime.InvokeVoidAsync("window.scrollTo", 0, 0);

    public ValueTask ScrollToElementAsync(string elementId) =>
        jsRuntime.InvokeVoidAsync("BlazorNavigationInterop.scrollToElement", elementId);
}
