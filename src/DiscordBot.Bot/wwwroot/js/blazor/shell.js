/**
 * MainLayout shell behaviour: sidebar collapse/mobile-drawer, user-menu toggle, mobile search
 * overlay open/close, and the Ctrl/Cmd+K search shortcut. Ports the relevant pieces of
 * wwwroot/js/navigation.js and wwwroot/js/search.js for the static-SSR Blazor shell
 * (Blazor/Layout/MainLayout.razor and friends) - see "Blazor Components" in
 * docs/architecture/patterns.md.
 *
 * Classic script (not an ES module - loaded with a plain <script src> from App.razor, after
 * blazor.web.js), because MainLayout never goes @rendermode interactive itself and so has no
 * IJSRuntime to import a module through; every interaction here has to be plain DOM script that
 * runs regardless of whether any circuit on the page is connected yet.
 *
 * Every handler is attached once via event delegation on `document` (data-shell-action="...", no
 * inline onclick= - Phase 6 adds a CSP) so it survives Blazor's enhanced navigation, which patches
 * the DOM in place rather than reloading the page and would otherwise leave a freshly-swapped
 * sidebar/navbar with no listeners of its own.
 */
(function () {
    if (window.__discordBotShellInitialized) {
        return;
    }
    window.__discordBotShellInitialized = true;

    var SIDEBAR_COLLAPSED_KEY = 'sidebarCollapsed';

    var sidebarOpen = false;
    var sidebarCollapsed = false;

    // ---------------------------------------------------------------------
    // Mobile sidebar drawer
    // ---------------------------------------------------------------------

    function setMobileSidebarOpen(open) {
        var sidebar = document.getElementById('sidebar');
        var overlay = document.getElementById('mobileOverlay');
        var toggleButton = document.getElementById('sidebarToggle');
        if (!sidebar || !overlay) {
            return;
        }

        sidebarOpen = open;

        if (sidebarOpen) {
            sidebar.classList.remove('-translate-x-full');
            overlay.classList.add('active');
        } else {
            sidebar.classList.add('-translate-x-full');
            overlay.classList.remove('active');
        }

        if (toggleButton) {
            toggleButton.setAttribute('aria-expanded', sidebarOpen.toString());
        }
    }

    function toggleMobileSidebar() {
        setMobileSidebarOpen(!sidebarOpen);
    }

    // ---------------------------------------------------------------------
    // Desktop sidebar collapse (persisted; App.razor's inline FOUC-guard
    // script applies the same localStorage key before first paint)
    // ---------------------------------------------------------------------

    function applySidebarCollapsed(collapsed) {
        sidebarCollapsed = collapsed;

        var sidebar = document.getElementById('sidebar');
        if (sidebar) {
            sidebar.classList.toggle('collapsed', collapsed);
        }
        document.documentElement.classList.toggle('sidebar-collapsed', collapsed);

        var collapseIcon = document.getElementById('sidebarCollapseIcon');
        if (collapseIcon) {
            collapseIcon.style.transform = collapsed ? 'rotate(180deg)' : '';
        }
    }

    function toggleSidebarCollapse() {
        applySidebarCollapsed(!sidebarCollapsed);
        try {
            window.localStorage.setItem(SIDEBAR_COLLAPSED_KEY, sidebarCollapsed.toString());
        } catch (e) {
            // Storage unavailable (private mode, quota) - collapse still applied this pageview.
        }
    }

    function restoreSidebarCollapsed() {
        try {
            applySidebarCollapsed(
                window.localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true' && window.innerWidth >= 1024);
        } catch (e) {
            // Storage unavailable - leave expanded (App.razor's own FOUC guard already handled the
            // pre-paint <html> class from the same key; this only syncs the icon/sidebar classes).
        }
    }

    // ---------------------------------------------------------------------
    // User menu dropdown
    // ---------------------------------------------------------------------

    function setUserMenuOpen(open) {
        var menu = document.getElementById('userMenu');
        var button = document.getElementById('userMenuButton');
        if (!menu) {
            return;
        }
        menu.classList.toggle('active', open);
        if (button) {
            button.setAttribute('aria-expanded', open.toString());
        }
    }

    function toggleUserMenu() {
        var menu = document.getElementById('userMenu');
        setUserMenuOpen(!(menu && menu.classList.contains('active')));
    }

    function closeUserMenu() {
        setUserMenuOpen(false);
    }

    // ---------------------------------------------------------------------
    // Mobile search overlay (Phase 3 deviation: plain GET /Search form, no
    // live recent/results panes - see MobileSearchOverlay.razor)
    // ---------------------------------------------------------------------

    function setMobileSearchOpen(open) {
        var overlay = document.getElementById('mobileSearchOverlay');
        if (!overlay) {
            return;
        }
        overlay.classList.toggle('active', open);
        if (open) {
            var input = document.getElementById('mobile-search-input');
            if (input) {
                input.focus();
            }
        }
    }

    function toggleMobileSearch() {
        var overlay = document.getElementById('mobileSearchOverlay');
        setMobileSearchOpen(!(overlay && overlay.classList.contains('active')));
    }

    function closeMobileSearch() {
        setMobileSearchOpen(false);
    }

    // ---------------------------------------------------------------------
    // App.razor's #blazor-error-ui dismiss button
    // ---------------------------------------------------------------------

    function dismissErrorUi() {
        var errorUi = document.getElementById('blazor-error-ui');
        if (errorUi) {
            errorUi.style.display = 'none';
        }
    }

    // ---------------------------------------------------------------------
    // Delegated action dispatch
    // ---------------------------------------------------------------------

    var ACTIONS = {
        'toggle-mobile-sidebar': toggleMobileSidebar,
        'toggle-sidebar-collapse': toggleSidebarCollapse,
        'toggle-user-menu': toggleUserMenu,
        'toggle-mobile-search': toggleMobileSearch,
        'close-mobile-search': closeMobileSearch,
        'dismiss-error-ui': dismissErrorUi
    };

    document.addEventListener('click', function (event) {
        var target = event.target.closest ? event.target.closest('[data-shell-action]') : null;
        var action = target && ACTIONS[target.getAttribute('data-shell-action')];
        if (action) {
            event.preventDefault();
            action();
            return;
        }

        // Click-outside-closes for the user menu: any click that lands neither on its toggle
        // button nor inside the open menu itself closes it (mirrors navigation.js's document
        // click handler). The toggle button's own click already returned above, so this only
        // ever runs for a click somewhere else entirely.
        var withinButton = event.target.closest && event.target.closest('#userMenuButton');
        var withinMenu = event.target.closest && event.target.closest('#userMenu');
        if (!withinButton && !withinMenu) {
            closeUserMenu();
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeUserMenu();
            closeMobileSearch();
            return;
        }

        // Ctrl/Cmd+K focuses the desktop search box, same shortcut search.js binds today.
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
            var searchInput = document.getElementById('navbar-search');
            if (searchInput) {
                event.preventDefault();
                searchInput.focus();
            }
        }
    });

    function showPlatformKbdHint() {
        var isMac = /Mac|iPod|iPhone|iPad/.test(window.navigator.platform || '');
        var kbdMac = document.getElementById('kbd-mac');
        var kbdOther = document.getElementById('kbd-other');
        if (kbdMac) {
            kbdMac.classList.toggle('hidden', !isMac);
        }
        if (kbdOther) {
            kbdOther.classList.toggle('hidden', isMac);
        }
    }

    function init() {
        restoreSidebarCollapsed();
        showPlatformKbdHint();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Blazor's enhanced navigation patches the DOM (new sidebar/navbar markup, new
    // sidebarCollapseIcon element) without a full reload or re-running this script - resync the
    // icon rotation/collapsed classes and the platform kbd hint onto whatever just got swapped in.
    // The delegated listeners above need no re-registration since `document` itself never changes.
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', function () {
            applySidebarCollapsed(sidebarCollapsed);
            showPlatformKbdHint();
        });
    }
})();
