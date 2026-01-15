/**
 * Theme Manager Module
 * Handles theme application, persistence, and synchronization.
 *
 * Theme persistence strategy:
 * - Cookie (theme-preference): Read by server for SSR
 * - localStorage (theme-preference): Fast client-side access
 *
 * Both are kept in sync for seamless UX across page loads.
 */
const ThemeManager = {
    COOKIE_NAME: 'theme-preference',
    STORAGE_KEY: 'theme-preference',
    COOKIE_MAX_AGE: 31536000, // 1 year in seconds

    /**
     * Applies a theme immediately and persists the preference.
     * @param {string} themeKey - The theme key (e.g., 'discord-dark', 'purple-dusk')
     * @param {boolean} persistToServer - If true, also sends preference to server API (for authenticated users)
     */
    applyTheme(themeKey, persistToServer = false) {
        if (!themeKey) {
            console.warn('ThemeManager: No theme key provided');
            return;
        }

        // Apply to DOM immediately
        document.documentElement.setAttribute('data-theme', themeKey);

        // Persist to cookie (for SSR on next page load)
        this.setCookie(themeKey);

        // Persist to localStorage (for JS access)
        try {
            localStorage.setItem(this.STORAGE_KEY, themeKey);
        } catch (e) {
            console.warn('ThemeManager: localStorage not available', e);
        }

        // Dispatch custom event for other components to react
        window.dispatchEvent(new CustomEvent('themechange', {
            detail: { themeKey }
        }));

        // Optionally persist to server for authenticated users
        if (persistToServer) {
            this.persistToServer(themeKey);
        }
    },

    /**
     * Gets the current theme from cookie or localStorage.
     * @returns {string|null} The current theme key, or null if not set
     */
    getCurrentTheme() {
        // Check cookie first (SSR source of truth)
        const cookieTheme = this.getCookie();
        if (cookieTheme) return cookieTheme;

        // Fallback to localStorage
        try {
            return localStorage.getItem(this.STORAGE_KEY);
        } catch (e) {
            return null;
        }
    },

    /**
     * Gets the current theme from the DOM.
     * @returns {string|null} The data-theme attribute value, or null if not set
     */
    getActiveTheme() {
        return document.documentElement.getAttribute('data-theme');
    },

    /**
     * Clears the theme preference, reverting to system default.
     * @param {boolean} persistToServer - If true, also clears preference on server
     */
    clearTheme(persistToServer = false) {
        document.documentElement.removeAttribute('data-theme');

        // Clear cookie
        document.cookie = `${this.COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;

        // Clear localStorage
        try {
            localStorage.removeItem(this.STORAGE_KEY);
        } catch (e) {
            console.warn('ThemeManager: localStorage not available', e);
        }

        // Dispatch custom event
        window.dispatchEvent(new CustomEvent('themechange', {
            detail: { themeKey: null }
        }));

        if (persistToServer) {
            this.clearServerPreference();
        }
    },

    /**
     * Sets the theme preference cookie.
     * @param {string} themeKey - The theme key to persist
     */
    setCookie(themeKey) {
        document.cookie = `${this.COOKIE_NAME}=${encodeURIComponent(themeKey)}; path=/; max-age=${this.COOKIE_MAX_AGE}; SameSite=Lax`;
    },

    /**
     * Gets the theme preference from cookie.
     * @returns {string|null} The theme key from cookie, or null if not set
     */
    getCookie() {
        const match = document.cookie.match(new RegExp(`(?:^|; )${this.COOKIE_NAME}=([^;]*)`));
        return match ? decodeURIComponent(match[1]) : null;
    },

    /**
     * Persists the theme preference to the server via API.
     * Called for authenticated users to save preference to database.
     * @param {string} themeKey - The theme key to persist
     */
    async persistToServer(themeKey) {
        try {
            const response = await fetch('/api/theme/preference', {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ themeKey })
            });

            if (!response.ok) {
                console.warn('ThemeManager: Failed to persist theme to server', response.status);
            }
        } catch (e) {
            console.warn('ThemeManager: Error persisting theme to server', e);
        }
    },

    /**
     * Clears the theme preference on the server.
     */
    async clearServerPreference() {
        try {
            const response = await fetch('/api/theme/preference', {
                method: 'DELETE',
                headers: {
                    'RequestVerificationToken': this.getAntiForgeryToken()
                }
            });

            if (!response.ok) {
                console.warn('ThemeManager: Failed to clear server preference', response.status);
            }
        } catch (e) {
            console.warn('ThemeManager: Error clearing server preference', e);
        }
    },

    /**
     * Gets the anti-forgery token from the page.
     * @returns {string} The token value, or empty string if not found
     */
    getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    },

    /**
     * Initializes the theme manager.
     * Called on page load to ensure theme is applied (fallback for SSR).
     */
    init() {
        // The theme should already be set by SSR or blocking script.
        // This is a safety net in case neither worked.
        const domTheme = this.getActiveTheme();
        const storedTheme = this.getCurrentTheme();

        if (!domTheme && storedTheme) {
            // DOM doesn't have theme but we have a stored preference
            document.documentElement.setAttribute('data-theme', storedTheme);
        }

        // Listen for storage events from other tabs
        window.addEventListener('storage', (e) => {
            if (e.key === this.STORAGE_KEY && e.newValue) {
                document.documentElement.setAttribute('data-theme', e.newValue);
            }
        });
    }
};

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ThemeManager.init());
} else {
    ThemeManager.init();
}

// Export for ES modules (if needed)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ThemeManager;
}
