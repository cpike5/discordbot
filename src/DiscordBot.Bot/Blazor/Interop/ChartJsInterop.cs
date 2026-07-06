using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin bridge to Chart.js via wwwroot/js/blazor-chart-interop.js. Charts stay
/// in Chart.js permanently (completion plan §2 decision 5); Blazor components
/// own the canvas element and data, and call this from OnAfterRenderAsync —
/// never during prerender, when no JS runtime exists.
/// </summary>
public class ChartJsInterop
{
    private readonly IJSRuntime _js;

    public ChartJsInterop(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Creates (or replaces) the chart bound to a canvas element id.</summary>
    /// <param name="canvasId">The id of the canvas element.</param>
    /// <param name="config">Chart.js configuration object (serialized to JSON).</param>
    public ValueTask CreateAsync(string canvasId, object config)
        => _js.InvokeVoidAsync("blazorChartInterop.create", canvasId, config);

    /// <summary>Replaces the chart's data (labels + datasets) and re-renders in place.</summary>
    public ValueTask UpdateAsync(string canvasId, object data)
        => _js.InvokeVoidAsync("blazorChartInterop.update", canvasId, data);

    /// <summary>Destroys the chart instance for a canvas, releasing its resources.</summary>
    public async ValueTask DestroyAsync(string canvasId)
    {
        try
        {
            await _js.InvokeVoidAsync("blazorChartInterop.destroy", canvasId);
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down — the browser is gone along with the chart.
        }
    }
}
