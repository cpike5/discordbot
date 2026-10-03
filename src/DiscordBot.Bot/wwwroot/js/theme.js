/**
 * Theme Manager Module
 * Applies, saves and follows the UI theme (UX decision D5).
 *
 * - Pages/Shared/_ThemeHead.cshtml picks the first-paint theme: a saved choice, otherwise
 *   the OS `prefers-color-scheme`. It also defines window.ThemeConfig (dark/light keys and
 *   the browser UI colours).
 * - With no saved choice the page keeps following the OS while it is open.
 * - A [data-theme-toggle] button switches between the dark and light theme and saves the
 *   choice: in the theme-preference cookie (read by the server on the next request), in
 *   localStorage (other open tabs follow), and for a signed-in user on the server
 *   (PUT /api/theme/preference) when the button has data-theme-persist="true".
 * - Every change dispatches `themechange` on window, with { themeKey, saved } in detail.
 *   chart-theme.js redraws charts on it.
 */
const ThemeManager = {
    COOKIE_NAME: 'theme-preference',
    STORAGE_KEY: 'theme-preference',
    COOKIE_MAX_AGE: 31536000, // 1 year in seconds

    /** Dark/light keys and browser colours, from _ThemeHead (with the same defaults). */
    get config() {
        return window.ThemeConfig || {
            dark: 'discord-dark',
            light: 'purple-dusk',
            colors: { 'discord-dark': '#0f1114', 'purple-dusk': '#ebe6e2' }
        };
    },

    /**
     * Applies a theme and saves it as the visitor's choice.
     * @param {string} themeKey - The theme key (e.g., 'discord-dark', 'purple-dusk')
     * @param {boolean} persistToServer - Also save it for the signed-in user
     */
    applyTheme(themeKey, persistToServer = false) {
        if (!themeKey) return;

        this.setCookie(themeKey);
        try {
            localStorage.setItem(this.STORAGE_KEY, themeKey);
        } catch (e) {
            // Blocked storage: the cookie still carries the choice
        }

        this.render(themeKey, true);

        if (persistToServer) {
            this.persistToServer(themeKey);
        }
    },

    /**
     * Forgets the saved choice and follows the OS preference again.
     * @param {boolean} persistToServer - Also clear it for the signed-in user
     */
    clearTheme(persistToServer = false) {
        document.cookie = `${this.COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;
        try {
            localStorage.removeItem(this.STORAGE_KEY);
        } catch (e) {
            // Nothing to clear
        }

        this.render(this.getSystemTheme() || this.config.dark, false);

        if (persistToServer) {
            this.clearServerPreference();
        }
    },

    /** Switches between the dark and the light theme. */
    toggle(persistToServer = false) {
        const next = this.isLight() ? this.config.dark : this.config.light;
        this.applyTheme(next, persistToServer);
    },

    /**
     * Puts a theme on the page without saving anything: data-theme, the browser UI colour,
     * toggle labels and the themechange event.
     */
    render(themeKey, saved) {
        const root = document.documentElement;
        const changed = root.getAttribute('data-theme') !== themeKey;

        root.setAttribute('data-theme', themeKey);
        root.setAttribute('data-theme-saved', saved ? 'true' : 'false');
        this.syncThemeColor(themeKey);
        this.labelToggles();

        if (changed) {
            window.dispatchEvent(new CustomEvent('themechange', {
                detail: { themeKey, saved }
            }));
        }
    },

    /** @returns {string|null} The data-theme attribute value, or null if not set */
    getActiveTheme() {
        return document.documentElement.getAttribute('data-theme');
    },

    /** @returns {boolean} Whether the visitor has chosen a theme (rather than following the OS) */
    isSaved() {
        return document.documentElement.getAttribute('data-theme-saved') === 'true';
    },

    /** @returns {boolean} Whether the light theme is showing */
    isLight() {
        return this.getActiveTheme() === this.config.light;
    },

    /** @returns {string|null} The theme matching the OS colour scheme, or null if it states none */
    getSystemTheme() {
        if (!window.matchMedia) return null;
        if (window.matchMedia('(prefers-color-scheme: light)').matches) return this.config.light;
        if (window.matchMedia('(prefers-color-scheme: dark)').matches) return this.config.dark;
        return null;
    },

    /** Keeps <meta name="theme-color"> on the theme's canvas colour. */
    syncThemeColor(themeKey) {
        const meta = document.querySelector('meta[name="theme-color"]');
        if (!meta) return;
        const fromTokens = getComputedStyle(document.documentElement)
            .getPropertyValue('--color-bg-primary').trim();
        meta.setAttribute('content', fromTokens || this.config.colors[themeKey] || '#0f1114');
    },

    /** Names each toggle for the theme it switches to; the icon follows data-theme in CSS. */
    labelToggles() {
        const label = this.isLight() ? 'Switch to the dark theme' : 'Switch to the light theme';
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            button.setAttribute('aria-label', label);
            button.setAttribute('title', label);
        });
    },

    setCookie(themeKey) {
        document.cookie = `${this.COOKIE_NAME}=${encodeURIComponent(themeKey)}; path=/; max-age=${this.COOKIE_MAX_AGE}; SameSite=Lax`;
    },

    getCookie() {
        const match = document.cookie.match(new RegExp(`(?:^|; )${this.COOKIE_NAME}=([^;]*)`));
        return match ? decodeURIComponent(match[1]) : null;
    },

    /**
     * Saves the theme for the signed-in user. The cookie already carries the choice, so a
     * failure here only means other devices won't see it; it is not worth a toast.
     */
    async persistToServer(themeKey) {
        try {
            if (window.ApiClient) {
                await window.ApiClient.put('/api/theme/preference', { themeKey });
            }
        } catch (e) {
            console.warn('ThemeManager: could not save the theme to your account', e);
        }
    },

    async clearServerPreference() {
        try {
            if (window.ApiClient) {
                await window.ApiClient.del('/api/theme/preference');
            }
        } catch (e) {
            console.warn('ThemeManager: could not clear the saved theme', e);
        }
    },

    init() {
        this.syncThemeColor(this.getActiveTheme());
        this.labelToggles();

        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            button.addEventListener('click', () => {
                this.toggle(button.getAttribute('data-theme-persist') === 'true');
            });
        });

        // With no saved choice, follow the OS when it switches (e.g. at sunset)
        if (window.matchMedia) {
            const light = window.matchMedia('(prefers-color-scheme: light)');
            const follow = () => {
                if (this.isSaved()) return;
                const system = this.getSystemTheme();
                if (system) this.render(system, false);
            };
            if (light.addEventListener) light.addEventListener('change', follow);
            else if (light.addListener) light.addListener(follow);
        }

        // Another tab saved or cleared a choice
        window.addEventListener('storage', (e) => {
            if (e.key !== this.STORAGE_KEY) return;
            if (e.newValue) this.render(e.newValue, true);
            else this.render(this.getSystemTheme() || this.config.dark, false);
        });
    }
};

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ThemeManager.init());
} else {
    ThemeManager.init();
}

window.ThemeManager = ThemeManager;
