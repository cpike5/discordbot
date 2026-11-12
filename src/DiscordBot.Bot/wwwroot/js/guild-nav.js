// Guild navigation
//  - Mobile (< sm): the dropdown that replaces the tab strip.
//  - Desktop (>= sm): "priority-plus" overflow. Every tab that fits stays in the strip and the rest
//    move into a "More" menu, so no tab is ever scrolled off screen. The server-rendered tab list
//    stays the source of truth: overflowed tabs are only hidden, and the menu holds copies of them.
//    Without JavaScript the strip simply scrolls, as it did before.
(function () {
    'use strict';

    /**
     * Decide which tabs stay in the strip. Pure, so it can be tested without a layout engine.
     * @param {object} o
     * @param {number[]} o.widths  Width of each tab, in config order.
     * @param {number} o.available Width the tabs may use.
     * @param {number} o.moreWidth Width the More button needs when it is shown.
     * @param {number} o.gap       Space between neighbouring items.
     * @param {number} o.activeIndex Index of the current page's tab, or -1.
     * @returns {{visible: boolean[], overflow: boolean, activeInOverflow: boolean}}
     */
    function computeLayout(o) {
        const widths = o.widths;
        const gap = o.gap || 0;
        const n = widths.length;
        const used = (flags) => {
            let total = 0;
            let count = 0;
            for (let i = 0; i < n; i++) {
                if (flags[i]) { total += widths[i] + (count > 0 ? gap : 0); count++; }
            }
            return total;
        };

        const everything = new Array(n).fill(true);
        if (used(everything) <= o.available) {
            return { visible: everything, overflow: false, activeInOverflow: false };
        }

        // More is shown: leave room for it and for the gap in front of it.
        const budget = o.available - o.moreWidth - gap;
        const visible = new Array(n).fill(false);
        let total = 0;
        for (let i = 0; i < n; i++) {
            const next = total + (i > 0 ? gap : 0) + widths[i];
            if (next > budget) break;
            visible[i] = true;
            total = next;
        }

        const active = o.activeIndex;
        let activeInOverflow = false;
        if (active >= 0 && !visible[active]) {
            // Make room for the active tab by dropping the last visible ones.
            let last = visible.lastIndexOf(true);
            while (last >= 0 && used(visible) + gap + widths[active] > budget) {
                visible[last] = false;
                last = visible.lastIndexOf(true);
            }
            if (used(visible) + (last >= 0 ? gap : 0) + widths[active] <= budget) {
                visible[active] = true;
            } else {
                activeInOverflow = true;
            }
        }
        return { visible: visible, overflow: true, activeInOverflow: activeInOverflow };
    }

    /**
     * Open/close and keyboard behaviour of the More menu (menu button pattern).
     * Written against a small interface so tests can drive it with stubs.
     * @param {object} o
     * @param {object} o.button     Needs setAttribute, focus.
     * @param {object} o.menu       Needs classList (add/remove/contains).
     * @param {function} o.getItems Returns the focusable menu items, in order.
     * @param {function} [o.getActiveItem] Returns the item for the current page, if any.
     */
    function createMoreMenu(o) {
        const button = o.button;
        const menu = o.menu;

        function isOpen() { return menu.classList.contains('active'); }

        function focusItem(index) {
            const items = o.getItems();
            if (items.length === 0) return;
            items[(index + items.length) % items.length].focus();
        }

        function open(which) {
            menu.classList.add('active');
            button.setAttribute('aria-expanded', 'true');
            const items = o.getItems();
            if (items.length === 0) return;
            if (which === 'last') focusItem(items.length - 1);
            else if (which === 'first') focusItem(0);
            else {
                const active = o.getActiveItem ? o.getActiveItem() : null;
                const at = active ? items.indexOf(active) : -1;
                focusItem(at >= 0 ? at : 0);
            }
        }

        function close(restoreFocus) {
            if (!isOpen()) return;
            menu.classList.remove('active');
            button.setAttribute('aria-expanded', 'false');
            if (restoreFocus) button.focus();
        }

        function toggle() { if (isOpen()) close(false); else open(); }

        function onButtonKeydown(e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); open('first'); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); open('last'); }
            else if (e.key === 'Escape' && isOpen()) { e.preventDefault(); close(true); }
        }

        function onMenuKeydown(e) {
            const items = o.getItems();
            const current = items.indexOf(e.target);
            switch (e.key) {
                case 'ArrowDown': e.preventDefault(); focusItem(current + 1); break;
                case 'ArrowUp': e.preventDefault(); focusItem(current - 1); break;
                case 'Home': e.preventDefault(); focusItem(0); break;
                case 'End': e.preventDefault(); focusItem(items.length - 1); break;
                case 'Escape': e.preventDefault(); close(true); break;
                case 'Tab': close(false); break;
            }
        }

        return { isOpen, open, close, toggle, onButtonKeydown, onMenuKeydown };
    }

    const api = { computeLayout: computeLayout, createMoreMenu: createMoreMenu };

    // Node (tests): export the pure parts and stop; there is no document to initialise
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
        return;
    }
    window.GuildNav = api;

    function initMobileDropdown() {
        const dropdownToggle = document.getElementById('guildNavDropdownToggle');
        const dropdownMenu = document.getElementById('guildNavDropdownMenu');
        if (!dropdownToggle || !dropdownMenu) return;

        dropdownToggle.addEventListener('click', function (e) {
            e.stopPropagation();
            const isHidden = dropdownMenu.classList.toggle('hidden');
            dropdownToggle.setAttribute('aria-expanded', !isHidden);
        });

        document.addEventListener('click', function (e) {
            if (!e.target.closest('.guild-nav-dropdown')) {
                dropdownMenu.classList.add('hidden');
                dropdownToggle.setAttribute('aria-expanded', 'false');
            }
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !dropdownMenu.classList.contains('hidden')) {
                dropdownMenu.classList.add('hidden');
                dropdownToggle.setAttribute('aria-expanded', 'false');
                dropdownToggle.focus();
            }
        });
    }

    function initOverflow() {
        const root = document.querySelector('[data-guild-nav]');
        if (!root) return;
        const strip = root.querySelector('.tab-panel-tabs');
        const wrapper = root.querySelector('[data-guild-nav-more]');
        const button = wrapper && wrapper.querySelector('.guild-nav-more-button');
        const menuEl = wrapper && wrapper.querySelector('.guild-nav-more-menu');
        const labelEl = wrapper && wrapper.querySelector('.guild-nav-more-label');
        if (!strip || !wrapper || !button || !menuEl || !labelEl) return;

        const tabs = Array.from(strip.querySelectorAll('.tab-panel-tab'));
        if (tabs.length === 0) return;
        const activeIndex = tabs.findIndex((t) => t.classList.contains('active'));

        root.classList.add('guild-nav-enhanced');

        // Widths are measured with every tab showing, then cached.
        let widths = null;
        const moreWidths = {};

        function measureTabs() {
            tabs.forEach((t) => { t.hidden = false; });
            widths = tabs.map((t) => t.getBoundingClientRect().width);
        }

        function measureMore(label) {
            labelEl.textContent = label;
            if (!(label in moreWidths)) moreWidths[label] = button.getBoundingClientRect().width;
            return moreWidths[label];
        }

        function availableWidth() {
            const style = window.getComputedStyle(strip);
            const padding = (parseFloat(style.paddingLeft) || 0) + (parseFloat(style.paddingRight) || 0);
            return strip.clientWidth - padding;
        }

        function items() { return Array.from(menuEl.querySelectorAll('[role="menuitem"]')); }

        const menu = createMoreMenu({
            button: button,
            menu: menuEl,
            getItems: items,
            getActiveItem: () => menuEl.querySelector('[aria-current="page"]')
        });

        // Menu items are clones of the server-rendered links (DOM cloning, no HTML strings)
        function buildMenuItem(tab) {
            const li = document.createElement('li');
            li.setAttribute('role', 'none');
            const link = tab.cloneNode(true);
            ['id', 'hidden', 'aria-selected', 'data-tab-id'].forEach((a) => link.removeAttribute(a));
            link.setAttribute('role', 'menuitem');
            link.setAttribute('tabindex', '-1');
            const isActive = tab.classList.contains('active');
            link.className = 'guild-nav-more-item' + (isActive ? ' active' : '');
            if (isActive) link.setAttribute('aria-current', 'page');
            li.appendChild(link);
            return li;
        }

        function labelOf(tab) {
            const span = tab.querySelector('span');
            return (span ? span.textContent : tab.textContent).trim();
        }

        function layout() {
            if (strip.clientWidth === 0) return; // desktop strip is not displayed (mobile)
            wrapper.hidden = false; // measurable; hidden again below when nothing overflows
            if (!widths) measureTabs();
            tabs.forEach((t) => { t.hidden = false; });

            const input = {
                widths: widths,
                available: availableWidth(),
                gap: parseFloat(window.getComputedStyle(strip).columnGap) || 0,
                activeIndex: activeIndex
            };

            let label = 'More';
            let result = computeLayout(Object.assign({ moreWidth: measureMore(label) }, input));
            if (result.overflow && result.activeInOverflow) {
                // "More: Welcome" is wider than "More": fit the strip around the real width
                label = 'More: ' + labelOf(tabs[activeIndex]);
                result = computeLayout(Object.assign({ moreWidth: measureMore(label) }, input));
            }

            if (!result.overflow) {
                wrapper.hidden = true;
                menu.close(false);
                return;
            }

            labelEl.textContent = label;
            button.classList.toggle('active', result.activeInOverflow);
            while (menuEl.firstChild) menuEl.removeChild(menuEl.firstChild);
            tabs.forEach((tab, i) => {
                tab.hidden = !result.visible[i];
                if (!result.visible[i]) menuEl.appendChild(buildMenuItem(tab));
            });
        }

        button.addEventListener('click', function (e) { e.stopPropagation(); menu.toggle(); });
        button.addEventListener('keydown', menu.onButtonKeydown);
        menuEl.addEventListener('keydown', menu.onMenuKeydown);
        menuEl.addEventListener('click', function () { menu.close(false); });
        document.addEventListener('click', function (e) {
            if (!wrapper.contains(e.target)) menu.close(false);
        });
        // Keyboard focus leaving the menu (Shift+Tab, a click elsewhere) closes it
        wrapper.addEventListener('focusout', function (e) {
            if (menu.isOpen() && e.relatedTarget && !wrapper.contains(e.relatedTarget)) menu.close(false);
        });

        let pending = false;
        function schedule() {
            if (pending) return;
            pending = true;
            window.requestAnimationFrame(function () { pending = false; layout(); });
        }

        layout();
        if (typeof ResizeObserver === 'function') new ResizeObserver(schedule).observe(root);
        else window.addEventListener('resize', schedule);
        if (document.fonts && document.fonts.ready) {
            // Web fonts change tab widths: measure again once they have loaded
            document.fonts.ready.then(function () {
                widths = null;
                Object.keys(moreWidths).forEach((k) => { delete moreWidths[k]; });
                schedule();
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        initMobileDropdown();
        initOverflow();
    });
})();
