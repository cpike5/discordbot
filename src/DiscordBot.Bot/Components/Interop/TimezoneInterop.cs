using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class TimezoneInterop(IJSRuntime jsRuntime)
{
    public ValueTask<string> GetTimezoneAsync() =>
        jsRuntime.InvokeAsync<string>("timezoneUtils.getTimezone");

    public ValueTask<string> FormatLocalTimeAsync(string utcIso) =>
        jsRuntime.InvokeAsync<string>("timezoneUtils.formatLocalTime", utcIso);
}
