/**
 * Shared filter panel behaviour for the analytics and Rat Watch pages.
 *
 * - toggleFilterPanel(): the collapse button the <filter-panel> tag helper writes calls this. The
 *   content grows to its own height (no fixed max-height clipping a tall panel on a phone) and is
 *   `inert` while collapsed, so hidden fields are not Tab stops.
 * - Date presets: a button with data-date-preset (partial _DateRangeFilter) fills the form's
 *   [data-date-start] and [data-date-end] inputs from DateRangeFilter.presetRange (the viewer's
 *   local calendar, not UTC), then submits the form. The button matching the inputs is marked
 *   aria-pressed, on load and whenever the dates change. Needs date-range-filter.js.
 *
 * - Live search: an input with data-filter-live (partial _FilterSearch) waits 300ms after the last
 *   keystroke, then reloads the page with its value in the query string (paging reset), and puts
 *   the caret back after the reload. A page that filters in script instead listens for the
 *   cancelable `filter-live` event on the input (detail: { name, value }) and calls
 *   preventDefault(); nothing navigates then.
 * - Chips: chipsHtml(chips) writes the same removable-chip markup as the _ActiveFilterChips
 *   partial for pages that draw their filters in script. Every label goes through SafeHtml.escape.
 *
 * Exposed as window.FilterPanel (and module.exports for tests).
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.FilterPanel = api;
        window.toggleFilterPanel = api.toggleFilterPanel;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    function dateRangeFilter() {
        return root.DateRangeFilter || null;
    }

    /** The start and end inputs a preset button belongs to, or null. */
    function inputsFor(button) {
        const form = button.closest ? button.closest('form') : null;
        if (!form) return null;
        const start = form.querySelector('[data-date-start]');
        const end = form.querySelector('[data-date-end]');
        return start && end ? { form, start, end } : null;
    }

    /**
     * Marks the preset buttons in `scope` that match the date inputs (aria-pressed) and clears
     * the rest. A custom range marks none.
     */
    function markPresets(scope) {
        const drf = dateRangeFilter();
        if (!drf || !scope) return;
        scope.querySelectorAll('[data-date-range-presets]').forEach(function (group) {
            const buttons = group.querySelectorAll('[data-date-preset]');
            if (buttons.length === 0) return;
            const inputs = inputsFor(buttons[0]);
            if (!inputs) return;
            const active = drf.detectPreset(inputs.start.value, inputs.end.value);
            buttons.forEach(function (button) {
                button.setAttribute('aria-pressed', button.getAttribute('data-date-preset') === active ? 'true' : 'false');
            });
        });
    }

    /**
     * Applies the clicked preset to its form's date inputs and submits the form.
     * @returns {boolean} false when the preset or the inputs are unknown
     */
    function applyPresetButton(button) {
        const drf = dateRangeFilter();
        const inputs = inputsFor(button);
        if (!drf || !inputs) return false;
        if (!drf.applyPreset(inputs.start, inputs.end, button.getAttribute('data-date-preset'))) return false;
        markPresets(inputs.form);
        if (typeof inputs.form.requestSubmit === 'function') {
            inputs.form.requestSubmit();
        } else {
            inputs.form.submit();
        }
        return true;
    }

    function reducedMotion() {
        return !!(root.matchMedia && root.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }

    function setExpanded(content, chevron, toggle, expanded) {
        toggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        if (chevron) {
            chevron.classList.toggle('-rotate-90', !expanded);
            chevron.classList.toggle('rotate-0', expanded);
        }
        content.classList.remove('max-h-screen', 'max-h-0');
        if (expanded) {
            content.removeAttribute('inert');
            content.style.maxHeight = content.scrollHeight + 'px';
            const settle = function () { content.style.maxHeight = 'none'; };
            if (reducedMotion()) settle();
            else content.addEventListener('transitionend', settle, { once: true });
        } else {
            content.setAttribute('inert', '');
            // From a fixed height, so the collapse animates
            content.style.maxHeight = content.scrollHeight + 'px';
            void content.offsetHeight;
            content.style.maxHeight = '0px';
        }
    }

    /** Collapses or expands the panel written by the <filter-panel> tag helper. */
    function toggleFilterPanel() {
        const doc = root.document;
        const content = doc.getElementById('filterContent');
        const chevron = doc.getElementById('filterChevron');
        const toggle = doc.getElementById('filterToggle');
        if (!content || !toggle) return;
        setExpanded(content, chevron, toggle, toggle.getAttribute('aria-expanded') !== 'true');
    }

    /** Initial state: an expanded panel is not height-capped, a collapsed one is inert. */
    function initPanel() {
        const doc = root.document;
        const content = doc.getElementById('filterContent');
        const toggle = doc.getElementById('filterToggle');
        if (!content || !toggle || toggle.getAttribute('onclick') !== 'toggleFilterPanel()') return;
        if (toggle.getAttribute('aria-expanded') === 'true') {
            content.classList.remove('max-h-screen');
            content.style.maxHeight = 'none';
        } else {
            content.setAttribute('inert', '');
        }
    }

    // ---- live search ---------------------------------------------------------------------------

    const LIVE_DELAY_MS = 300;
    const LIVE_FOCUS_KEY = 'filterPanel.liveFocus';
    const PAGE_KEY = 'pageNumber';

    /**
     * The URL for a live search: `href` with `name` set to `value` (or removed when empty) and
     * back on page 1. Other keys, repeated ones included, stay as they are.
     * @param {string} href path plus query, e.g. location.pathname + location.search
     */
    function liveUrl(href, name, value) {
        const hash = href.indexOf('#');
        const noHash = hash >= 0 ? href.slice(0, hash) : href;
        const q = noHash.indexOf('?');
        const path = q >= 0 ? noHash.slice(0, q) : noHash;
        const params = new URLSearchParams(q >= 0 ? noHash.slice(q + 1) : '');
        params.delete(PAGE_KEY);
        params.delete(name);
        const text = String(value || '').trim();
        if (text) params.set(name, text);
        const query = params.toString();
        return query ? path + '?' + query : path;
    }

    function readLocation() {
        const loc = root.location;
        return loc.pathname + loc.search;
    }

    function rememberFocus(input) {
        try {
            root.sessionStorage.setItem(LIVE_FOCUS_KEY, JSON.stringify({
                id: input.id,
                caret: typeof input.selectionStart === 'number' ? input.selectionStart : String(input.value || '').length
            }));
        } catch (e) { /* storage blocked: the search still works, the caret is not restored */ }
    }

    /** After a live-search reload: focus the box again with the caret where it was. */
    function restoreFocus(doc) {
        let saved = null;
        try {
            saved = JSON.parse(root.sessionStorage.getItem(LIVE_FOCUS_KEY) || 'null');
            root.sessionStorage.removeItem(LIVE_FOCUS_KEY);
        } catch (e) { return false; }
        if (!saved || !saved.id) return false;
        const input = doc.getElementById(saved.id);
        if (!input || !input.hasAttribute || !input.hasAttribute('data-filter-live')) return false;
        input.focus();
        if (typeof input.setSelectionRange === 'function') {
            const at = Math.min(saved.caret, String(input.value || '').length);
            input.setSelectionRange(at, at);
        }
        return true;
    }

    /**
     * Wires one data-filter-live input. Returns { commit, cancel } so tests drive it directly.
     * options: delay, navigate(url), timers { setTimeout, clearTimeout }, href() for the current URL.
     */
    function attachLive(input, options) {
        const opts = options || {};
        const delay = opts.delay === undefined ? LIVE_DELAY_MS : opts.delay;
        const timers = opts.timers || { setTimeout: root.setTimeout.bind(root), clearTimeout: root.clearTimeout.bind(root) };
        const navigate = opts.navigate || function (url) { root.location.assign(url); };
        const href = opts.href || readLocation;
        const name = input.getAttribute('name') || 'SearchTerm';
        let committed = String(input.value || '').trim();
        let timer = null;

        function commit() {
            if (timer !== null) { timers.clearTimeout(timer); timer = null; }
            const value = String(input.value || '').trim();
            if (value === committed) return false;
            committed = value;
            if (typeof input.dispatchEvent === 'function' && typeof root.CustomEvent === 'function') {
                const event = new root.CustomEvent('filter-live', { bubbles: true, cancelable: true, detail: { name, value } });
                if (!input.dispatchEvent(event)) return true; // the page handles it
            }
            rememberFocus(input);
            navigate(liveUrl(href(), name, value));
            return true;
        }

        function schedule() {
            if (timer !== null) timers.clearTimeout(timer);
            timer = timers.setTimeout(commit, delay);
        }

        input.addEventListener('input', schedule);
        input.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                commit();
            }
        });
        // The clear (x) button of a type=search box fires `search` without a keystroke
        input.addEventListener('search', commit);
        return { commit, cancel: function () { if (timer !== null) { timers.clearTimeout(timer); timer = null; } } };
    }

    // ---- chips ----------------------------------------------------------------------------------

    function escapeHtml(value) {
        if (root.SafeHtml && typeof root.SafeHtml.escape === 'function') return root.SafeHtml.escape(value);
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    const CHIP_X = '<svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">' +
        '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" /></svg>';

    /**
     * Chip markup for filters a page draws in script; the same shape as _ActiveFilterChips except
     * that the remove control is a button carrying data-chip-key (the page removes that filter).
     * @param {{label: string, key: string}[]} chips
     * @param {{label?: string, clearAll?: boolean}} [options]
     */
    function chipsHtml(chips, options) {
        const opts = options || {};
        if (!chips || chips.length === 0) return '';
        const items = chips.map(function (chip) {
            return '<span class="badge badge-gray badge-pill badge-case" data-filter-chip>' + escapeHtml(chip.label) +
                '<button type="button" class="badge-remove" data-chip-key="' + escapeHtml(chip.key) + '"' +
                ' aria-label="' + escapeHtml('Remove filter: ' + chip.label) + '" title="Remove filter">' + CHIP_X + '</button></span>';
        }).join('');
        const clearAll = chips.length > 1 && opts.clearAll !== false
            ? '<button type="button" class="text-sm text-accent-blue hover:underline" data-chip-clear-all>Clear all</button>'
            : '';
        return '<div class="flex flex-wrap items-center gap-2 mb-4" role="group" aria-label="' +
            escapeHtml(opts.label || 'Active filters') + '" data-active-filter-chips>' + items + clearAll + '</div>';
    }

    function init() {
        const doc = root.document;
        initPanel();
        doc.querySelectorAll('[data-filter-live]').forEach(function (input) { attachLive(input); });
        restoreFocus(doc);
        markPresets(doc);
        doc.addEventListener('click', function (event) {
            const button = event.target && event.target.closest ? event.target.closest('[data-date-preset]') : null;
            if (button) applyPresetButton(button);
        });
        doc.addEventListener('change', function (event) {
            const target = event.target;
            if (target && target.matches && target.matches('[data-date-start], [data-date-end]')) {
                const form = target.closest('form');
                if (form) markPresets(form);
            }
        });
    }

    if (root.document) {
        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    return { toggleFilterPanel, markPresets, applyPresetButton, liveUrl, attachLive, restoreFocus, chipsHtml };
});
