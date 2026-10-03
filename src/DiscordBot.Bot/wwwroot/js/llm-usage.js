/**
 * Admin > LLM Usage (/Admin/LlmUsage).
 * The hero cards and by-user/model/mode/day tables are rendered server-side by LlmUsageModel.
 * This module drives the page's small interactions:
 *
 *  - the filter panel and the date presets (local calendar days, from DateRangeFilter);
 *  - the per-user drill-down: choosing a "Cost by user" row (a real button, so the keyboard works)
 *    fetches that user's paged ledger rows from api/admin/llm-usage/records through ApiClient and
 *    renders them under the table, with loading, empty and error states.
 *
 * Requests are sequenced: choosing another user or page aborts the one in flight, and a response
 * that is no longer current is dropped, so a slow answer can never overwrite a newer one.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    }
    if (typeof document !== 'undefined') {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () { api.init(); });
        } else {
            api.init();
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    const PAGE_SIZE = 25;
    const FILTER_PANEL_KEY = 'llmUsageFilterExpanded';

    // ---- pure helpers ------------------------------------------------------

    /** The query string of one drill-down request. Dates are the UTC instants the server computed. */
    function buildQuery(config, userId, page) {
        const params = new URLSearchParams();
        if (config.fromUtc) params.set('from', config.fromUtc);
        if (config.toUtc) params.set('to', config.toUtc);
        if (config.guildId) params.set('guildId', config.guildId);
        if (config.mode) params.set('mode', config.mode);
        if (userId) params.set('userId', userId);
        params.set('page', String(page));
        params.set('pageSize', String(PAGE_SIZE));
        return params.toString();
    }

    /** "Page 2 of 5 (113 messages)". */
    function pagerText(page, totalCount) {
        const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
        const noun = typeof Format !== 'undefined'
            ? Format.plural(totalCount, 'message')
            : totalCount + (totalCount === 1 ? ' message' : ' messages');
        return 'Page ' + page + ' of ' + totalPages + ' (' + noun + ')';
    }

    // ---- drill-down --------------------------------------------------------

    const state = {
        userId: null,
        displayName: '',
        page: 1,
        totalCount: 0,
        opener: null,
        // Incremented by every request; a response only counts while it is still the latest
        sequence: 0,
        controller: null
    };

    function el(id) {
        return document.getElementById(id);
    }

    function esc(value) {
        return SafeHtml.escape(value);
    }

    function setState(node) {
        // Used for the loading, empty and error states; the table hides while one shows
        const holder = el('llmUsageDrilldownState');
        const wrap = el('llmUsageDrilldownTableWrap');
        if (wrap) wrap.classList.toggle('hidden', !!node);
        if (holder && !node) holder.replaceChildren();
    }

    function renderRows(records) {
        const body = el('llmUsageDrilldownBody');
        if (!body) return;

        body.innerHTML = records.map((r) => {
            const statusBadge = r.success
                ? '<span class="badge badge-success">OK</span>'
                : '<span class="badge badge-error">Failed</span>';
            const sourceBadge = r.costSource === 'Billed'
                ? '<span class="badge badge-blue">Billed</span>'
                : '<span class="badge badge-gray">Estimated</span>';
            const tokens = Format.number(Number(r.inputTokens) + Number(r.outputTokens));
            const when = Format.formatDate(r.timestamp, 'datetime');
            return `<tr>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-sm text-text-secondary">${esc(when)}</td>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-sm text-text-secondary">${esc(r.mode)}</td>
                <td class="px-4 sm:px-6 py-2 text-sm text-text-primary font-mono break-all">${esc(r.model)}</td>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-right text-sm text-text-secondary">${esc(tokens)}</td>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-right text-sm">
                    <span class="text-text-primary font-medium">$${esc(Number(r.costUsd).toFixed(4))}</span>
                    ${sourceBadge}
                </td>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-right text-sm text-text-secondary">${esc(Format.number(Number(r.latencyMs)))} ms</td>
                <td class="px-4 sm:px-6 py-2 whitespace-nowrap text-sm">${statusBadge}</td>
            </tr>`;
        }).join('');
    }

    function updatePager() {
        const pageInfo = el('llmUsageDrilldownPageInfo');
        const prev = el('llmUsageDrilldownPrev');
        const next = el('llmUsageDrilldownNext');
        const totalPages = Math.max(1, Math.ceil(state.totalCount / PAGE_SIZE));

        if (pageInfo) pageInfo.textContent = pagerText(state.page, state.totalCount);
        if (prev) prev.disabled = state.page <= 1;
        if (next) next.disabled = state.page >= totalPages;
    }

    async function loadPage(page) {
        const config = window.llmUsageConfig || {};
        const holder = el('llmUsageDrilldownState');
        if (!holder) return;

        // A newer request replaces whatever is in flight
        if (state.controller) state.controller.abort();
        const controller = new AbortController();
        state.controller = controller;
        const mine = ++state.sequence;

        setState(true);
        const loading = Skeleton.show(holder, { kind: 'table', rows: 5, columns: 5, delay: 150 });

        try {
            const data = await ApiClient.get('/api/admin/llm-usage/records?' + buildQuery(config, state.userId, page), {
                signal: controller.signal,
                errorMessage: 'The messages could not be loaded.'
            });
            if (mine !== state.sequence) return;

            loading.hide();
            state.page = page;
            state.totalCount = data.totalCount || 0;

            if (!data.records || data.records.length === 0) {
                setState(true);
                EmptyState.render(holder, {
                    type: 'noResults',
                    title: 'No messages in this range',
                    description: 'This user has no recorded messages for the selected dates and filters.',
                    size: 'compact',
                    announce: true
                });
                return;
            }

            holder.replaceChildren();
            setState(null);
            renderRows(data.records);
            updatePager();
        } catch (error) {
            // An abort means a newer request took over: it owns the display now
            if (mine !== state.sequence || (error && error.name === 'AbortError')) return;

            loading.hide();
            setState(true);
            EmptyState.error(holder, {
                title: 'Could not load these messages',
                description: 'The usage records could not be loaded. Check your connection and try again.',
                size: 'compact',
                onRetry: () => loadPage(page)
            });
        }
    }

    function openDrilldown(userId, displayName, opener) {
        state.userId = userId;
        state.displayName = displayName;
        state.opener = opener || null;

        document.querySelectorAll('.llm-usage-user-button').forEach((button) => {
            const row = button.closest('.llm-usage-user-row');
            button.setAttribute('aria-expanded', String(!!row && row.dataset.userId === userId));
        });

        const panel = el('llmUsageDrilldown');
        const title = el('llmUsageDrilldownTitle');
        const subtitle = el('llmUsageDrilldownSubtitle');
        if (panel) panel.classList.remove('hidden');
        if (title) title.textContent = 'Messages from ' + displayName;
        if (subtitle) subtitle.textContent = 'Discord ID ' + userId;

        loadPage(1);

        if (panel) {
            const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            panel.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'nearest' });
        }
        // Focus follows the content a keyboard or screen reader user just asked for
        if (title) title.focus({ preventScroll: true });
    }

    function closeDrilldown() {
        if (state.controller) state.controller.abort();
        state.sequence += 1;

        const panel = el('llmUsageDrilldown');
        if (panel) panel.classList.add('hidden');
        document.querySelectorAll('.llm-usage-user-button').forEach((button) => button.setAttribute('aria-expanded', 'false'));

        // Put focus back on the row that opened it
        if (state.opener && document.contains(state.opener)) state.opener.focus();
        state.opener = null;
    }

    // ---- filter panel, presets, dates -------------------------------------

    function setPanelExpanded(expanded, persist) {
        const content = el('llmUsageFilterContent');
        const button = el('llmUsageFilterToggle');
        const chevron = el('llmUsageFilterChevron');
        if (!content || !button) return;

        content.hidden = !expanded;
        button.setAttribute('aria-expanded', String(expanded));
        if (chevron) chevron.style.transform = expanded ? 'rotate(0deg)' : 'rotate(-90deg)';
        if (persist) {
            try { localStorage.setItem(FILTER_PANEL_KEY, String(expanded)); } catch (e) { /* storage blocked */ }
        }
    }

    function initFilterPanel() {
        let saved = null;
        try { saved = localStorage.getItem(FILTER_PANEL_KEY); } catch (e) { /* storage blocked */ }
        const roomy = window.matchMedia && window.matchMedia('(min-width: 1024px)').matches;
        const filtersActive = !!(document.querySelector('#GuildId') && document.querySelector('#GuildId').value) ||
            !!(document.querySelector('#Mode') && document.querySelector('#Mode').value);
        setPanelExpanded(saved === 'true' || saved === 'false' ? saved === 'true' : (filtersActive || roomy), false);
    }

    function highlightPreset() {
        const start = el('StartDate');
        const end = el('EndDate');
        const active = start && end && window.DateRangeFilter
            ? window.DateRangeFilter.detectPreset(start.value, end.value)
            : null;

        document.querySelectorAll('[data-date-preset]').forEach((button) => {
            const on = button.dataset.datePreset === active;
            button.setAttribute('aria-pressed', String(on));
            button.classList.toggle('btn-primary', on);
            button.classList.toggle('btn-secondary', !on);
        });
    }

    function applyPreset(button) {
        if (!window.DateRangeFilter || !window.DateRangeFilter.applyPreset(el('StartDate'), el('EndDate'), button.dataset.datePreset)) return;
        highlightPreset();
        const form = el('llmUsageFilterForm');
        if (form && typeof form.requestSubmit === 'function') form.requestSubmit();
        else if (form) form.submit();
    }

    /** Daily buckets are UTC days: show each as the calendar date it is, in the viewer's locale. */
    function formatDays() {
        document.querySelectorAll('[data-utc-day]').forEach((node) => {
            const text = Format.formatDate(node.dataset.utcDay + 'T00:00:00Z', 'date', { timeZone: 'UTC' });
            if (text) node.textContent = text;
        });
    }

    // ---- wiring ------------------------------------------------------------

    let initialized = false;

    function init() {
        if (initialized || typeof document === 'undefined') return;
        initialized = true;

        initFilterPanel();
        highlightPreset();
        formatDays();

        document.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;

            if (target.closest('[data-llm-filter-toggle]')) {
                return setPanelExpanded(el('llmUsageFilterToggle').getAttribute('aria-expanded') !== 'true', true);
            }

            const preset = target.closest('[data-date-preset]');
            if (preset) return applyPreset(preset);

            // The user button, or a click anywhere else on its row (mouse convenience)
            const row = target.closest('.llm-usage-user-row');
            if (row) {
                const button = row.querySelector('.llm-usage-user-button');
                return openDrilldown(row.dataset.userId, row.dataset.displayName, button);
            }

            if (target.closest('#llmUsageDrilldownClose')) return closeDrilldown();
            if (target.closest('#llmUsageDrilldownPrev') && state.page > 1) return loadPage(state.page - 1);
            if (target.closest('#llmUsageDrilldownNext')) return loadPage(state.page + 1);
        });

        document.addEventListener('change', (event) => {
            const target = event.target;
            if (target instanceof Element && (target.id === 'StartDate' || target.id === 'EndDate')) highlightPreset();
        });
    }

    return { init, buildQuery, pagerText, PAGE_SIZE };
});
