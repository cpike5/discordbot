using Microsoft.JSInterop;

namespace DiscordBot.Bot.Components.Interop;

public class ChartJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly HashSet<string> _chartIds = [];

    public async ValueTask CreateChartAsync(string canvasId, object config)
    {
        await jsRuntime.InvokeVoidAsync("BlazorChartInterop.createChart", canvasId, config);
        _chartIds.Add(canvasId);
    }

    public ValueTask UpdateChartAsync(string canvasId, object data) =>
        jsRuntime.InvokeVoidAsync("BlazorChartInterop.updateChart", canvasId, data);

    public async ValueTask DestroyChartAsync(string canvasId)
    {
        await jsRuntime.InvokeVoidAsync("BlazorChartInterop.destroyChart", canvasId);
        _chartIds.Remove(canvasId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var canvasId in _chartIds)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("BlazorChartInterop.destroyChart", canvasId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, chart is already gone
            }
        }

        _chartIds.Clear();
    }
}
