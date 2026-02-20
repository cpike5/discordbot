using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class ClipboardInterop(IJSRuntime jsRuntime)
{
    public ValueTask CopyTextAsync(string text) =>
        jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
}
