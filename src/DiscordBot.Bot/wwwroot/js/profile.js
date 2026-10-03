/**
 * Profile page (Pages/Account/Profile.cshtml).
 *
 * - "Match my system": the server forgets the saved theme (and its cookie), but the choice is also
 *   kept in this browser's localStorage, which other open tabs follow. After the redirect the page
 *   renders `[data-theme-cleared]` once, and this script calls ThemeManager.clearTheme(false) so the
 *   stored choice is removed and the other tabs go back to following the system too.
 *
 * Exposed as window.ProfilePage (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.ProfilePage = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', function () { root.ProfilePage.init(); });
            } else {
                root.ProfilePage.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    /**
     * Clears the browser's saved theme when the page says the user just chose "Match my system".
     * @param {Document} [doc] The document to look in (defaults to the page's).
     * @param {Object} [themeManager] ThemeManager (defaults to the global one from theme.js).
     * @returns {boolean} True when the saved theme was cleared.
     */
    function init(doc, themeManager) {
        doc = doc || (typeof document !== 'undefined' ? document : null);
        // ThemeManager is a top-level const of theme.js: a global binding, not a property of window
        themeManager = themeManager || (typeof ThemeManager !== 'undefined' ? ThemeManager : null);
        if (!doc || !themeManager || typeof themeManager.clearTheme !== 'function') return false;
        if (!doc.querySelector('[data-theme-cleared]')) return false;

        themeManager.clearTheme(false);
        return true;
    }

    return { init: init };
});
