using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// Thin wrapper around <c>wwwroot/js/blazor/audio.js</c>: shared-element preview playback,
/// client-side audio duration probing, drag-and-drop file intake, and progress-reporting
/// upload via <c>XMLHttpRequest</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Call these methods only from <c>OnAfterRenderAsync</c> or an event handler, never
/// during prerendering.</b> The JS module is imported lazily on first use via
/// <see cref="IJSRuntime"/>, which requires a live circuit.
/// </para>
/// <para>
/// Every method that takes a <see cref="DotNetObjectReference{TValue}"/> callback is
/// owned by the caller: create it (typically in <c>OnInitialized</c>), keep it in a field,
/// and dispose it in the component's <see cref="IAsyncDisposable"/>/<see cref="IDisposable"/>
/// implementation — after first calling the matching unregister/stop method on this class so
/// the JS side stops trying to invoke a disposed reference.
/// </para>
/// <para>
/// Register via <c>AddBlazorInterop()</c> (<see cref="Extensions.BlazorInteropServiceExtensions"/>);
/// this class is scoped, one instance per circuit.
/// </para>
/// </remarks>
public sealed class AudioInterop : IAsyncDisposable
{
    private const string ModulePath = "./js/blazor/audio.js";

    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _moduleTask;

    public AudioInterop(IJSRuntime jsRuntime)
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
            // Do not cache a failed import forever: clear the field so the next call to
            // ModuleAsync() retries instead of forever awaiting this same faulted task. Only
            // clear it if it still holds this failed attempt - a concurrent caller may already
            // have started (and cached) a fresh one, which must not be clobbered.
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
    /// Plays <paramref name="url"/> through a single page-wide shared <c>&lt;audio&gt;</c>
    /// element, stopping any preview already in progress. If <paramref name="dotNetRef"/> is
    /// supplied, its <c>[JSInvokable] OnPreviewEnded()</c> method is invoked when playback
    /// ends or errors (not when interrupted by another <see cref="PlayPreviewAsync{TComponent}"/>
    /// or <see cref="StopPreviewAsync"/> call).
    /// </summary>
    public async Task PlayPreviewAsync<TComponent>(string url, DotNetObjectReference<TComponent>? dotNetRef = null)
        where TComponent : class
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("playPreview", url, dotNetRef);
    }

    /// <summary>Stops the current preview, if any, without invoking <c>OnPreviewEnded</c>.</summary>
    public async Task StopPreviewAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("stopPreview");
    }

    /// <summary>
    /// Reads the duration, in seconds, of the file at <paramref name="index"/> in an
    /// <c>&lt;input type="file"&gt;</c> element's file list, via a throwaway
    /// <c>&lt;audio&gt;</c> element and object URL. Throws <see cref="JSException"/> if
    /// there is no file at that index or the browser cannot determine a finite duration.
    /// </summary>
    public async Task<double> GetDurationAsync(ElementReference inputElement, int index)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<double>("getDuration", inputElement, index);
    }

    /// <summary>
    /// Wires dragover/dragleave/drop listeners on <paramref name="element"/>.
    /// <paramref name="dotNetRef"/>'s <c>[JSInvokable] OnDragStateChanged(bool)</c> fires
    /// when the drag-over state changes, and <c>[JSInvokable] OnFilesDropped(int count,
    /// string token)</c> fires on drop; pass <c>token</c> to <see cref="UploadAsync{TComponent}(string,string,string,DotNetObjectReference{TComponent})"/>
    /// to upload the dropped file(s).
    /// </summary>
    /// <returns>A handle to pass to <see cref="UnregisterDropZoneAsync"/>.</returns>
    public async Task<int> RegisterDropZoneAsync<TComponent>(ElementReference element, DotNetObjectReference<TComponent> dotNetRef)
        where TComponent : class
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("registerDropZone", element, dotNetRef);
    }

    /// <summary>Removes the listeners registered by <see cref="RegisterDropZoneAsync{TComponent}"/>.</summary>
    public async Task UnregisterDropZoneAsync(int handle)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("unregisterDropZone", handle);
    }

    /// <summary>
    /// Uploads the file selected in <paramref name="inputElement"/> to <paramref name="url"/>
    /// as <c>multipart/form-data</c> via <c>XMLHttpRequest</c>. Invokes
    /// <c>[JSInvokable] OnUploadProgress(int percent)</c> while sending,
    /// <c>[JSInvokable] OnUploadCompleted(int status, string responseText)</c> when the
    /// server responds (any HTTP status — the caller decides success/failure), and
    /// <c>[JSInvokable] OnUploadFailed(string message)</c> on a network error or an empty
    /// file list.
    /// </summary>
    /// <param name="inputElement">The <c>&lt;input type="file"&gt;</c> holding the file to upload.</param>
    /// <param name="url">The upload endpoint.</param>
    /// <param name="antiforgeryToken">Sent as the <c>RequestVerificationToken</c> header.</param>
    /// <param name="dotNetRef">Owned by the caller; see the class remarks on disposal.</param>
    public async Task UploadAsync<TComponent>(ElementReference inputElement, string url, string antiforgeryToken, DotNetObjectReference<TComponent> dotNetRef)
        where TComponent : class
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("upload", inputElement, url, antiforgeryToken, dotNetRef);
    }

    /// <summary>
    /// Uploads the file(s) captured by a prior drop (see
    /// <see cref="RegisterDropZoneAsync{TComponent}"/>'s <c>OnFilesDropped</c> token) to
    /// <paramref name="url"/>. Same callback contract as
    /// <see cref="UploadAsync{TComponent}(ElementReference,string,string,DotNetObjectReference{TComponent})"/>.
    /// </summary>
    public async Task UploadAsync<TComponent>(string dropToken, string url, string antiforgeryToken, DotNetObjectReference<TComponent> dotNetRef)
        where TComponent : class
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("upload", dropToken, url, antiforgeryToken, dotNetRef);
    }

    /// <summary>
    /// Disposes the imported JS module reference. A no-op if the module was never imported or
    /// the import failed (a faulted <see cref="_moduleTask"/> is never re-awaited here). Safe to
    /// call even if the circuit has already disconnected; <see cref="JSDisconnectedException"/>,
    /// <see cref="OperationCanceledException"/> and <see cref="ObjectDisposedException"/> are
    /// swallowed.
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
