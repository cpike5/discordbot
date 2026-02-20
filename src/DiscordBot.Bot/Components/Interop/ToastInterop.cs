using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class ToastInterop(IJSRuntime jsRuntime)
{
    public ValueTask ShowAsync(string type, string message) =>
        jsRuntime.InvokeVoidAsync("ToastManager.show", type, message);

    public ValueTask ShowAsync(string type, string message, string title) =>
        jsRuntime.InvokeVoidAsync("ToastManager.show", type, message, new { title });

    public ValueTask ClearAllAsync() =>
        jsRuntime.InvokeVoidAsync("ToastManager.clearAll");
}
