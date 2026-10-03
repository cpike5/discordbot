/**
 * Logs page (/Admin/Logs): the small interactions of the Messages and Audit tabs.
 *
 *  - Audit: show/hide the filter form, expand a row, expand the JSON details.
 *  - Messages: a long message is clamped to two lines and gets a "Show more" button only when it
 *    is actually cut off.
 *
 * All handlers are delegated and nothing user-authored is ever put into markup from here: the
 * rows are rendered by the server, and the script only toggles classes and attributes.
 */
(function () {
    'use strict';

    // ---- audit tab ---------------------------------------------------------

    function toggleFilters(button) {
        const form = document.getElementById('filtersForm');
        const text = document.getElementById('toggleFiltersText');
        const icon = document.getElementById('toggleFiltersIcon');
        const expanded = button.getAttribute('aria-expanded') === 'true';

        if (form) form.classList.toggle('hidden', expanded);
        button.setAttribute('aria-expanded', String(!expanded));
        if (text) text.textContent = expanded ? 'Show Filters' : 'Hide Filters';
        if (icon) icon.classList.toggle('rotate-180', expanded);
    }

    function toggleRow(button) {
        const row = button.closest('.audit-row');
        const rowId = row && row.dataset.rowId;
        const expandRow = rowId ? document.querySelector('.expand-content-row[data-parent-id="' + CSS.escape(rowId) + '"]') : null;
        const chevron = button.querySelector('.chevron-icon');
        const content = expandRow && expandRow.querySelector('.expand-content');
        const expanded = button.getAttribute('aria-expanded') === 'true';

        if (expandRow) expandRow.classList.toggle('hidden', expanded);
        if (chevron) chevron.classList.toggle('rotated', !expanded);
        button.setAttribute('aria-expanded', String(!expanded));

        if (content) {
            if (expanded) {
                content.classList.remove('expanded');
            } else {
                // One frame after the row is shown, so the max-height transition runs
                requestAnimationFrame(function () { content.classList.add('expanded'); });
            }
        }
    }

    function toggleJson(button) {
        const container = document.getElementById(button.dataset.target || '');
        const viewer = container && container.querySelector('.json-viewer');
        const text = button.querySelector('.toggle-json-text');
        const icon = button.querySelector('.toggle-json-icon');
        const expanded = button.getAttribute('aria-expanded') === 'true';

        if (container) container.classList.toggle('expanded', !expanded);
        if (viewer) viewer.classList.toggle('expanded', !expanded);
        if (text) text.textContent = expanded ? 'Expand' : 'Collapse';
        if (icon) icon.style.transform = expanded ? 'rotate(0deg)' : 'rotate(180deg)';
        button.setAttribute('aria-expanded', String(!expanded));
    }

    // ---- messages tab ------------------------------------------------------

    /** Shows "Show more" on the messages that the two-line clamp actually cuts off. */
    function measureContent(root) {
        (root || document).querySelectorAll('[data-log-content]').forEach(function (content) {
            const button = content.parentElement && content.parentElement.querySelector('[data-log-content-toggle]');
            if (!button) return;

            // An expanded message has no clamp to measure; its button stays so it can collapse again
            if (button.getAttribute('aria-expanded') === 'true') return;
            button.classList.toggle('hidden', content.scrollHeight <= content.clientHeight + 1);
        });
    }

    function toggleContent(button) {
        const content = button.parentElement && button.parentElement.querySelector('[data-log-content]');
        if (!content) return;

        const expanded = button.getAttribute('aria-expanded') === 'true';
        content.classList.toggle('line-clamp-2', expanded);
        button.setAttribute('aria-expanded', String(!expanded));
        button.textContent = expanded ? 'Show more' : 'Show less';
    }

    // ---- wiring ------------------------------------------------------------

    document.addEventListener('click', function (event) {
        const target = event.target;
        if (!(target instanceof Element)) return;

        const filterButton = target.closest('#toggleFilters');
        if (filterButton) {
            toggleFilters(filterButton);
            return;
        }

        const expandButton = target.closest('.expand-btn');
        if (expandButton) {
            toggleRow(expandButton);
            return;
        }

        const jsonButton = target.closest('.toggle-json-btn');
        if (jsonButton) {
            event.stopPropagation();
            toggleJson(jsonButton);
            return;
        }

        const contentButton = target.closest('[data-log-content-toggle]');
        if (contentButton) {
            toggleContent(contentButton);
        }
    });

    let resizeTimer = null;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () { measureContent(); }, 150);
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { measureContent(); });
    } else {
        measureContent();
    }

    // Fonts load after first paint and change line lengths
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(function () { measureContent(); });
    }
})();
