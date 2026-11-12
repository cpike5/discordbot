/**
 * Account menu of the member portal header (Pages/Portal/Shared/_PortalUserMenu.cshtml).
 *
 * The button toggles the menu (.active on the dropdown, aria-expanded on the button). Escape,
 * a click outside, or focus leaving the menu closes it; Escape returns focus to the button.
 * ArrowDown / ArrowUp / Home / End move between the items, and ArrowDown on the closed button
 * opens the menu on its first item.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.PortalUserMenu = api;
        const start = function () { api.init(document); };
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', start);
        } else {
            start();
        }
    }
})(typeof window !== 'undefined' ? window : this, function () {
    'use strict';

    const ITEM_SELECTOR = '[role="menuitem"]';

    /**
     * Wires one menu container: [data-portal-user-menu] holding a button and the dropdown.
     * @param {Element} container The menu wrapper.
     * @param {Document} doc The document, for outside clicks.
     * @returns {{open: Function, close: Function, isOpen: Function}|null}
     */
    function wire(container, doc) {
        const button = container.querySelector('#portalUserMenuButton');
        const menu = container.querySelector('#portalUserMenu');
        if (!button || !menu) return null;

        function items() {
            return Array.prototype.slice.call(menu.querySelectorAll(ITEM_SELECTOR));
        }

        function isOpen() {
            return menu.classList.contains('active');
        }

        function open(focusIndex) {
            menu.classList.add('active');
            button.setAttribute('aria-expanded', 'true');
            if (typeof focusIndex === 'number') {
                const list = items();
                const target = focusIndex < 0 ? list[list.length - 1] : list[focusIndex];
                if (target) target.focus();
            }
        }

        function close(returnFocus) {
            menu.classList.remove('active');
            button.setAttribute('aria-expanded', 'false');
            if (returnFocus) button.focus();
        }

        function move(step, from) {
            const list = items();
            if (list.length === 0) return;
            const current = list.indexOf(from);
            const next = current === -1
                ? (step > 0 ? 0 : list.length - 1)
                : (current + step + list.length) % list.length;
            list[next].focus();
        }

        button.addEventListener('click', function () {
            if (isOpen()) close(false); else open();
        });

        button.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                open(0);
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                open(-1);
            } else if (event.key === 'Escape' && isOpen()) {
                close(true);
            }
        });

        menu.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                event.preventDefault();
                close(true);
            } else if (event.key === 'ArrowDown') {
                event.preventDefault();
                move(1, event.target);
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                move(-1, event.target);
            } else if (event.key === 'Home') {
                event.preventDefault();
                open(0);
            } else if (event.key === 'End') {
                event.preventDefault();
                open(-1);
            }
        });

        // Tabbing past the last item (or back past the button) closes it
        container.addEventListener('focusout', function (event) {
            const next = event.relatedTarget;
            if (isOpen() && next && !container.contains(next)) close(false);
        });

        doc.addEventListener('click', function (event) {
            if (isOpen() && !container.contains(event.target)) close(false);
        });

        return { open: open, close: close, isOpen: isOpen };
    }

    /**
     * Wires every portal user menu on the page.
     * @param {Document} doc The document.
     */
    function init(doc) {
        const wired = [];
        doc.querySelectorAll('[data-portal-user-menu]').forEach(function (container) {
            const instance = wire(container, doc);
            if (instance) wired.push(instance);
        });
        return wired;
    }

    return { wire: wire, init: init };
});
