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
        initialized: false,
        // The unfiltered enabled-models list backing the per-mode default <select>s and their
        // price/context detail lines. `models` above is the search/vendor/filter-driven catalog
        // table list and must never drive the selects - options would appear/disappear as the
        // admin types into the search box or flips a filter checkbox above.
        enabledModels: [],
        // Per fieldId (setting key with ":"/"." replaced by "-"), the slug "" ("use configured
        // value") resolves to - from GET .../defaults's configuredSlug, kept in sync by loadDefaults.
        configuredSlugByFieldId: {}
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

    /** Every mode <select> rendered by Settings.cshtml, one per LLM mode. */
    function defaultSelects() {
        return Array.from(document.querySelectorAll('#aiModelsDefaultsForm select[data-mode-select]'));
    }

    /**
     * Fetches the full (unfiltered) set of currently enabled models, independent of whatever
     * search/vendor/filter state the catalog table above is in. Backs the per-mode default
     * <select>s and their price/context detail lines - see the comment on state.enabledModels.
     * Best-effort: on failure the selects simply keep whatever they already had; the catalog
     * table below reports its own load errors separately via loadCatalog()/showError().
     */
    async function loadEnabledModels() {
        try {
            const data = await window.ApiClient.get(`${API_BASE}?enabledOnly=true&sortBy=Name`);
            state.enabledModels = data.models || [];
        } catch (err) {
            // Keep the previous value rather than clearing it out from under the selects.
        }
    }

    /**
     * Rebuilds each mode <select>'s options from state.enabledModels (never the catalog table's
     * filtered state.models - see goal item 2) plus a leading "" option meaning "use the
     * configured value", labeled with what that actually resolves to. "" is always first, never
     * sorted in with the slugs. The slug currently selected is always kept as an option, even when
     * it isn't enabled (its "not enabled" badge comes from renderDefaults), and select.value is
     * never re-pointed to a different slug by this rebuild. Also toggles each field's "no models
     * enabled" hint.
     */
    function rebuildDefaultSelects() {
        const enabledSlugs = state.enabledModels
            .map(m => m.slug)
            .sort((a, b) => a.localeCompare(b));

        defaultSelects().forEach(select => {
            const current = select.value;
            const fieldId = select.dataset.fieldId;
            const configuredSlug = state.configuredSlugByFieldId[fieldId] || '';
            const emptyLabel = configuredSlug
                ? `Use configured value (${configuredSlug})`
                : 'Use configured value';

            const slugOptions = enabledSlugs.slice();
            if (current && !slugOptions.includes(current)) {
                slugOptions.push(current);
                slugOptions.sort((a, b) => a.localeCompare(b));
            }

            const optionsHtml = [`<option value="">${escapeHtml(emptyLabel)}</option>`]
                .concat(slugOptions.map(slug =>
                    `<option value="${escapeHtml(slug)}">${escapeHtml(slug)}</option>`))
                .join('');

            select.innerHTML = optionsHtml;
            // "" and `current` (whether enabled or not) are always present above, so this never
            // re-points the mode to a different slug.
            select.value = current;

            const hint = el(`aiModelDefaultHint-${fieldId}`);
            if (hint) hint.classList.toggle('hidden', enabledSlugs.length > 0);
        });
    }

    /** Fills each mode's price-per-million / context-length line from the enabled-models list. */
    function renderModeDetails() {
        defaultSelects().forEach(select => {
            const fieldId = select.dataset.fieldId;
            const detail = el(`aiModelDefaultDetail-${fieldId}`);
            if (!detail) return;

            if (!select.value) {
                detail.textContent = '';
                return;
            }

            const model = state.enabledModels.find(m => m.slug === select.value);
            if (!model) {
                detail.textContent = '';
                return;
            }
            detail.textContent =
                `${formatPrice(model.promptPricePerMillion)} prompt · ${formatPrice(model.completionPricePerMillion)} completion · ${formatContext(model.contextLength)} context`;
        });
    }

    /** Rebuilds the defaults selects/details after enabledModels and/or defaults data changes. */
    function refreshDefaultsUi() {
        rebuildDefaultSelects();
        renderModeDetails();
    }

    async function loadDefaults() {
        if (!el('aiModelsDefaultsForm')) return;

        try {
            const data = await window.ApiClient.get(`${API_BASE}/defaults`);
            renderDefaults(data.modes || []);
            hideDefaultsFormError();
        } catch (err) {
            showDefaultsFormError(`Failed to load current defaults: ${err.message || 'unknown error'}`);
        }
    }

    function renderDefaults(modes) {
        modes.forEach(mode => {
            const fieldId = mode.settingKey.replace(/[:.]/g, '-');
            state.configuredSlugByFieldId[fieldId] = mode.configuredSlug || '';

            const badges = el(`aiModelDefaultBadges-${fieldId}`);
            if (!badges) return;

            let sourceBadge;
            if (mode.source === 'Db') {
                sourceBadge = '<span class="badge badge-info">DB override</span>';
            } else if (mode.source === 'Fallback') {
                sourceBadge = '<span class="badge badge-warning">Fallback default</span>';
            } else {
                sourceBadge = '<span class="badge badge-gray">Config default</span>';
            }

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

            badges.innerHTML = `${sourceBadge}${statusBadge}`;
        });
    }

    function showDefaultsFormError(message) {
        const box = el('aiModelsDefaultsError');
        if (!box) return;
        const p = box.querySelector('p');
        if (p) p.textContent = message;
        box.classList.remove('hidden');
    }

    function hideDefaultsFormError() {
        el('aiModelsDefaultsError')?.classList.add('hidden');
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

            // The toggled model changed the enabled set the defaults selects draw from, and it may
            // be a mode's current default - refresh both together.
            await Promise.all([loadEnabledModels(), loadDefaults()]);
            refreshDefaultsUi();
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
            await Promise.all([loadCatalog(), loadDefaults(), loadEnabledModels()]);
            refreshDefaultsUi();
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

    // --- Per-mode defaults form (save / reset) -----------------------------------------------
    //
    // settings.js's button-state helpers (setButtonLoading/Success/Error) and its inline-alert
    // helpers are private to that module's closure, so this form gets its own minimal versions -
    // same CSS classes and element ids as every other Settings tab, just driven from here since
    // this form is not #settingsForm (see the comment above the section in Settings.cshtml).

    const saveIcons = {
        loading: '<svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>',
        success: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
        error: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>'
    };

    function setSaveButtonState(state) {
        const btn = el('aiModelsSaveDefaultsBtn');
        if (!btn) return;
        btn.classList.remove('btn-save-success', 'btn-save-error');
        switch (state) {
            case 'loading':
                btn.disabled = true;
                btn.innerHTML = `${saveIcons.loading} Saving…`;
                break;
            case 'success':
                btn.disabled = false;
                btn.innerHTML = `${saveIcons.success} Saved!`;
                btn.classList.add('btn-save-success');
                setTimeout(() => setSaveButtonState('idle'), 2000);
                break;
            case 'error':
                btn.disabled = false;
                btn.innerHTML = `${saveIcons.error} Save Failed - Retry`;
                btn.classList.add('btn-save-error');
                break;
            default:
                btn.disabled = false;
                btn.innerHTML = 'Save AI Models';
        }
    }

    function hideDefaultsAlerts() {
        el('saveSuccessAlert-AiModels')?.classList.add('hidden');
        el('saveErrorAlert-AiModels')?.classList.add('hidden');
        defaultSelects().forEach(select => {
            el(`aiModelDefaultFieldError-${select.dataset.fieldId}`)?.classList.add('hidden');
        });
    }

    function showDefaultsAlert(alertId, message) {
        const alert = el(alertId);
        if (!alert) return;
        const msgEl = alert.querySelector('.inline-alert-message');
        if (msgEl) msgEl.textContent = message;
        alert.classList.remove('hidden');
    }

    /** Best-effort: highlights the select(s) named in a validation error, e.g. "'slug' is not enabled…". */
    function flagOffendingSelect(message) {
        defaultSelects().forEach(select => {
            if (!select.value || !message.includes(select.value)) return;
            const err = el(`aiModelDefaultFieldError-${select.dataset.fieldId}`);
            if (err) {
                err.textContent = message;
                err.classList.remove('hidden');
            }
        });
    }

    function buildDefaultsFormData() {
        const formData = new FormData();
        defaultSelects().forEach(select => formData.append(select.name, select.value));
        return formData;
    }

    async function onSaveDefaults(evt) {
        evt.preventDefault();
        hideDefaultsAlerts();
        setSaveButtonState('loading');

        try {
            const { ok, data } = await window.ApiClient.postRaw(
                '?handler=SaveCategory&category=AiModels', buildDefaultsFormData());

            if (ok && data.success) {
                setSaveButtonState('success');
                showDefaultsAlert('saveSuccessAlert-AiModels', data.message);
                window.quickActions?.showToast(data.message, 'success');

                // The save may have changed which slug is "the" default for a mode - refresh badges
                // and detail lines so they reflect what was just persisted.
                await loadDefaults();
                refreshDefaultsUi();
            } else {
                const errorMsg = data.errors && data.errors.length
                    ? data.errors.join(', ')
                    : (data.message || 'Failed to save AI Models settings.');

                setSaveButtonState('error');
                showDefaultsAlert('saveErrorAlert-AiModels', errorMsg);
                flagOffendingSelect(errorMsg);
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (err) {
            const errorMsg = err.message || 'An error occurred while saving AI Models settings.';
            setSaveButtonState('error');
            showDefaultsAlert('saveErrorAlert-AiModels', errorMsg);
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    function bindDefaultsForm() {
        el('aiModelsDefaultsForm')?.addEventListener('submit', onSaveDefaults);
        defaultSelects().forEach(select => {
            select.addEventListener('change', () => {
                renderModeDetails();
                el(`aiModelDefaultFieldError-${select.dataset.fieldId}`)?.classList.add('hidden');
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
        Promise.all([loadDefaults(), loadEnabledModels()]).then(refreshDefaultsUi);
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
        bindDefaultsForm();
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
