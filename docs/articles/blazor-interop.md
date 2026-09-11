# Blazor JavaScript Interop

Phase 1 of the Blazor port (`docs/plans/blazor-port-plan.md` §4.4, §5) keeps three
purpose-built JavaScript modules for browser APIs Blazor doesn't cover on its own, each
wrapped by a thin, scoped C# service. This article documents both sides plus the vendoring
of Chart.js.

## Modules at a glance

| Module | JS file | C# wrapper | Replaces |
| --- | --- | --- | --- |
| Charts | `wwwroot/js/blazor/charts.js` | `ChartInterop` | 14 per-page Chart.js CDN tags and `performance/components/chart-utils.js` |
| Audio | `wwwroot/js/blazor/audio.js` | `AudioInterop` | Preview/duration/upload slivers of `portal-soundboard.js`, `portal-tts.js` |
| Browser | `wwwroot/js/blazor/browser.js` | `BrowserInterop` | Scattered helpers in `navigation.js`, `quick-actions.js`, `settings.js`, `moderation-settings.js`, `portal-vox.js`, `timezone.js`, `_EmphasisToolbar.cshtml` |

None of the legacy Razor Pages or their JavaScript were touched — those pages keep loading
Chart.js from the CDN and using their own inline scripts until they're individually migrated
in Phase 4. The three modules here are additive, used only by Blazor components.

## The prerender rule

**Call every method on `ChartInterop`, `AudioInterop` and `BrowserInterop` only from
`OnAfterRenderAsync` or an event handler — never from `OnInitialized`, `OnParametersSet`, or
a field initializer.** Each wrapper lazily imports its JS module on first call via
`IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/blazor/<name>.js")`, which
requires a live circuit. During server-side prerendering there is no browser connection yet,
so any interop call throws. Guard with `firstRender`:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _chartHandle = await ChartInterop.CreateAsync(_canvasRef, BuildConfig());
    }
}
```

## Registration

`Extensions/BlazorInteropServiceExtensions.cs` exposes:

```csharp
services.AddBlazorInterop();
```

which registers `ChartInterop`, `AudioInterop` and `BrowserInterop` as **scoped** — one
instance per circuit, matching the lifetime of the `IJSRuntime` they wrap. This method is
not called from `Program.cs` by this PR; the agent wiring Blazor hosting calls it during
integration.

## Disposal

Every wrapper implements `IAsyncDisposable`. Disposing it disposes the imported
`IJSObjectReference`, swallowing `JSDisconnectedException` (the circuit is already gone by
the time cleanup runs, so there's nothing left to tell the client). If a component injects
a wrapper directly from DI (rather than owning an instance itself), it does not need to
dispose it — the scoped container does that when the circuit ends.

Methods that register a JS-side listener return a numeric handle and must be paired with a
release call, or the listener leaks for the life of the circuit:

| Register | Release |
| --- | --- |
| `ChartInterop.CreateAsync` | `ChartInterop.DestroyAsync` (or `DestroyAllAsync` as a last resort) |
| `AudioInterop.RegisterDropZoneAsync` | `AudioInterop.UnregisterDropZoneAsync` |
| `BrowserInterop.TrapFocusAsync` | `BrowserInterop.ReleaseFocusAsync` |
| `BrowserInterop.MatchMediaAsync` | `BrowserInterop.UnwatchMediaAsync` |
| `BrowserInterop.OnClickOutsideAsync` | `BrowserInterop.OffClickOutsideAsync` |

Any `DotNetObjectReference<T>` passed into one of these calls (`AudioInterop.PlayPreviewAsync`,
`RegisterDropZoneAsync`, `UploadAsync`; `BrowserInterop.MatchMediaAsync`,
`OnClickOutsideAsync`) is **owned by the caller**: create it (typically in `OnInitialized`),
keep it in a field, call the matching release/stop method first, then dispose the reference
in the component's own dispose path.

```csharp
public sealed partial class MyComponent : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<MyComponent>? _selfRef;
    private int _clickOutsideHandle;

    protected override void OnInitialized() => _selfRef = DotNetObjectReference.Create(this);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _clickOutsideHandle = await BrowserInterop.OnClickOutsideAsync(_menuRef, _selfRef!);
        }
    }

    [JSInvokable]
    public void OnClickOutside() { /* close the menu */ StateHasChanged(); }

    public async ValueTask DisposeAsync()
    {
        await BrowserInterop.OffClickOutsideAsync(_clickOutsideHandle);
        _selfRef?.Dispose();
    }
}
```

## Discord snowflakes cross interop as strings

Same rule as everywhere else in this codebase (see the CLAUDE.md gotcha on Discord IDs in
JavaScript): Discord snowflakes are `ulong`, larger than `Number.MAX_SAFE_INTEGER`. Any
guild/user/channel/message ID that reaches a JS interop call — inside a chart `config`
object's labels or dataset keys, an upload URL, a localStorage key — must already be a
`string` by the time it crosses the `IJSRuntime` boundary. Convert in C# before calling, not
in JS.

## ChartInterop / charts.js

Chart.js 4.4.1 is a vendored **runtime** npm dependency (`chart.js` in
`src/DiscordBot.Bot/package.json`), not loaded from a CDN for Blazor pages. The `build:vendor`
npm script copies `node_modules/chart.js/dist/chart.umd.js` to
`wwwroot/lib/chart.js/chart.umd.js` (gitignored, like `wwwroot/css/app.css`, and regenerated
on every `dotnet build` unless `SkipTailwind=true`/`CI=true`). `charts.js` loads it lazily:
if `window.Chart` is already defined (a legacy Razor page loaded the CDN copy first) it's
reused; otherwise the vendored script tag is injected once and awaited on the first
`create()` call. **No existing page's CDN `<script>` tag was removed** — Phase 4 removes
those as each chart page migrates to Blazor.

Graphite theme defaults are ported from `wwwroot/js/performance/components/chart-utils.js`
and read from CSS custom properties on `document.documentElement` **at create time**, so a
chart created after a theme switch picks up the new palette (existing charts are not
live-retinted — destroy and recreate to pick up a theme change mid-session).

### JS API

| Function | Signature | Notes |
| --- | --- | --- |
| `create` | `(canvasElement, config) => Promise<number>` | Returns a numeric handle. Merges Graphite defaults under `config.options`. |
| `update` | `(handle, data?, options?) => void` | Replaces `chart.data` / deep-merges `chart.options` when provided, then redraws. |
| `destroy` | `(handle) => void` | Destroys one chart. |
| `destroyAll` | `() => void` | Destroys every chart this module is tracking. |
| `buildDefaultOptions` | `(cssVarGetter) => object` | Pure; exported for the `node --test` unit tests. |
| `buildPalette` | `(cssVarGetter) => object` | Pure; the `{primary, secondary, success, warning, error, info, muted}` Graphite chart palette. |

### C# API (`ChartInterop`)

```csharp
Task<int> CreateAsync(ElementReference canvas, object config);
Task UpdateAsync(int handle, object? data = null, object? options = null);
Task DestroyAsync(int handle);
Task DestroyAllAsync();
```

## AudioInterop / audio.js

A single page-wide `<audio>` element handles preview playback so starting a new preview
always stops any other. Ports the audio/upload slivers of `portal-soundboard.js` (drag-drop,
client-side duration check, XHR upload with progress) and `portal-tts.js` (blob preview).

### JS API

| Function | Signature | Notes |
| --- | --- | --- |
| `playPreview` | `(url, dotNetRef?) => void` | Stops any current preview first. Calls `dotNetRef.OnPreviewEnded()` on end/error (not on deliberate interruption). |
| `stopPreview` | `() => void` | Silent stop — no callback. |
| `getDuration` | `(inputElement, index) => Promise<number>` | Throwaway `Audio` + object URL, mirrors the soundboard upload's duration check. Rejects if there's no file or the duration isn't finite. |
| `registerDropZone` | `(element, dotNetRef) => number` | Wires dragover/dragleave/drop. Calls `dotNetRef.OnDragStateChanged(bool)` on hover state changes and `dotNetRef.OnFilesDropped(count, token)` on drop. Returns a handle for `unregisterDropZone`. |
| `unregisterDropZone` | `(handle) => void` | Removes the listeners. |
| `upload` | `(source, url, antiforgeryToken, dotNetRef) => void` | `source` is either the `<input type=file>` element or a drop token from `OnFilesDropped`. XHR multipart POST; `dotNetRef.OnUploadProgress(percent)`, `OnUploadCompleted(status, responseText)`, `OnUploadFailed(message)`. |

The drop token from `OnFilesDropped(count, token)` is the piece that lets a component route
a drag-and-drop file into the same `upload()` call used for a picked file — hold onto the
token from that callback and pass it as `source` to `AudioInterop.UploadAsync(string, ...)`.

### C# API (`AudioInterop`)

```csharp
Task PlayPreviewAsync<TComponent>(string url, DotNetObjectReference<TComponent>? dotNetRef = null) where TComponent : class;
Task StopPreviewAsync();
Task<double> GetDurationAsync(ElementReference inputElement, int index);
Task<int> RegisterDropZoneAsync<TComponent>(ElementReference element, DotNetObjectReference<TComponent> dotNetRef) where TComponent : class;
Task UnregisterDropZoneAsync(int handle);
Task UploadAsync<TComponent>(ElementReference inputElement, string url, string antiforgeryToken, DotNetObjectReference<TComponent> dotNetRef) where TComponent : class;
Task UploadAsync<TComponent>(string dropToken, string url, string antiforgeryToken, DotNetObjectReference<TComponent> dotNetRef) where TComponent : class;
```

The antiforgery token is sent as the `RequestVerificationToken` header — the same header
name `api-client.js` and the other legacy pages use, so the server side needs no change.

## BrowserInterop / browser.js

A grab-bag of small browser APIs that don't warrant their own module, ported from
`navigation.js` (click-outside), `quick-actions.js` (focus trap), `settings.js` /
`moderation-settings.js` (`beforeunload` guard), `portal-vox.js` (`matchMedia`),
`timezone.js` (IANA detection), and `_EmphasisToolbar.cshtml`'s inline script (textarea
selection).

### JS API

| Function | Signature | Notes |
| --- | --- | --- |
| `storageGet` / `storageSet` / `storageRemove` | `(key[, value]) => string\|null \| bool` | Wrap `localStorage`; never throw (private browsing, quota, disabled storage all resolve false/null instead). |
| `copyToClipboard` | `(text) => Promise<bool>` | `navigator.clipboard.writeText`. |
| `focusElement` | `(element) => void` | |
| `scrollIntoView` | `(element, behavior?) => void` | `behavior` defaults to `"smooth"`. |
| `trapFocus` | `(element) => number` | Cycles Tab/Shift+Tab within `element`, focuses its first focusable descendant. Returns a handle for `releaseFocus`. |
| `releaseFocus` | `(handle) => void` | |
| `setBeforeUnloadGuard` | `(enabled) => void` | One page-wide `beforeunload` prompt (mirrors the legacy single-form-per-page assumption). |
| `matchMedia` | `(query, dotNetRef) => { handle, matches }` | Calls `dotNetRef.OnMediaChanged(bool)` on every change; the initial `matches` comes back synchronously so the caller doesn't need to wait for the first change event. |
| `unwatchMedia` | `(handle) => void` | |
| `getTimeZone` | `() => string` | IANA zone via `Intl`, falls back to `"UTC"`. |
| `onClickOutside` | `(element, dotNetRef) => number` | Calls `dotNetRef.OnClickOutside()` on an outside click. |
| `offClickOutside` | `(handle) => void` | |
| `getSelection` | `(textarea) => { start, end, value }` | |
| `setSelection` | `(textarea, start, end) => void` | Focuses the textarea and sets the range. |
| `insertAtSelection` | `(textarea, text) => void` | Replaces the selection, moves the caret, dispatches a bubbling `input` event so Blazor two-way bindings observe the change. |

### C# API (`BrowserInterop`)

```csharp
Task<string?> StorageGetAsync(string key);
Task<bool> StorageSetAsync(string key, string value);
Task<bool> StorageRemoveAsync(string key);
Task<bool> CopyToClipboardAsync(string text);
Task FocusElementAsync(ElementReference element);
Task ScrollIntoViewAsync(ElementReference element, string? behavior = null);
Task<int> TrapFocusAsync(ElementReference element);
Task ReleaseFocusAsync(int handle);
Task SetBeforeUnloadGuardAsync(bool enabled);
Task<MediaWatchResult> MatchMediaAsync<TComponent>(string query, DotNetObjectReference<TComponent> dotNetRef) where TComponent : class;
Task UnwatchMediaAsync(int handle);
Task<string> GetTimeZoneAsync();
Task<int> OnClickOutsideAsync<TComponent>(ElementReference element, DotNetObjectReference<TComponent> dotNetRef) where TComponent : class;
Task OffClickOutsideAsync(int handle);
Task<TextSelectionResult> GetSelectionAsync(ElementReference textarea);
Task SetSelectionAsync(ElementReference textarea, int start, int end);
Task InsertAtSelectionAsync(ElementReference textarea, string text);
```

`MediaWatchResult` (`{ int Handle, bool Matches }`) and `TextSelectionResult`
(`{ int Start, int End, string Value }`) are records in
`Bot/Blazor/Interop/TextSelectionResult.cs`.

## Testing

- **C#** (`tests/DiscordBot.Tests/Blazor/Interop/`): Moq-based tests per wrapper assert the
  exact module path passed to `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)`,
  the JS identifier and arguments passed to each `IJSObjectReference.InvokeAsync` call, and
  that `DisposeAsync` swallows `JSDisconnectedException` without importing the module first
  (nothing to dispose) and after (disposes the module reference). Generic void-returning JS
  calls (`InvokeVoidAsync`, which instantiates the framework-internal `IJSVoidResult` type
  parameter) are verified with Moq's `It.IsAnyType` matcher rather than naming that type.
- **JS** (`wwwroot/js/__tests__/blazor-charts.test.js`, `blazor-browser.test.js`): `node --test`
  covers the pure parts only — `charts.js`'s `buildDefaultOptions`/`buildPalette` (a
  `cssVarGetter` function stands in for reading `document.documentElement`, so no DOM is
  needed) and `browser.js`'s `resolveTimeZone` (a stubbed `Intl`) and the `storageGet/Set/Remove`
  trio (a stubbed `window.localStorage`, including the throw-and-report-false/null path).
  `wwwroot/js/blazor/package.json` sets `"type": "module"` for this one directory only, so
  Node treats these ES modules correctly via dynamic `import()` from the otherwise-CommonJS
  `wwwroot/js/__tests__/*.test.js` suite; it has no effect on the browser, which already
  treats a dynamically-imported script as a module regardless of file extension.
  `npm test` (in `src/DiscordBot.Bot`) runs the whole `__tests__` suite.

## Vendoring Chart.js

- `package.json`: `chart.js` (exact `4.4.1`) is a **runtime** dependency (not `devDependencies`,
  since `build:vendor` needs it copied into the published output).
- `npm run build:vendor`: a plain `node -e` copy (no new dev dependency) from
  `node_modules/chart.js/dist/chart.umd.js` to `wwwroot/lib/chart.js/chart.umd.js`.
- `npm run build`: runs `build:vendor` then `build:css`. `DiscordBot.Bot.csproj`'s
  `BuildTailwindCSS` MSBuild target and the `Dockerfile`'s Tailwind build step both call
  `npm run build` (not `build:css` directly) so the vendored copy is produced alongside the
  CSS on every build. Both are skipped when `SkipTailwind=true` or `CI=true`, same as before —
  set `SkipTailwind=true` when you're not touching CSS or Chart.js.
- `wwwroot/lib/` is gitignored (added next to the existing `wwwroot/css/app.css` entry) —
  it's regenerated output, not source.
- `.github/workflows/ci.yml` was left unchanged: it never called `npm run build:css` directly
  (it runs `npm ci` for caching/lint purposes while the MSBuild target itself is skipped via
  the `CI=true` environment variable GitHub Actions sets automatically).
