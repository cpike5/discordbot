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
