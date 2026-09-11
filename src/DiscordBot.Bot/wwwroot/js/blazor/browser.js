/**
 * Blazor interop module: small browser API helpers that don't warrant their
 * own module — localStorage, clipboard, focus/scroll, a modal focus trap, a
 * `beforeunload` dirty guard, `matchMedia` watching, IANA timezone
 * detection, click-outside detection, and textarea selection manipulation.
 * Wrapped by `Blazor/Interop/BrowserInterop.cs`.
 *
 * Ports scattered helpers from `navigation.js`, `quick-actions.js`,
 * `settings.js`/`moderation-settings.js`, `portal-vox.js`, `timezone.js` and
 * `Pages/Shared/Components/_EmphasisToolbar.cshtml`.
 *
 * Every handler registry here (focus traps, matchMedia watchers,
 * click-outside listeners) is disposable via a numeric handle, so a circuit
 * that creates and disposes components repeatedly does not leak listeners.
 */

// ---------------------------------------------------------------------------
// localStorage
// ---------------------------------------------------------------------------

/**
 * @param {string} key
 * @returns {string | null} The stored value, or null if absent or storage is unavailable.
 */
export function storageGet(key) {
    try {
        return window.localStorage.getItem(key);
    } catch (e) {
        return null;
    }
}

/**
 * @param {string} key
 * @param {string} value
 * @returns {boolean} Whether the write succeeded.
 */
export function storageSet(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch (e) {
        return false;
    }
}

/**
 * @param {string} key
 * @returns {boolean} Whether the removal succeeded (or the key was already absent).
 */
export function storageRemove(key) {
    try {
        window.localStorage.removeItem(key);
        return true;
    } catch (e) {
        return false;
    }
}

// ---------------------------------------------------------------------------
// Clipboard
// ---------------------------------------------------------------------------

/**
 * @param {string} text
 * @returns {Promise<boolean>} Whether the copy succeeded.
 */
export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (e) {
        return false;
    }
}

// ---------------------------------------------------------------------------
// Focus / scroll
// ---------------------------------------------------------------------------

/** @param {HTMLElement} element */
export function focusElement(element) {
    if (element && typeof element.focus === 'function') {
        element.focus();
    }
}

/**
 * @param {HTMLElement} element
 * @param {ScrollBehavior} [behavior]
 */
export function scrollIntoView(element, behavior) {
    if (element && typeof element.scrollIntoView === 'function') {
        element.scrollIntoView({ behavior: behavior || 'smooth', block: 'nearest' });
    }
}

// ---------------------------------------------------------------------------
// Focus trap (modals) — ports quick-actions.js's Tab-key trap
// ---------------------------------------------------------------------------

const FOCUSABLE_SELECTOR = [
    'a[href]', 'button:not([disabled])', 'textarea:not([disabled])',
    'input:not([disabled])', 'select:not([disabled])', '[tabindex]:not([tabindex="-1"])'
].join(', ');

/** handle (number) -> { element, handleKeydown } */
const focusTraps = new Map();
let nextFocusTrapHandle = 1;

function getFocusable(container) {
    return Array.from(container.querySelectorAll(FOCUSABLE_SELECTOR))
        .filter((el) => el.offsetParent !== null || el === document.activeElement);
}

/**
 * Traps Tab/Shift+Tab focus cycling within `element` and focuses its first
 * focusable descendant. Returns a handle for {@link releaseFocus}.
 *
 * @param {HTMLElement} element
 * @returns {number}
 */
export function trapFocus(element) {
    const handleKeydown = (event) => {
        if (event.key !== 'Tab') {
            return;
        }
        const focusable = getFocusable(element);
        if (focusable.length === 0) {
            return;
        }
        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    };

    element.addEventListener('keydown', handleKeydown);

    const focusable = getFocusable(element);
    if (focusable.length > 0) {
        focusable[0].focus();
    }

    const handle = nextFocusTrapHandle++;
    focusTraps.set(handle, { element, handleKeydown });
    return handle;
}

/** @param {number} handle */
export function releaseFocus(handle) {
    const entry = focusTraps.get(handle);
    if (!entry) {
        return;
    }
    entry.element.removeEventListener('keydown', entry.handleKeydown);
    focusTraps.delete(handle);
}

// ---------------------------------------------------------------------------
// beforeunload dirty guard — ports settings.js / moderation-settings.js
// ---------------------------------------------------------------------------

let beforeUnloadEnabled = false;

function handleBeforeUnload(event) {
    if (!beforeUnloadEnabled) {
        return;
    }
    event.preventDefault();
    event.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
    return event.returnValue;
}

if (typeof window !== 'undefined' && window.addEventListener) {
    // Registered once at module load; guarded by beforeUnloadEnabled so most
    // pages (guard disabled) pay only for one inert listener. Not testable
    // from Node without a `window` global, hence the guard here rather than
    // relying on the module failing to load under `node --test`.
    window.addEventListener('beforeunload', handleBeforeUnload);
}

/** @param {boolean} enabled */
export function setBeforeUnloadGuard(enabled) {
    beforeUnloadEnabled = !!enabled;
}

// ---------------------------------------------------------------------------
// matchMedia watching — ports portal-vox.js's responsive checks
// ---------------------------------------------------------------------------

/** handle (number) -> { mql, handleChange } */
const mediaWatchers = new Map();
let nextMediaHandle = 1;

/**
 * @param {string} query e.g. "(max-width: 1023px)"
 * @param {object} dotNetRef A DotNetObjectReference whose OnMediaChanged(bool) is invoked on change.
 * @returns {{ handle: number, matches: boolean }}
 */
export function matchMedia(query, dotNetRef) {
    const mql = window.matchMedia(query);
    const handleChange = (event) => {
        dotNetRef.invokeMethodAsync('OnMediaChanged', event.matches).catch(() => {});
    };
    mql.addEventListener('change', handleChange);

    const handle = nextMediaHandle++;
    mediaWatchers.set(handle, { mql, handleChange });
    return { handle, matches: mql.matches };
}

/** @param {number} handle */
export function unwatchMedia(handle) {
    const entry = mediaWatchers.get(handle);
    if (!entry) {
        return;
    }
    entry.mql.removeEventListener('change', entry.handleChange);
    mediaWatchers.delete(handle);
}

// ---------------------------------------------------------------------------
// Timezone — ports timezone.js's getTimezone
// ---------------------------------------------------------------------------

/**
 * Pure so it is testable with a stubbed `Intl`
 * (see `wwwroot/js/__tests__/blazor-browser.test.js`).
 *
 * @param {typeof Intl} [intlImpl]
 * @returns {string} IANA timezone identifier (e.g. "America/New_York"), or "UTC" on failure.
 */
export function resolveTimeZone(intlImpl) {
    try {
        const impl = intlImpl || Intl;
        return impl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
    } catch (e) {
        return 'UTC';
    }
}

/** @returns {string} IANA timezone identifier. */
export function getTimeZone() {
    return resolveTimeZone(typeof Intl !== 'undefined' ? Intl : undefined);
}

// ---------------------------------------------------------------------------
// Click outside — ports navigation.js's dropdown-close handler
// ---------------------------------------------------------------------------

/** handle (number) -> handleClick */
const clickOutsideHandlers = new Map();
let nextClickOutsideHandle = 1;

/**
 * Invokes `dotNetRef.OnClickOutside()` when a click lands outside `element`.
 * @param {HTMLElement} element
 * @param {object} dotNetRef
 * @returns {number}
 */
export function onClickOutside(element, dotNetRef) {
    const handleClick = (event) => {
        if (!element.contains(event.target)) {
            dotNetRef.invokeMethodAsync('OnClickOutside').catch(() => {});
        }
    };
    document.addEventListener('click', handleClick);

    const handle = nextClickOutsideHandle++;
    clickOutsideHandlers.set(handle, handleClick);
    return handle;
}

/** @param {number} handle */
export function offClickOutside(handle) {
    const handleClick = clickOutsideHandlers.get(handle);
    if (!handleClick) {
        return;
    }
    document.removeEventListener('click', handleClick);
    clickOutsideHandlers.delete(handle);
}

// ---------------------------------------------------------------------------
// Release-all — last-resort cleanup for a circuit that goes away without
// every component getting a chance to call its own release method (a lost
// connection, a crashed circuit). Registered below on the Blazor
// enhanced-navigation and page-unload hooks so listeners registered by this
// module never outlive the circuit that created them.
// ---------------------------------------------------------------------------

/**
 * Releases every focus trap, matchMedia watcher and click-outside listener
 * this module is currently tracking. Safe to call with nothing registered.
 * Exported for the Blazor lifecycle hooks below and for a component/layout
 * that wants an explicit last-resort cleanup of its own.
 */
export function releaseAll() {
    for (const handle of Array.from(focusTraps.keys())) {
        releaseFocus(handle);
    }
    for (const handle of Array.from(mediaWatchers.keys())) {
        unwatchMedia(handle);
    }
    for (const handle of Array.from(clickOutsideHandlers.keys())) {
        offClickOutside(handle);
    }
}

if (typeof window !== 'undefined') {
    // Enhanced navigation swaps the page without a full reload, so a
    // component's own DisposeAsync may never run for the elements it
    // registered listeners on; releaseAll on every enhanced navigation keeps
    // those from accumulating for the life of the circuit.
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
// Textarea selection — ports _EmphasisToolbar.cshtml's inline script
// ---------------------------------------------------------------------------

/**
 * @param {HTMLTextAreaElement} textarea
 * @returns {{ start: number, end: number, value: string }}
 */
export function getSelection(textarea) {
    return {
        start: textarea.selectionStart,
        end: textarea.selectionEnd,
        value: textarea.value.substring(textarea.selectionStart, textarea.selectionEnd)
    };
}

/**
 * @param {HTMLTextAreaElement} textarea
 * @param {number} start
 * @param {number} end
 */
export function setSelection(textarea, start, end) {
    textarea.focus();
    textarea.setSelectionRange(start, end);
}

/**
 * Replaces the current selection with `text`, moves the caret to the end of
 * the inserted text, and dispatches a bubbling `input` event so Blazor
 * two-way bindings (and any other `input` listeners) observe the change.
 *
 * @param {HTMLTextAreaElement} textarea
 * @param {string} text
 */
export function insertAtSelection(textarea, text) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const before = textarea.value.substring(0, start);
    const after = textarea.value.substring(end);

    textarea.value = before + text + after;
    const caret = start + text.length;
    textarea.setSelectionRange(caret, caret);

    textarea.dispatchEvent(new Event('input', { bubbles: true }));
}
