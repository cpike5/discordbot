using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper around <c>wwwroot/js/blazor/charts.js</c> for creating, updating and
/// destroying Chart.js chart instances from Blazor components.
/// </summary>
/// <remarks>
/// <para>
/// <b>Call these methods only from <c>OnAfterRenderAsync</c> or an event handler, never
/// during prerendering.</b> The JS module is imported lazily on first use via
/// <see cref="IJSRuntime"/>, which requires a live browser connection (a circuit, for the
/// Interactive Server render mode this app uses); there is no such connection while a
/// component is prerendering on the server.
/// </para>
/// <para>
/// Chart.js itself is loaded lazily by <c>charts.js</c>, not by this wrapper: the first
/// call to <see cref="CreateAsync"/> injects the vendored
/// <c>/lib/chart.js/chart.umd.js</c> script tag (see <c>package.json</c>'s
/// <c>build:vendor</c> script) if <c>window.Chart</c> isn't already defined.
/// </para>
/// <para>
/// Register via <c>AddBlazorInterop()</c> (<see cref="Extensions.BlazorInteropServiceExtensions"/>);
/// this class is scoped, one instance per circuit.
/// </para>
/// </remarks>
public sealed class ChartInterop : IAsyncDisposable
{
    private const string ModulePath = "./js/blazor/charts.js";

    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _moduleTask;

    public ChartInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private Task<IJSObjectReference> ModuleAsync()
        => _moduleTask ??= ImportModuleAsync();

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);

    /// <summary>
    /// Creates a Chart.js chart on <paramref name="canvas"/> using <paramref name="config"/>
    /// (a plain object serialized to the Chart.js <c>{ type, data, options }</c> shape; the
    /// Graphite theme defaults are merged in on the JS side). Any Discord snowflakes inside
    /// <paramref name="config"/> — e.g. a dataset label keyed by guild ID — must already be
    /// strings.
    /// </summary>
    /// <returns>A numeric handle for <see cref="UpdateAsync"/> and <see cref="DestroyAsync"/>.</returns>
    public async Task<int> CreateAsync(ElementReference canvas, object config)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("create", canvas, config);
    }

    /// <summary>
    /// Updates an existing chart's data and/or options in place and redraws it. Either
    /// argument may be <see langword="null"/> to leave that part of the chart unchanged.
    /// </summary>
    public async Task UpdateAsync(int handle, object? data = null, object? options = null)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("update", handle, data, options);
    }

    /// <summary>Destroys a single chart instance created by <see cref="CreateAsync"/>.</summary>
    public async Task DestroyAsync(int handle)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("destroy", handle);
    }

    /// <summary>
    /// Destroys every chart instance the JS module is currently tracking. Intended as a
    /// last-resort cleanup, e.g. from a layout's dispose path when individual handles were
    /// not tracked.
    /// </summary>
    public async Task DestroyAllAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("destroyAll");
    }

    /// <summary>
    /// Disposes the imported JS module reference. Safe to call even if the circuit has
    /// already disconnected; <see cref="JSDisconnectedException"/> is swallowed since there
    /// is nothing left to clean up client-side in that case.
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
