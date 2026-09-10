/**
 * AI Models tab (Admin > Settings > AI Models).
 * Renders the local OpenRouter model catalog and allowlist, and the read-only per-mode
 * defaults panel. Talks to LlmModelsController (api/admin/llm-models) via window.ApiClient.
 *
 * Initialization is lazy: this section sits outside #settingsForm (see Pages/Admin/Settings.cshtml)
 * and is not the default tab, so nothing is fetched until the AI Models tab is actually shown -
 * either by clicking it (hooked via window.settingsManager.switchTab) or by the page loading with
 * it already active (window.initialActiveCategory).
 */
(function () {
    'use strict';

    const API_BASE = '/api/admin/llm-models';

    const state = {
        models: [],
        vendors: [],
        lastRefreshAt: null,
        sortBy: 'Name',
        descending: false,
        searchDebounce: null,
        initialized: false
    };

    const MODE_LABELS = {
        GuildAssistant: 'Guild Assistant',
        DmAssistant: 'DM Assistant',
        FeatureRequests: 'Feature Requests'
    };

    function el(id) {
        return document.getElementById(id);
    }

    function formatPrice(value) {
        if (value === null || value === undefined) return '—';
        return `$${Number(value).toFixed(2)}/M`;
    }

    function formatContext(tokens) {
        if (!tokens && tokens !== 0) return '—';
        if (tokens >= 1000000) {
            const millions = tokens / 1000000;
            return `${millions % 1 === 0 ? millions : millions.toFixed(1)}M`;
        }
        if (tokens >= 1000) {
            const thousands = Math.round(tokens / 1000);
            return `${thousands}k`;
        }
        return String(tokens);
    }

    function formatDate(value) {
        if (!value) return '—';
        try {
            return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        } catch {
            return '—';
        }
    }

    function formatRelativeRefresh(value) {
        if (!value) return 'Never refreshed';
        try {
            const d = new Date(value);
            return `Last refreshed ${d.toLocaleString()}`;
        } catch {
            return 'Never refreshed';
        }
    }

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function currentFilters() {
        return {
            search: el('aiModelsSearch')?.value?.trim() || '',
            vendor: el('aiModelsVendor')?.value || '',
            enabledOnly: !!el('aiModelsEnabledOnly')?.checked,
            availableOnly: !!el('aiModelsAvailableOnly')?.checked,
            toolsOnly: !!el('aiModelsToolsOnly')?.checked
        };
    }

    function hasActiveFilters() {
        const f = currentFilters();
        return !!(f.search || f.vendor || f.enabledOnly || f.availableOnly || f.toolsOnly);
    }

    function clearFilters() {
        const searchInput = el('aiModelsSearch');
        if (searchInput) searchInput.value = '';
        const vendorSelect = el('aiModelsVendor');
        if (vendorSelect) vendorSelect.value = '';
        const enabledOnly = el('aiModelsEnabledOnly');
        if (enabledOnly) enabledOnly.checked = false;
        const availableOnly = el('aiModelsAvailableOnly');
        if (availableOnly) availableOnly.checked = false;
        const toolsOnly = el('aiModelsToolsOnly');
        if (toolsOnly) toolsOnly.checked = false;
        loadCatalog();
    }

    function buildQuery() {
        const f = currentFilters();
        const params = new URLSearchParams();
        if (f.search) params.set('search', f.search);
        if (f.vendor) params.set('vendor', f.vendor);
        if (f.enabledOnly) params.set('enabledOnly', 'true');
        if (f.availableOnly) params.set('availableOnly', 'true');
        if (f.toolsOnly) params.set('toolsOnly', 'true');
        params.set('sortBy', state.sortBy);
        params.set('descending', String(state.descending));
        return params.toString();
    }

    function hideResultStates() {
        el('aiModelsTableWrapper')?.classList.add('hidden');
        el('aiModelsEmptyState')?.classList.add('hidden');
        el('aiModelsNoMatchesState')?.classList.add('hidden');
        el('aiModelsErrorState')?.classList.add('hidden');
    }

    function showError(message) {
        // Always surface the error, even if a previous render left the table wrapper (or another
        // state panel) hidden - don't leave the panel looking silently empty.
        hideResultStates();
        const errorState = el('aiModelsErrorState');
        if (errorState) {
            const msgEl = errorState.querySelector('p');
            if (msgEl) msgEl.textContent = message;
            errorState.classList.remove('hidden');
        }
        const count = el('aiModelsCount');
        if (count) count.textContent = '';
    }

    async function loadCatalog() {
        const tbody = el('aiModelsTableBody');
        if (tbody) {
            tbody.innerHTML = '<tr><td colspan="8" class="table-cell text-sm text-text-secondary">Loading…</td></tr>';
        }

        try {
            const data = await window.ApiClient.get(`${API_BASE}?${buildQuery()}`);
            state.models = data.models || [];
            state.vendors = data.vendors || [];
            state.lastRefreshAt = data.lastRefreshAt || null;

            renderVendorOptions();
            renderTable();
            renderRefreshStatus();
        } catch (err) {
            showError(`Failed to load models: ${err.message || 'unknown error'}`);
        }
    }

    async function loadDefaults() {
        const container = el('aiModelsDefaults');
        if (!container) return;

        try {
            const data = await window.ApiClient.get(`${API_BASE}/defaults`);
            renderDefaults(data.modes || []);
        } catch (err) {
            container.innerHTML = `<p class="text-sm text-error">Failed to load defaults: ${escapeHtml(err.message)}</p>`;
        }
    }

    function renderDefaults(modes) {
        const container = el('aiModelsDefaults');
        if (!container) return;

        if (!modes.length) {
            container.innerHTML = '<p class="text-sm text-text-secondary">No modes configured.</p>';
            return;
        }

        container.innerHTML = modes.map(mode => {
            const label = MODE_LABELS[mode.mode] || mode.label || mode.mode;
            const sourceBadge = mode.source === 'Db'
                ? '<span class="badge badge-info">DB override</span>'
                : '<span class="badge badge-gray">Config default</span>';

            let statusBadge;
            if (!mode.isKnown) {
                statusBadge = '<span class="badge badge-warning">Not in catalog</span>';
            } else if (!mode.isEnabled) {
                statusBadge = '<span class="badge badge-warning">Not enabled</span>';
            } else if (!mode.isAvailable) {
                statusBadge = '<span class="badge badge-error">Unavailable</span>';
            } else {
                statusBadge = '<span class="badge badge-success">Enabled &amp; available</span>';
            }

            return `
                <div class="p-4 bg-bg-primary border border-border-primary rounded-lg">
                    <p class="text-xs font-medium text-text-secondary mb-1">${escapeHtml(label)}</p>
                    <p class="text-sm font-mono text-text-primary break-all mb-2">${escapeHtml(mode.slug)}</p>
                    <div class="flex flex-wrap gap-1.5">
                        ${sourceBadge}
                        ${statusBadge}
                    </div>
                </div>`;
        }).join('');
    }

    function renderVendorOptions() {
        const select = el('aiModelsVendor');
        if (!select) return;

        const current = select.value;
        const options = ['<option value="">All vendors</option>']
            .concat(state.vendors.map(v => `<option value="${escapeHtml(v)}">${escapeHtml(v)}</option>`));
        select.innerHTML = options.join('');
        if (state.vendors.includes(current)) {
            select.value = current;
        }
    }

    function renderRefreshStatus() {
        const status = el('aiModelsLastRefresh');
        if (status) {
            status.textContent = formatRelativeRefresh(state.lastRefreshAt);
        }
    }

    function renderTable() {
        const tbody = el('aiModelsTableBody');
        const count = el('aiModelsCount');
        if (!tbody) return;

        hideResultStates();

        if (!state.models.length) {
            // "Catalog empty" (never refreshed, or refresh hasn't found anything yet) is a different
            // situation from "filters exclude everything" - the former needs a refresh, the latter
            // needs the filters cleared.
            if (hasActiveFilters() && state.lastRefreshAt) {
                el('aiModelsNoMatchesState')?.classList.remove('hidden');
            } else {
                el('aiModelsEmptyState')?.classList.remove('hidden');
            }
            if (count) count.textContent = '';
            return;
        }

        el('aiModelsTableWrapper')?.classList.remove('hidden');

        tbody.innerHTML = state.models.map(m => {
            const staleRow = m.isEnabled && !m.isAvailable;
            const toolsBadge = m.supportsTools
                ? '<span class="badge badge-success">Yes</span>'
                : '<span class="badge badge-gray">No</span>';

            return `
                <tr class="table-row hover:bg-bg-hover transition-colors ${staleRow ? 'bg-warning/5' : ''}">
                    <td class="table-cell">
                        <div class="text-sm font-semibold text-text-primary">${escapeHtml(m.name)}</div>
                        <div class="text-xs font-mono text-text-tertiary break-all">${escapeHtml(m.slug)}</div>
                        ${staleRow ? '<span class="badge badge-warning mt-1 inline-block">Enabled but unavailable</span>' : ''}
                    </td>
                    <td class="table-cell text-sm text-text-secondary">${escapeHtml(m.vendor)}</td>
                    <td class="table-cell text-sm text-text-secondary text-right font-mono">${formatPrice(m.promptPricePerMillion)}</td>
                    <td class="table-cell text-sm text-text-secondary text-right font-mono">${formatPrice(m.completionPricePerMillion)}</td>
                    <td class="table-cell text-sm text-text-secondary text-right font-mono">${formatContext(m.contextLength)}</td>
                    <td class="table-cell text-sm text-text-secondary">${formatDate(m.releasedAt)}</td>
                    <td class="table-cell text-center">${toolsBadge}</td>
                    <td class="table-cell text-center">
                        <label class="form-toggle cursor-pointer inline-flex">
                            <input type="checkbox" class="form-toggle-input" data-slug="${escapeHtml(m.slug)}" ${m.isEnabled ? 'checked' : ''} />
                            <span class="form-toggle-track">
                                <span class="form-toggle-thumb"></span>
                            </span>
                        </label>
                    </td>
                </tr>`;
        }).join('');

        if (count) {
            count.textContent = `${state.models.length} model${state.models.length === 1 ? '' : 's'}`;
        }

        tbody.querySelectorAll('input[data-slug]').forEach(input => {
            input.addEventListener('change', onToggleEnabled);
        });
    }

    async function onToggleEnabled(evt) {
        const input = evt.target;
        const slug = input.dataset.slug;
        const enabled = input.checked;
        input.disabled = true;

        try {
            await window.ApiClient.put(`${API_BASE}/enabled`, { slug, enabled });

            const model = state.models.find(m => m.slug === slug);
            if (model) {
                model.isEnabled = enabled;
                if (enabled) model.enabledAt = new Date().toISOString();
            }

            window.quickActions?.showToast(
                `${slug} ${enabled ? 'enabled' : 'disabled'}.`, 'success');

            // Re-render so the "enabled but unavailable" highlight and enabled-only filter stay correct.
            if (el('aiModelsEnabledOnly')?.checked) {
                await loadCatalog();
            } else {
                renderTable();
            }
        } catch (err) {
            input.checked = !enabled;
            window.ApiClient.showErrorToast(err.message || 'Failed to update this model.');
        } finally {
            input.disabled = false;
        }
    }

    async function onRefresh() {
        const btn = el('aiModelsRefreshBtn');
        if (btn) {
            btn.disabled = true;
            btn.classList.add('opacity-60');
        }

        try {
            const result = await window.ApiClient.post(`${API_BASE}/refresh`, {});
            window.quickActions?.showToast(
                `Catalog refreshed: ${result.added} added, ${result.updated} updated, ${result.removed} removed.`,
                'success');
            await Promise.all([loadCatalog(), loadDefaults()]);
        } catch (err) {
            window.ApiClient.showErrorToast(err.message || 'Failed to refresh the catalog from OpenRouter.');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.classList.remove('opacity-60');
            }
        }
    }

    function debounceReload() {
        clearTimeout(state.searchDebounce);
        state.searchDebounce = setTimeout(loadCatalog, 300);
    }

    function applySort(key) {
        if (state.sortBy === key) {
            state.descending = !state.descending;
        } else {
            state.sortBy = key;
            state.descending = false;
        }
        updateSortHeaderAttrs();
        loadCatalog();
    }

    function updateSortHeaderAttrs() {
        document.querySelectorAll('#ai-models-settings th[data-sort-key]').forEach(th => {
            if (th.dataset.sortKey === state.sortBy) {
                th.setAttribute('aria-sort', state.descending ? 'descending' : 'ascending');
            } else {
                th.setAttribute('aria-sort', 'none');
            }
        });
    }

    function bindSortHeaders() {
        document.querySelectorAll('#ai-models-settings th[data-sort-key]').forEach(th => {
            th.addEventListener('click', () => applySort(th.dataset.sortKey));
            th.addEventListener('keydown', evt => {
                if (evt.key === 'Enter' || evt.key === ' ') {
                    evt.preventDefault();
                    applySort(th.dataset.sortKey);
                }
            });
        });
    }

    function bindFilters() {
        el('aiModelsSearch')?.addEventListener('input', debounceReload);
        el('aiModelsVendor')?.addEventListener('change', loadCatalog);
        el('aiModelsEnabledOnly')?.addEventListener('change', loadCatalog);
        el('aiModelsAvailableOnly')?.addEventListener('change', loadCatalog);
        el('aiModelsToolsOnly')?.addEventListener('change', loadCatalog);
        el('aiModelsRefreshBtn')?.addEventListener('click', onRefresh);
        el('aiModelsClearFiltersBtn')?.addEventListener('click', clearFilters);
    }

    /** Fetches the catalog and defaults the first (and only the first) time the tab is shown. */
    function ensureInitialized() {
        if (state.initialized || !el('ai-models-settings')) return;
        state.initialized = true;
        loadDefaults();
        loadCatalog();
    }

    function hookTabActivation() {
        // Prefer wrapping window.settingsManager.switchTab so we catch every way the tab can be
        // activated. It may not exist yet if settings.js hasn't attached it (script order), so also
        // fall back to listening on the tab button itself.
        const trigger = () => {
            if (document.getElementById('ai-models-settings')?.classList.contains('active')) {
                ensureInitialized();
            }
        };

        if (window.settingsManager && typeof window.settingsManager.switchTab === 'function') {
            const original = window.settingsManager.switchTab;
            window.settingsManager.switchTab = function (category) {
                const result = original.apply(this, arguments);
                if (category === 'AiModels') {
                    Promise.resolve(result).then(ensureInitialized);
                }
                return result;
            };
        }

        document.querySelector('.settings-tab[data-tab="AiModels"]')?.addEventListener('click', trigger);
    }

    function init() {
        if (!el('ai-models-settings')) return;

        bindFilters();
        bindSortHeaders();
        updateSortHeaderAttrs();
        hookTabActivation();

        // The page can load with AI Models already the active tab (deep link / reload).
        if (window.initialActiveCategory === 'AiModels') {
            ensureInitialized();
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.llmModels = { reload: loadCatalog, reloadDefaults: loadDefaults };
})();
