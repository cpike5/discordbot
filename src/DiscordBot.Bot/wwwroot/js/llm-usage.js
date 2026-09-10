/**
 * Admin > LLM Usage (/Admin/LlmUsage).
 * The hero cards and by-user/model/mode/day tables are rendered server-side by LlmUsageModel.
 * This module only drives the per-user drill-down panel: clicking a "Cost by User" row fetches
 * that user's paged raw ledger rows from LlmUsageController (api/admin/llm-usage/records) via
 * window.ApiClient and renders them into the panel beneath the table.
 */
(function () {
    'use strict';

    const PAGE_SIZE = 25;

    const state = {
        userId: null,
        displayName: '',
        page: 1,
        totalCount: 0
    };

    function el(id) {
        return document.getElementById(id);
    }

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function formatTime(value) {
        try {
            return new Date(value).toLocaleString();
        } catch {
            return value;
        }
    }

    function buildQuery(page) {
        const config = window.llmUsageConfig || {};
        const params = new URLSearchParams();
        if (config.startDate) params.set('from', config.startDate);
        if (config.endDate) params.set('to', `${config.endDate}T23:59:59.999`);
        if (config.guildId) params.set('guildId', config.guildId);
        if (config.mode) params.set('mode', config.mode);
        if (state.userId) params.set('userId', state.userId);
        params.set('page', String(page));
        params.set('pageSize', String(PAGE_SIZE));
        return params.toString();
    }

    function renderRows(records) {
        const body = el('llmUsageDrilldownBody');
        if (!body) return;

        if (!records || records.length === 0) {
            body.innerHTML = '<tr><td colspan="7" class="px-6 py-6 text-center text-sm text-text-tertiary">No messages in this range.</td></tr>';
            return;
        }

        body.innerHTML = records.map((r) => {
            const statusBadge = r.success
                ? '<span class="badge badge-success">OK</span>'
                : '<span class="badge badge-error">Failed</span>';
            const sourceBadge = r.costSource === 'Billed'
                ? '<span class="badge badge-blue">Billed</span>'
                : '<span class="badge badge-gray">Estimated</span>';
            const tokens = (r.inputTokens + r.outputTokens).toLocaleString();
            return `<tr>
                <td class="px-6 py-2 whitespace-nowrap text-sm text-text-secondary">${escapeHtml(formatTime(r.timestamp))}</td>
                <td class="px-6 py-2 whitespace-nowrap text-sm text-text-secondary">${escapeHtml(r.mode)}</td>
                <td class="px-6 py-2 whitespace-nowrap text-sm text-text-primary font-mono">${escapeHtml(r.model)}</td>
                <td class="px-6 py-2 whitespace-nowrap text-right text-sm text-text-secondary">${tokens}</td>
                <td class="px-6 py-2 whitespace-nowrap text-right text-sm">
                    <span class="text-text-primary font-medium">$${Number(r.costUsd).toFixed(4)}</span>
                    ${sourceBadge}
                </td>
                <td class="px-6 py-2 whitespace-nowrap text-right text-sm text-text-secondary">${r.latencyMs}ms</td>
                <td class="px-6 py-2 whitespace-nowrap text-sm">${statusBadge}</td>
            </tr>`;
        }).join('');
    }

    function updatePager() {
        const pageInfo = el('llmUsageDrilldownPageInfo');
        const prev = el('llmUsageDrilldownPrev');
        const next = el('llmUsageDrilldownNext');
        const totalPages = Math.max(1, Math.ceil(state.totalCount / PAGE_SIZE));

        if (pageInfo) {
            pageInfo.textContent = `Page ${state.page} of ${totalPages} (${state.totalCount} message${state.totalCount === 1 ? '' : 's'})`;
        }
        if (prev) prev.disabled = state.page <= 1;
        if (next) next.disabled = state.page >= totalPages;
    }

    async function loadPage(page) {
        if (!window.ApiClient) return;

        try {
            const data = await window.ApiClient.get(`/api/admin/llm-usage/records?${buildQuery(page)}`, {
                errorMessage: 'Failed to load usage records.'
            });
            state.page = page;
            state.totalCount = data.totalCount || 0;
            renderRows(data.records);
            updatePager();
        } catch (err) {
            const body = el('llmUsageDrilldownBody');
            if (body) {
                body.innerHTML = '<tr><td colspan="7" class="px-6 py-6 text-center text-sm text-error">Failed to load usage records.</td></tr>';
            }
            if (window.ApiClient.showErrorToast) {
                window.ApiClient.showErrorToast(err.message || 'Failed to load usage records.');
            }
        }
    }

    function openDrilldown(userId, displayName) {
        state.userId = userId;
        state.displayName = displayName;

        const panel = el('llmUsageDrilldown');
        const title = el('llmUsageDrilldownTitle');
        const subtitle = el('llmUsageDrilldownSubtitle');
        if (panel) panel.classList.remove('hidden');
        if (title) title.textContent = `Messages from ${displayName}`;
        if (subtitle) subtitle.textContent = `Discord ID ${userId}`;

        loadPage(1);

        if (panel) panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function init() {
        document.querySelectorAll('.llm-usage-user-row').forEach((row) => {
            row.addEventListener('click', () => {
                openDrilldown(row.dataset.userId, row.dataset.displayName);
            });
        });

        const prev = el('llmUsageDrilldownPrev');
        const next = el('llmUsageDrilldownNext');
        if (prev) prev.addEventListener('click', () => { if (state.page > 1) loadPage(state.page - 1); });
        if (next) next.addEventListener('click', () => loadPage(state.page + 1));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
