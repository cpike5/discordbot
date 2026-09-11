/**
 * Blazor interop module: audio preview, duration probing, drag-and-drop and
 * upload progress. Wrapped by `Blazor/Interop/AudioInterop.cs`.
 *
 * Ports the audio/upload slivers of `wwwroot/js/portal-soundboard.js` and
 * `wwwroot/js/portal-tts.js` behind a single shared `<audio>` element instead
 * of one per page.
 */

// ---------------------------------------------------------------------------
// Preview playback (one shared Audio element; a new preview stops any other)
// ---------------------------------------------------------------------------

let previewAudio = null;
let previewDotNetRef = null;

function handlePreviewEnded() {
    const ref = previewDotNetRef;
    detachPreviewListeners();
    previewAudio = null;
    previewDotNetRef = null;
    if (ref) {
        ref.invokeMethodAsync('OnPreviewEnded').catch(() => {
            // Circuit likely gone; nothing more to do.
        });
    }
}

function detachPreviewListeners() {
    if (previewAudio) {
        previewAudio.removeEventListener('ended', handlePreviewEnded);
        previewAudio.removeEventListener('error', handlePreviewEnded);
    }
}

/**
 * Plays `url` through a single shared `<audio>` element, stopping any preview
 * already in progress first. If `dotNetRef` is supplied, its `OnPreviewEnded`
 * method is invoked when playback ends or errors (but not when a later call
 * to `playPreview`/`stopPreview` interrupts it deliberately).
 *
 * @param {string} url
 * @param {object} [dotNetRef] A DotNetObjectReference whose OnPreviewEnded method is invoked on end/error.
 */
export function playPreview(url, dotNetRef) {
    stopPreview();

    previewAudio = new Audio(url);
    previewDotNetRef = dotNetRef || null;
    previewAudio.addEventListener('ended', handlePreviewEnded);
    previewAudio.addEventListener('error', handlePreviewEnded);
    previewAudio.play().catch(() => handlePreviewEnded());
}

/** Stops the current preview, if any, without invoking `OnPreviewEnded`. */
export function stopPreview() {
    if (previewAudio) {
        detachPreviewListeners();
        previewAudio.pause();
        previewAudio.currentTime = 0;
    }
    previewAudio = null;
    previewDotNetRef = null;
}

// ---------------------------------------------------------------------------
// Duration probing (throwaway Audio + object URL, mirrors portal-soundboard.js)
// ---------------------------------------------------------------------------

/**
 * Reads the duration, in seconds, of a `File` held by an
 * `<input type="file">` element, via a throwaway `Audio` element and an
 * object URL. Rejects if there is no file at `index` or the browser cannot
 * determine a finite duration.
 *
 * @param {HTMLInputElement} inputElement
 * @param {number} index
 * @returns {Promise<number>}
 */
export function getDuration(inputElement, index) {
    return new Promise((resolve, reject) => {
        const file = inputElement && inputElement.files ? inputElement.files[index] : null;
        if (!file) {
            reject(new Error('No file at the given index.'));
            return;
        }

        const audio = new Audio();
        const objectUrl = URL.createObjectURL(file);
        audio.src = objectUrl;

        audio.onloadedmetadata = () => {
            URL.revokeObjectURL(objectUrl);
            if (!isFinite(audio.duration)) {
                reject(new Error('Could not determine audio duration.'));
                return;
            }
            resolve(audio.duration);
        };

        audio.onerror = () => {
            URL.revokeObjectURL(objectUrl);
            reject(new Error('Could not read audio file. The file may be corrupted.'));
        };
    });
}

// ---------------------------------------------------------------------------
// Drag-and-drop zones
// ---------------------------------------------------------------------------

/** handle (number) -> { element, onDragOver, onDragLeave, onDrop } */
const dropZones = new Map();
let nextDropZoneHandle = 1;

/** token (string) -> File[] */
const droppedFiles = new Map();
let nextDropToken = 1;

/**
 * Wires dragover/dragleave/drop listeners on `element`. Calls
 * `dotNetRef.OnDragStateChanged(bool)` when the drag-over state changes, and
 * `dotNetRef.OnFilesDropped(count, token)` on drop, where `token` can later
 * be passed as the `source` argument to {@link upload}. Dropped files are
 * held in a module-level map and are removed once `upload` consumes the
 * token (or never claimed, if the caller doesn't upload).
 *
 * @param {HTMLElement} element
 * @param {object} dotNetRef A DotNetObjectReference exposing OnDragStateChanged and OnFilesDropped.
 * @returns {number} A handle to pass to {@link unregisterDropZone}.
 */
export function registerDropZone(element, dotNetRef) {
    let isOver = false;

    const setOver = (value) => {
        if (isOver === value) {
            return;
        }
        isOver = value;
        dotNetRef.invokeMethodAsync('OnDragStateChanged', value).catch(() => {});
    };

    const onDragOver = (event) => {
        event.preventDefault();
        event.stopPropagation();
        setOver(true);
    };

    const onDragLeave = (event) => {
        event.preventDefault();
        event.stopPropagation();
        setOver(false);
    };

    const onDrop = (event) => {
        event.preventDefault();
        event.stopPropagation();
        setOver(false);

        const files = event.dataTransfer ? Array.from(event.dataTransfer.files) : [];
        const token = `drop-${nextDropToken++}`;
        droppedFiles.set(token, files);
        dotNetRef.invokeMethodAsync('OnFilesDropped', files.length, token).catch(() => {});
    };

    element.addEventListener('dragover', onDragOver);
    element.addEventListener('dragleave', onDragLeave);
    element.addEventListener('drop', onDrop);

    const handle = nextDropZoneHandle++;
    dropZones.set(handle, { element, onDragOver, onDragLeave, onDrop });
    return handle;
}

/**
 * Removes the listeners registered by {@link registerDropZone}.
 * @param {number} handle
 */
export function unregisterDropZone(handle) {
    const entry = dropZones.get(handle);
    if (!entry) {
        return;
    }
    entry.element.removeEventListener('dragover', entry.onDragOver);
    entry.element.removeEventListener('dragleave', entry.onDragLeave);
    entry.element.removeEventListener('drop', entry.onDrop);
    dropZones.delete(handle);
}

// ---------------------------------------------------------------------------
// Release-all — last-resort cleanup for a circuit that goes away without
// every component getting a chance to call its own release method (a lost
// connection, a crashed circuit). Registered below on the Blazor
// enhanced-navigation and page-unload hooks so this module never holds
// listeners or dropped `File` objects past the circuit that created them.
// ---------------------------------------------------------------------------

/**
 * Stops any preview in progress, releases every registered drop zone, and
 * discards any dropped-but-never-uploaded `File` objects. Safe to call with
 * nothing registered. Exported for the Blazor lifecycle hooks below and for
 * a component/layout that wants an explicit last-resort cleanup of its own.
 */
export function releaseAll() {
    stopPreview();
    for (const handle of Array.from(dropZones.keys())) {
        unregisterDropZone(handle);
    }
    droppedFiles.clear();
}

if (typeof window !== 'undefined') {
    // Enhanced navigation swaps the page without a full reload, so a
    // component's own DisposeAsync may never run for the elements it
    // registered listeners on; releaseAll on every enhanced navigation keeps
    // those - and any held File objects - from accumulating for the life of
    // the circuit.
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', releaseAll);
    }
    // pagehide is the reliable fallback for circuit loss (tab close, network
    // drop, crash): Blazor Server has no client-side "circuit down" DOM
    // event, but pagehide always fires before the page/tab actually goes
    // away, unlike beforeunload which some browsers skip on a fast/backward
    // navigation.
    window.addEventListener('pagehide', releaseAll);
}

// ---------------------------------------------------------------------------
// Upload
// ---------------------------------------------------------------------------

function resolveFiles(source) {
    if (typeof source === 'string') {
        const files = droppedFiles.get(source) || [];
        droppedFiles.delete(source);
        return files;
    }
    // Blazor unwraps an ElementReference to the underlying DOM element.
    return source && source.files ? Array.from(source.files) : [];
}

/**
 * Uploads the file(s) from `source` (an `<input type="file">` element, or a
 * drop token returned by {@link registerDropZone}'s `OnFilesDropped`
 * callback) to `url` as `multipart/form-data`, via `XMLHttpRequest` so
 * upload progress is observable.
 *
 * Invokes `dotNetRef.OnUploadProgress(percent)` while sending,
 * `dotNetRef.OnUploadCompleted(status, responseText)` when the server
 * responds (any status — the caller decides success/failure from `status`),
 * and `dotNetRef.OnUploadFailed(message)` on a network error or when there is
 * no file to send.
 *
 * @param {HTMLInputElement | string} source
 * @param {string} url
 * @param {string} antiforgeryToken Sent as the `RequestVerificationToken` header.
 * @param {object} dotNetRef A DotNetObjectReference exposing OnUploadProgress, OnUploadCompleted and OnUploadFailed.
 */
export function upload(source, url, antiforgeryToken, dotNetRef) {
    const files = resolveFiles(source);
    if (!files.length) {
        dotNetRef.invokeMethodAsync('OnUploadFailed', 'No file selected.').catch(() => {});
        return;
    }

    const formData = new FormData();
    files.forEach((file) => formData.append('file', file));

    const xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);
    if (antiforgeryToken) {
        xhr.setRequestHeader('RequestVerificationToken', antiforgeryToken);
    }

    xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) {
            const percent = Math.round((event.loaded / event.total) * 100);
            dotNetRef.invokeMethodAsync('OnUploadProgress', percent).catch(() => {});
        }
    };

    xhr.onload = () => {
        dotNetRef.invokeMethodAsync('OnUploadCompleted', xhr.status, xhr.responseText).catch(() => {});
    };

    xhr.onerror = () => {
        dotNetRef.invokeMethodAsync('OnUploadFailed', 'Network error. Please check your connection and try again.').catch(() => {});
    };

    xhr.send(formData);
}
