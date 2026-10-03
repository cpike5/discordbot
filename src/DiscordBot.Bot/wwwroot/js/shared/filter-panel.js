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

    function init() {
        const doc = root.document;
        initPanel();
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

    return { toggleFilterPanel, markPresets, applyPresetButton };
});
