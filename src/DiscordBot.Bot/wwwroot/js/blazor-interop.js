// Blazor interop shim.
//
// The existing toast.js / theme.js modules expose `ToastManager` and `ThemeManager`
// as top-level `const`s (classic scripts), so they live in the global lexical scope
// but are NOT properties of `window`. Blazor's IJSRuntime resolves dotted paths
// against `window`/`globalThis`, so it cannot reach them directly.
//
// This shim is a classic script loaded AFTER toast.js/theme.js. It can reference
// those lexically-scoped bindings and re-exposes a small, stable, window-attached
// surface for Blazor interop services to call.
(function () {
    "use strict";

    window.blazorInterop = {
        // Bridges DiscordBot.Bot.Blazor.Interop.ToastInterop -> ToastManager.show(type, message, options)
        toast: function (type, message, title) {
            try {
                if (typeof ToastManager !== "undefined" && ToastManager) {
                    ToastManager.show(type, message, title ? { title: title } : {});
                } else {
                    console.warn("blazorInterop.toast: ToastManager not loaded");
                }
            } catch (e) {
                console.error("blazorInterop.toast failed", e);
            }
        },

        // Bridges DiscordBot.Bot.Blazor.Interop.ThemeInterop -> ThemeManager.applyTheme(key, persist)
        applyTheme: function (themeKey, persistToServer) {
            try {
                if (typeof ThemeManager !== "undefined" && ThemeManager) {
                    ThemeManager.applyTheme(themeKey, persistToServer === true);
                } else {
                    console.warn("blazorInterop.applyTheme: ThemeManager not loaded");
                }
            } catch (e) {
                console.error("blazorInterop.applyTheme failed", e);
            }
        },

        // Triggers a browser file download for the given URL without navigating away
        // (the Blazor equivalent of the legacy `window.location.href = exportUrl` for
        // CSV exports). Uses a transient anchor so content-disposition attachments
        // download in place.
        download: function (url) {
            try {
                var a = document.createElement("a");
                a.href = url;
                a.rel = "noopener";
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
            } catch (e) {
                console.error("blazorInterop.download failed", e);
            }
        },

        // Downloads in-memory text content as a named file (Blob + object URL) — the
        // Blazor equivalent of the legacy inline export scripts that built a Blob and
        // clicked a transient anchor (e.g. the audit-entry JSON export).
        downloadData: function (filename, mimeType, content) {
            try {
                var blob = new Blob([content], { type: mimeType || "application/octet-stream" });
                var url = URL.createObjectURL(blob);
                var a = document.createElement("a");
                a.href = url;
                a.download = filename || "download";
                a.rel = "noopener";
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            } catch (e) {
                console.error("blazorInterop.downloadData failed", e);
            }
        },

        // Copies text to the clipboard; returns true on success so the caller can
        // raise the matching toast from .NET. Mirrors the legacy copyUserId helper.
        copyText: async function (text) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (e) {
                console.error("blazorInterop.copyText failed", e);
                return false;
            }
        },

        // Re-runs the shared timezone conversion over any [data-utc] spans an island
        // just rendered (table dates, modal timestamps). Safe to call repeatedly.
        convertTimes: function () {
            try {
                if (window.timezoneUtils && typeof window.timezoneUtils.convertDisplayTimes === "function") {
                    window.timezoneUtils.convertDisplayTimes();
                }
            } catch (e) {
                console.error("blazorInterop.convertTimes failed", e);
            }
        },

        // Sets/clears the indeterminate state on a checkbox element (not settable via
        // HTML attributes) — used by the member directory's select-all tristate.
        setIndeterminate: function (element, value) {
            if (element) {
                element.indeterminate = value === true;
            }
        },

        // Arms/disarms a beforeunload guard so islands with unsaved changes warn the
        // user before they navigate away (the Blazor equivalent of the legacy
        // setupUnloadWarning in moderation-settings.js). TabbedFormShell toggles this
        // as its centralized dirty flag changes.
        setUnsavedGuard: function (enabled) {
            if (enabled) {
                if (!window.__blazorUnsavedGuard) {
                    var handler = function (e) {
                        e.preventDefault();
                        e.returnValue = "You have unsaved changes. Are you sure you want to leave?";
                        return e.returnValue;
                    };
                    window.__blazorUnsavedGuard = handler;
                    window.addEventListener("beforeunload", handler);
                }
            } else if (window.__blazorUnsavedGuard) {
                window.removeEventListener("beforeunload", window.__blazorUnsavedGuard);
                window.__blazorUnsavedGuard = null;
            }
        }
    };
})();
