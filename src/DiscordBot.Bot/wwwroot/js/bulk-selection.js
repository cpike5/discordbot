/**
 * Bulk selection for lists that render the same rows twice (a table from `md` up and cards
 * below it). Both layouts stay in the DOM and only one is visible, so counting checked boxes
 * double-counts every row. This module keeps one selection keyed by the row's id and mirrors it
 * onto every checkbox that carries that id.
 *
 * Markup (all attributes, no handler text):
 *
 *   <input type="checkbox" data-select-item="<id>" aria-label="Select …">   one per row per layout
 *   <input type="checkbox" data-select-all aria-label="Select all …">       any number
 *   <div data-bulk-toolbar hidden class="hidden …"> … <span data-selected-count></span> …
 *       <button data-bulk-clear>…</button> </div>
 *   <p class="sr-only" role="status" data-selection-status></p>             polite announcement
 *
 *   BulkSelection.init({ noun: ['member', 'members'], onChange(ids) { … } })
 *
 * The id is the row's identity (a Discord snowflake or a GUID), always handled as a string.
 *
 * Exposed as window.BulkSelection (browser) and module.exports (Node/tests). `createSelection`
 * has no DOM dependency.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.BulkSelection = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /**
     * A set of selected ids. Setting the same id twice (once per layout) counts once.
     * @param {Iterable<string>} [available] - every id the page offers, for select-all
     */
    function createSelection(available) {
        var ids = new Set();
        var all = new Set(Array.from(available || [], String));

        return {
            /** Replace the ids the page offers (after rows are added or removed). */
            setAvailable: function (next) {
                all = new Set(Array.from(next || [], String));
                ids.forEach(function (id) { if (!all.has(id)) ids.delete(id); });
            },
            set: function (id, selected) {
                id = String(id);
                if (selected) ids.add(id); else ids.delete(id);
            },
            has: function (id) { return ids.has(String(id)); },
            selectAll: function () { all.forEach(function (id) { ids.add(id); }); },
            clear: function () { ids.clear(); },
            toArray: function () { return Array.from(ids); },
            get size() { return ids.size; },
            get total() { return all.size; },
            /** 'none', 'some' or 'all' - the state select-all shows. */
            get state() {
                if (ids.size === 0) return 'none';
                return all.size > 0 && ids.size >= all.size ? 'all' : 'some';
            }
        };
    }

    function describe(count, noun) {
        if (typeof Format !== 'undefined' && Format.plural) {
            return Format.plural(count, noun[0], noun[1]);
        }
        return count + ' ' + (count === 1 ? noun[0] : noun[1]);
    }

    /**
     * Wire the selection to the checkboxes, toolbar and status line under `options.root`.
     * @param {Object} [options]
     * @param {ParentNode} [options.root=document]
     * @param {string[]} [options.noun=['item','items']] - singular and plural for the count
     * @param {function(string[]): void} [options.onChange]
     * @returns {{ ids(): string[], count(): number, clear(): void, refresh(): void }}
     */
    function init(options) {
        options = options || {};
        var scope = options.root || document;
        var noun = options.noun || ['item', 'items'];

        var toolbar = scope.querySelector('[data-bulk-toolbar]');
        var counts = scope.querySelectorAll('[data-selected-count]');
        var status = scope.querySelector('[data-selection-status]');
        var selection = createSelection();

        function items() { return scope.querySelectorAll('input[data-select-item]'); }
        function selectAlls() { return scope.querySelectorAll('input[data-select-all]'); }

        function collectAvailable() {
            var found = [];
            items().forEach(function (cb) { found.push(cb.dataset.selectItem); });
            selection.setAvailable(found);
        }

        function render(announce) {
            items().forEach(function (cb) { cb.checked = selection.has(cb.dataset.selectItem); });

            var state = selection.state;
            selectAlls().forEach(function (cb) {
                cb.checked = state === 'all';
                cb.indeterminate = state === 'some';
            });

            var text = describe(selection.size, noun);
            counts.forEach(function (el) { el.textContent = text; });
            if (toolbar) {
                // Both: a utility class such as `flex` outranks the hidden attribute
                toolbar.hidden = selection.size === 0;
                toolbar.classList.toggle('hidden', selection.size === 0);
            }
            if (status && announce) {
                status.textContent = selection.size === 0
                    ? 'Selection cleared'
                    : text + ' selected';
            }

            if (typeof options.onChange === 'function') options.onChange(selection.toArray());
        }

        scope.addEventListener('change', function (e) {
            var target = e.target;
            if (!target || !target.matches) return;

            if (target.matches('input[data-select-item]')) {
                selection.set(target.dataset.selectItem, target.checked);
                render(true);
            } else if (target.matches('input[data-select-all]')) {
                if (target.checked) selection.selectAll(); else selection.clear();
                render(true);
            }
        });

        scope.addEventListener('click', function (e) {
            var clear = e.target.closest && e.target.closest('[data-bulk-clear]');
            if (clear) {
                selection.clear();
                render(true);
            }
        });

        collectAvailable();
        // A page restored from the back/forward cache can bring checkboxes back checked
        items().forEach(function (cb) {
            if (cb.checked) selection.set(cb.dataset.selectItem, true);
        });
        render();

        return {
            ids: function () { return selection.toArray(); },
            count: function () { return selection.size; },
            clear: function () { selection.clear(); render(true); },
            refresh: function () { collectAvailable(); render(); }
        };
    }

    return { createSelection: createSelection, init: init };
});
