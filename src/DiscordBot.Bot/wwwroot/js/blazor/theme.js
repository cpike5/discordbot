/**
 * Blazor interop module for client-side theme switching. Mirrors the client contract
 * wwwroot/js/theme.js's ThemeManager owns for the legacy Razor Pages shell (same cookie name,
 * same localStorage key, same 1-year cookie lifetime, same `themechange` CustomEvent) so both
 * stacks read/write theme state identically while they coexist - a theme switch made from a
 * Blazor page is visible immediately on a Razor Page the user navigates to next, and vice versa.
 * Wrapped by `Blazor/Interop/ThemeInterop.cs`.
 *
 * Deliberately does NOT call the server (no /api/theme/preference fetch, unlike theme.js's
 * persistToServer): the C# side persists via IThemeService.SetUserThemeAsync directly (a normal
 * service call, no HTTP round trip needed from inside a circuit) - see ThemeInterop.cs and the
 * demo in Blazor/Pages/Admin/BlazorProbe.razor.
 */

const COOKIE_NAME = 'theme-preference';
const STORAGE_KEY = 'theme-preference';
const COOKIE_MAX_AGE = 31536000; // 1 year in seconds, matching theme.js's ThemeManager.COOKIE_MAX_AGE

/**
 * Applies a theme immediately: sets `data-theme` on `<html>`, persists it to the theme-preference
 * cookie (path=/, SameSite=Lax, 1-year max-age) and localStorage, and dispatches a `themechange`
 * CustomEvent on `window` (`detail: { themeKey }`) so any other listener (a future live toggle,
 * another tab's `storage` listener) reacts the same way it would to a legacy-page theme change.
 *
 * @param {string} themeKey e.g. "discord-dark"
 */
export function apply(themeKey) {
    if (!themeKey) {
        return;
    }

    document.documentElement.setAttribute('data-theme', themeKey);

    try {
        document.cookie = `${COOKIE_NAME}=${encodeURIComponent(themeKey)}; path=/; max-age=${COOKIE_MAX_AGE}; SameSite=Lax`;
    } catch (e) {
        // Cookie write can fail under strict privacy settings; localStorage below still works.
    }

    try {
        window.localStorage.setItem(STORAGE_KEY, themeKey);
    } catch (e) {
        // Storage unavailable (private mode, quota) - the DOM attribute and cookie already applied.
    }

    window.dispatchEvent(new CustomEvent('themechange', { detail: { themeKey } }));
}

/**
 * Clears the theme preference (cookie, localStorage and the `data-theme` attribute), reverting to
 * the system default the next time a page resolves a theme server-side. Dispatches `themechange`
 * with a null `themeKey`.
 */
export function clear() {
    document.documentElement.removeAttribute('data-theme');

    try {
        document.cookie = `${COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;
    } catch (e) {
        // Best-effort.
    }

    try {
        window.localStorage.removeItem(STORAGE_KEY);
    } catch (e) {
        // Best-effort.
    }

    window.dispatchEvent(new CustomEvent('themechange', { detail: { themeKey: null } }));
}

/**
 * @returns {string | null} The current theme key: the cookie value if set (the same
 * source-of-truth server-side rendering reads), else the localStorage value, else null.
 */
export function getCurrent() {
    const match = document.cookie.match(new RegExp(`(?:^|; )${COOKIE_NAME}=([^;]*)`));
    if (match) {
        return decodeURIComponent(match[1]);
    }

    try {
        return window.localStorage.getItem(STORAGE_KEY);
    } catch (e) {
        return null;
    }
}
