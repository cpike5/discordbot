/**
 * AJAX Sort Module
 * Re-renders a list in place when a sort dropdown changes, without leaving the page.
 *
 * Usage:
 *   1. Include this script in your page: <script src="~/js/ajax-sort.js"></script>
 *   2. Set UseAjax=true on your SortDropdownViewModel
 *   3. The module listens for the dropdown's 'sortchange' event on its own.
 *
 * What it does for the page:
 *   - Keeps the list on screen (dimmed, aria-busy) while the new order loads, so a failure never
 *     leaves the person with nothing: the old list stays and an error toast offers Retry.
 *   - Puts the sort in the address bar. The first entry is stamped too, so Back and Forward step
 *     through the sorts, re-rendering the list and the dropdown to match.
 *   - Fires `ajaxsort:loaded` (bubbles, detail: { sortValue, target }) on the list after each swap.
 *     Do work that depends on the new markup there. `AjaxSort.configure({ onBeforeLoad,
 *     onAfterLoad, onError })` does the same with callbacks; there is one listener, however
 *     often it is configured.
 *
 * The partial is HTML, which ApiClient deliberately refuses to return as data, so this one
 * request uses fetch(). Session expiry is still reported: ApiClient watches same-origin fetches.
 *
 * Exposed as window.AjaxSort (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.AjaxSort = factory(root);
        const start = function () { root.AjaxSort.init(); };
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', start);
        } else {
            start();
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    const FAILURE_MESSAGE = 'Could not re-sort the list. Your list is unchanged.';

    /** The partial's URL with the sort value set. Relative URLs resolve against `base`. */
    function buildPartialUrl(partialUrl, paramName, sortValue, base) {
        const url = new URL(partialUrl, base);
        url.searchParams.set(paramName, sortValue);
        return url.toString();
    }

    /** The sort a URL's query string names, or `fallback` when it names none. */
    function sortFromSearch(search, paramName, fallback) {
        const value = new URLSearchParams(search || '').get(paramName);
        return value || fallback;
    }

    const state = {
        initialized: false,
        options: {},
        lastDetail: null,   // the most recent sortchange detail: where to fetch, what to swap
        sequence: 0         // a slower, older response must not overwrite a newer one
    };

    function safeCall(fn, a, b) {
        if (typeof fn !== 'function') return;
        try { fn(a, b); } catch (e) { /* a page callback must not break the list */ }
    }

    const AjaxSort = {
        buildPartialUrl: buildPartialUrl,
        sortFromSearch: sortFromSearch,

        /** Merge page callbacks (onBeforeLoad, onAfterLoad, onError). Safe to call repeatedly. */
        configure: function (options) {
            Object.assign(state.options, options || {});
            return AjaxSort;
        },

        /** Start listening. Idempotent. `options` is the same as configure(). */
        init: function (options) {
            if (options) AjaxSort.configure(options);
            if (state.initialized) return;
            state.initialized = true;

            document.addEventListener('sortchange', function (e) {
                AjaxSort.handleSortChange(e, state.options);
            });

            // Stamp the entry the person arrived on, so Back from the first re-sort has a state
            // to restore.
            const stamp = document.querySelector('[data-ajax-sort]');
            if (stamp && root.history && typeof root.history.replaceState === 'function') {
                // The sort in the URL is the sort on screen; the dropdown knows its own default.
                const wrapper = stamp.closest('.sort-dropdown-wrapper');
                const current = wrapper && wrapper.dataset.currentSort;
                if (current) {
                    root.history.replaceState({ sort: current }, '', root.location.href);
                }
            }

            root.addEventListener('popstate', function () {
                const detail = state.lastDetail || AjaxSort.detailFromPage();
                if (!detail) return;
                const sortValue = sortFromSearch(root.location.search, detail.paramName, detail.defaultSort);
                AjaxSort.restoreDropdown(sortValue);
                AjaxSort.load(Object.assign({}, detail, { sortValue: sortValue }), state.options, false);
            });
        },

        /** What a popstate needs when no sort has been changed on this page yet. */
        detailFromPage: function () {
            const wrapper = document.querySelector('.sort-dropdown-wrapper[data-partial-url]');
            if (!wrapper) return null;
            return {
                paramName: wrapper.dataset.paramName,
                targetSelector: wrapper.dataset.targetSelector,
                partialUrl: wrapper.dataset.partialUrl,
                defaultSort: wrapper.dataset.defaultSort || wrapper.dataset.currentSort
            };
        },

        /** Tell the dropdown which option is selected, without firing another sortchange. */
        restoreDropdown: function (sortValue) {
            document.querySelectorAll('.sort-dropdown-wrapper').forEach(function (wrapper) {
                if (wrapper.sortDropdown && typeof wrapper.sortDropdown.setSelected === 'function') {
                    wrapper.sortDropdown.setSelected(sortValue);
                }
            });
        },

        /** Handle the dropdown's sortchange event. */
        handleSortChange: async function (e, options) {
            const detail = e.detail || {};
            if (!detail.targetSelector || !detail.partialUrl) {
                console.error('AjaxSort: Missing targetSelector or partialUrl');
                return;
            }
            const wrapper = document.getElementById(detail.dropdownId);
            state.lastDetail = Object.assign({}, detail, {
                defaultSort: (wrapper && wrapper.dataset.defaultSort) || detail.defaultSort
            });
            await AjaxSort.load(detail, options || state.options, true);
        },

        /**
         * Fetch the partial and swap it in. `push` adds a history entry (a fresh sort choice);
         * a popstate restore does not.
         */
        load: async function (detail, options, push) {
            const sortValue = detail.sortValue;
            const paramName = detail.paramName;
            const target = document.querySelector(detail.targetSelector);
            if (!target) {
                console.error('AjaxSort: Target element not found:', detail.targetSelector);
                return false;
            }

            const mine = ++state.sequence;
            safeCall(options.onBeforeLoad, target, sortValue);

            // Keep what is there while the new order loads
            target.setAttribute('aria-busy', 'true');
            target.classList.add('opacity-60', 'pointer-events-none');

            let ok = false;
            try {
                const response = await fetch(buildPartialUrl(detail.partialUrl, paramName, sortValue, root.location.origin), {
                    headers: { 'Accept': 'text/html', 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin'
                });

                if (!response.ok) {
                    throw new Error('The list request failed with status ' + response.status);
                }

                const html = await response.text();
                if (mine !== state.sequence) return false; // a newer sort has taken over

                target.innerHTML = html;
                ok = true;

                if (push && root.history && typeof root.history.pushState === 'function') {
                    const next = new URL(root.location.href);
                    next.searchParams.set(paramName, sortValue);
                    root.history.pushState({ sort: sortValue }, '', next.toString());
                }

                target.dispatchEvent(new CustomEvent('ajaxsort:loaded', {
                    bubbles: true,
                    detail: { sortValue: sortValue, target: target }
                }));
                safeCall(options.onAfterLoad, target, sortValue);
            } catch (error) {
                if (mine !== state.sequence) return false;
                console.error('AjaxSort: Failed to load content:', error);
                // The old list is still on screen. Say what happened and offer another try.
                if (root.toast && typeof root.toast.error === 'function') {
                    root.toast.error(FAILURE_MESSAGE, {
                        key: 'ajax-sort-failed',
                        action: { label: 'Retry', onClick: function () { AjaxSort.load(detail, options, push); } }
                    });
                }
                // Put the dropdown back on the sort that is actually showing
                const shown = sortFromSearch(root.location.search, paramName, detail.defaultSort);
                if (shown) AjaxSort.restoreDropdown(shown);
                safeCall(options.onError, error, target);
            } finally {
                if (mine === state.sequence) {
                    target.removeAttribute('aria-busy');
                    target.classList.remove('opacity-60', 'pointer-events-none');
                }
            }
            return ok;
        }
    };

    return AjaxSort;
});
