/**
 * Performance Dashboard - Alerts Tab Module
 * Active incidents (acknowledge), threshold configuration, alert frequency chart.
 *
 * - Threshold edits are kept while the viewer looks at other tabs (they live in this module, not
 *   in the tab's markup, which is replaced on every load) and are put back on the fresh inputs
 *   when the tab returns. Leaving the page with unsaved edits asks first (dashboard.js).
 * - Warning must be lower than critical; a problem is shown on the field, focus goes to the first
 *   one, and nothing is sent until the rows are valid. The server enforces the same rule.
 * - Live from the alerts hub group: a change to the incidents refreshes the tab, unless the viewer
 *   is mid-edit, in which case a toast offers the refresh.
 */
(function (root, factory) {
    const api = factory(root);
    if (typeof module === 'object' && module.exports) {
        module.exports = api.pure;
    } else {
        root.Performance = root.Performance || {};
        root.Performance.Tabs = root.Performance.Tabs || {};
        root.Performance.Tabs.Alerts = api.tab;
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    // ---------------------------------------------------------------- pure helpers

    /**
     * Checks one metric row. Returns a message per field, or null when the field is fine.
     * `originalWarning` / `originalCritical` are the saved values as strings ('' for none): a
     * threshold that has a value cannot be cleared, because an update without a value means
     * "leave it alone" on the server.
     */
    function validateRow(row) {
        const result = { warning: null, critical: null };
        const parsed = {};

        ['warning', 'critical'].forEach(function (field) {
            const text = String(row[field] === undefined || row[field] === null ? '' : row[field]).trim();
            const original = String(row['original' + field.charAt(0).toUpperCase() + field.slice(1)] || '').trim();
            if (text === '') {
                if (original !== '') {
                    result[field] = 'Enter a number. A threshold that is set cannot be cleared.';
                }
                return;
            }
            const value = Number(text);
            if (!isFinite(value) || value < 0) {
                result[field] = 'Enter a number that is zero or greater.';
                return;
            }
            parsed[field] = value;
        });

        if (result.warning === null && result.critical === null &&
            parsed.warning !== undefined && parsed.critical !== undefined &&
            parsed.warning >= parsed.critical) {
            result.warning = 'The warning threshold must be lower than the critical threshold.';
        }
        return result;
    }

    /** True when a validateRow result has no problems. */
    function isValid(result) {
        return !result.warning && !result.critical;
    }

    /** The PUT body for one metric's edits: only the fields that changed. */
    function buildPayload(edit) {
        const payload = {};
        if (edit.warning !== undefined && String(edit.warning).trim() !== '') payload.warningThreshold = Number(edit.warning);
        if (edit.critical !== undefined && String(edit.critical).trim() !== '') payload.criticalThreshold = Number(edit.critical);
        if (edit.enabled !== undefined) payload.isEnabled = !!edit.enabled;
        return payload;
    }

    /** Colour class for a current value against the thresholds in the inputs (higher is worse). */
    function valueTone(current, warning, critical) {
        if (typeof current !== 'number' || !isFinite(current)) return null;
        if (typeof critical === 'number' && isFinite(critical) && current >= critical) return 'text-error';
        if (typeof warning === 'number' && isFinite(warning) && current >= warning) return 'text-warning';
        return 'text-success';
    }

    const pure = { validateRow, isValid, buildPayload, valueTone };

    // ---------------------------------------------------------------- the tab

    if (typeof window === 'undefined') {
        return { pure: pure, tab: null };
    }

    window.Performance = window.Performance || {};
    const ChartUtils = window.Performance.ChartUtils;
    const REFRESH_DEBOUNCE_MS = 800;
    const OWN_ACTION_QUIET_MS = 3000;

    const state = {
        charts: [],
        // metric -> { warning?, critical?, enabled? }: only fields that differ from what was saved.
        // Survives destroy() on purpose.
        edits: {},
        form: null,
        listeners: [],
        refreshTimer: null,
        lastOwnAction: 0,
        isInitialized: false
    };

    /** Pending state on a button (LoadingManager is a global const, not a window property). */
    function setBusy(button, busy, text) {
        if (typeof LoadingManager !== 'undefined' && button) LoadingManager.setButtonLoading(button, busy, text || null);
    }

    function tabRoot() {
        return document.querySelector('[data-tab="alerts"]');
    }

    function hasUnsavedChanges() {
        return Object.keys(state.edits).length > 0;
    }

    function dashboard() {
        return window.Performance && window.Performance.Dashboard;
    }

    function listen(target, type, handler) {
        target.addEventListener(type, handler);
        state.listeners.push(function () { target.removeEventListener(type, handler); });
    }

    function rowInputs(row) {
        return {
            warning: row.querySelector('input[data-field="warning"]'),
            critical: row.querySelector('input[data-field="critical"]'),
            enabled: row.querySelector('input[data-field="enabled"]')
        };
    }

    function originals(inputs) {
        return {
            originalWarning: inputs.warning ? inputs.warning.defaultValue : '',
            originalCritical: inputs.critical ? inputs.critical.defaultValue : ''
        };
    }

    // ------------------------------------------------------------ edits

    /** Works out which fields of a row differ from the saved values and keeps `state.edits` in step. */
    function recordEdit(row) {
        const metric = row.dataset.metric;
        const inputs = rowInputs(row);
        const edit = {};
        ['warning', 'critical'].forEach(function (field) {
            const input = inputs[field];
            if (input && input.value.trim() !== input.defaultValue.trim()) edit[field] = input.value;
        });
        if (inputs.enabled && inputs.enabled.checked !== inputs.enabled.defaultChecked) {
            edit.enabled = inputs.enabled.checked;
        }
        if (Object.keys(edit).length > 0) {
            state.edits[metric] = edit;
        } else {
            delete state.edits[metric];
        }
    }

    /** Puts remembered edits back on freshly rendered inputs. */
    function reapplyEdits() {
        const root = tabRoot();
        if (!root) return;
        Object.keys(state.edits).forEach(function (metric) {
            const row = Array.from(root.querySelectorAll('tr[data-metric]')).find(r => r.dataset.metric === metric);
            if (!row) {
                delete state.edits[metric]; // the metric no longer exists
                return;
            }
            const inputs = rowInputs(row);
            const edit = state.edits[metric];
            if (edit.warning !== undefined && inputs.warning) inputs.warning.value = edit.warning;
            if (edit.critical !== undefined && inputs.critical) inputs.critical.value = edit.critical;
            if (edit.enabled !== undefined && inputs.enabled) inputs.enabled.checked = edit.enabled;
            updateCurrentValue(row);
        });
        syncSaveControls();
    }

    function syncSaveControls() {
        const dirty = hasUnsavedChanges();
        const save = document.getElementById('alertsTabSaveConfigBtn');
        const note = document.getElementById('alertsUnsavedNote');
        if (save) save.hidden = !dirty;
        if (note) note.hidden = !dirty;
    }

    // ------------------------------------------------------------ validation display

    function showRowErrors(row, result) {
        const inputs = rowInputs(row);
        const messages = [];
        ['warning', 'critical'].forEach(function (field) {
            const input = inputs[field];
            const message = result[field];
            if (input) {
                input.classList.toggle('input-validation-error', !!message);
                if (message) {
                    input.setAttribute('aria-invalid', 'true');
                } else {
                    input.removeAttribute('aria-invalid');
                }
            }
            if (message && messages.indexOf(message) < 0) messages.push(message);
        });
        const error = row.querySelector('[data-threshold-error]');
        if (error) {
            error.textContent = messages.join(' ');
            error.hidden = messages.length === 0;
        }
    }

    function validateRowElement(row) {
        const inputs = rowInputs(row);
        const result = validateRow(Object.assign({
            warning: inputs.warning ? inputs.warning.value : '',
            critical: inputs.critical ? inputs.critical.value : ''
        }, originals(inputs)));
        showRowErrors(row, result);
        return result;
    }

    /** Previews the colour of the current value against the thresholds being typed. */
    function updateCurrentValue(row) {
        const span = row.querySelector('[data-current-value]');
        if (!span) return;
        const current = parseFloat(span.dataset.currentValue);
        const inputs = rowInputs(row);
        const warning = inputs.warning && inputs.warning.value.trim() !== '' ? Number(inputs.warning.value) : null;
        const critical = inputs.critical && inputs.critical.value.trim() !== '' ? Number(inputs.critical.value) : null;
        const tone = valueTone(current, warning, critical);
        if (!tone) return;
        span.classList.remove('text-success', 'text-warning', 'text-error');
        span.classList.add(tone);
    }

    // ------------------------------------------------------------ save

    async function save(form) {
        const rows = Array.from(form.querySelectorAll('tr[data-metric]')).filter(r => state.edits[r.dataset.metric]);
        if (rows.length === 0) return;

        // Check every edited row first and send nothing while any is wrong
        let firstInvalid = null;
        rows.forEach(function (row) {
            const result = validateRowElement(row);
            if (!isValid(result) && !firstInvalid) {
                const inputs = rowInputs(row);
                firstInvalid = (result.warning ? inputs.warning : inputs.critical) || inputs.warning;
            }
        });
        if (firstInvalid) {
            firstInvalid.focus();
            window.toast.error('Fix the highlighted thresholds, then save again.', { key: 'perf-alert-invalid' });
            return;
        }

        const button = document.getElementById('alertsTabSaveConfigBtn');
        setBusy(button, true, 'Saving...');
        const failures = [];
        for (const row of rows) {
            const metric = row.dataset.metric;
            try {
                await window.ApiClient.put('/api/alerts/config/' + encodeURIComponent(metric), buildPayload(state.edits[metric]));
                // What is on screen is now what is saved
                const inputs = rowInputs(row);
                if (inputs.warning) inputs.warning.defaultValue = inputs.warning.value;
                if (inputs.critical) inputs.critical.defaultValue = inputs.critical.value;
                if (inputs.enabled) inputs.enabled.defaultChecked = inputs.enabled.checked;
                delete state.edits[metric];
            } catch (error) {
                if (error && error.sessionExpired) break;
                const name = (row.querySelector('.font-medium') || {}).textContent || metric;
                failures.push({ name: name.trim(), message: error && error.message ? error.message : 'It could not be saved.' });
            }
        }
        setBusy(button, false);
        syncSaveControls();

        if (failures.length === 0) {
            window.toast.success('Alert thresholds saved.');
            const d = dashboard();
            if (d) d.refresh(); // current values and colours follow the new thresholds
        } else {
            const first = failures[0];
            window.toast.error(
                failures.length === 1
                    ? `Could not save ${first.name}. ${first.message}`
                    : `Could not save ${failures.length} metrics. ${first.name}: ${first.message}`,
                { key: 'perf-alert-save' });
        }
    }

    // ------------------------------------------------------------ incidents

    function markAcknowledged(card) {
        const actions = card.querySelector('.alert-actions');
        if (actions) actions.remove();
        const meta = card.querySelector('.alert-meta');
        const severity = meta ? meta.querySelector('.severity-badge') : null;
        if (severity && !meta.querySelector('[data-acknowledged-badge]')) {
            const badge = document.createElement('span');
            badge.className = 'status-badge status-badge-secondary';
            badge.setAttribute('data-acknowledged-badge', '');
            badge.textContent = 'Acknowledged';
            severity.insertAdjacentElement('afterend', badge);
        }
    }

    async function acknowledge(button) {
        const id = button.dataset.acknowledgeIncident;
        if (!id) return;
        state.lastOwnAction = Date.now();
        setBusy(button, true, 'Acknowledging...');
        try {
            await window.ApiClient.post('/api/alerts/incidents/' + encodeURIComponent(id) + '/acknowledge', { notes: '' });
            const card = button.closest('[data-incident-id]');
            if (card) markAcknowledged(card);
            window.toast.success('Incident acknowledged.');
        } catch (error) {
            setBusy(button, false);
            window.ApiClient.showErrorToast(error);
        }
    }

    async function acknowledgeAll(button) {
        const confirmed = await window.quickActions.confirm({
            title: 'Acknowledge all incidents?',
            message: 'Every active incident is marked as acknowledged. They stay in the list until they resolve.',
            variant: 'warning',
            confirmText: 'Acknowledge all'
        });
        if (!confirmed) return;
        state.lastOwnAction = Date.now();
        setBusy(button, true, 'Acknowledging...');
        try {
            const result = await window.ApiClient.post('/api/alerts/incidents/acknowledge-all');
            const count = (result && result.acknowledgedCount) || 0;
            window.toast.success(window.Format
                ? 'Acknowledged ' + window.Format.plural(count, 'incident') + '.'
                : 'Acknowledged ' + count + ' incidents.');
            const d = dashboard();
            if (d) d.refresh();
        } catch (error) {
            setBusy(button, false);
            window.ApiClient.showErrorToast(error);
        }
    }

    // ------------------------------------------------------------ chart

    function getServerData() {
        const container = tabRoot();
        if (!container) return { alertFrequencyData: [] };
        try {
            return { alertFrequencyData: JSON.parse(container.dataset.alertFrequencyData || '[]') };
        } catch (e) {
            return { alertFrequencyData: [] };
        }
    }

    function initAlertFrequencyChart() {
        const canvas = document.getElementById('alertsFrequencyChart');
        if (!canvas) return;

        const data = getServerData().alertFrequencyData;
        const total = data.reduce((sum, d) => sum + (d.criticalCount || 0) + (d.warningCount || 0) + (d.infoCount || 0), 0);
        if (!data || data.length === 0 || total === 0) {
            ChartUtils.showChartEmpty(canvas, {
                title: 'No alerts in the last 30 days',
                description: 'Alerts appear here by day once a threshold is crossed.'
            });
            return;
        }

        ChartUtils.clearChartState(canvas);
        const labels = data.map(d => new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
        const critical = data.map(d => d.criticalCount || 0);
        const warning = data.map(d => d.warningCount || 0);
        const info = data.map(d => d.infoCount || 0);

        const chart = ChartUtils.createBarChart(canvas, labels, [
            { label: 'Critical', data: critical, themeColors: { backgroundColor: c => c.error }, borderRadius: 2 },
            { label: 'Warning', data: warning, themeColors: { backgroundColor: c => c.warning }, borderRadius: 2 },
            { label: 'Info', data: info, themeColors: { backgroundColor: c => c.info }, borderRadius: 2 }
        ], {
            plugins: { legend: { display: false } },
            scales: {
                x: {
                    stacked: true,
                    grid: { display: false },
                    ticks: { maxRotation: 45, minRotation: 45, autoSkip: true, maxTicksLimit: 15 }
                },
                y: { stacked: true, beginAtZero: true, ticks: { stepSize: 1, precision: 0 } }
            }
        });
        state.charts.push(chart);
        ChartUtils.describeChart(canvas, {
            caption: 'Alerts per day over the last 30 days',
            labels,
            datasets: [
                { label: 'Critical', data: critical },
                { label: 'Warning', data: warning },
                { label: 'Info', data: info }
            ],
            firstColumn: 'Day'
        });
    }

    // ------------------------------------------------------------ live

    /** Brings the tab up to date, or offers to when that would pull the page out from under the viewer. */
    function scheduleRefresh() {
        if (Date.now() - state.lastOwnAction < OWN_ACTION_QUIET_MS) return;
        clearTimeout(state.refreshTimer);
        state.refreshTimer = setTimeout(function () {
            state.refreshTimer = null;
            const root = tabRoot();
            const d = dashboard();
            if (!root || !d) return;
            const busy = hasUnsavedChanges() || (document.activeElement && root.contains(document.activeElement) && document.activeElement !== root);
            if (busy) {
                window.toast.info('Alerts have changed.', {
                    key: 'perf-alerts-changed',
                    action: { label: 'Refresh', onClick: function () { d.refresh(); } }
                });
            } else {
                d.refresh();
            }
        }, REFRESH_DEBOUNCE_MS);
    }

    const live = {
        group: 'alerts',
        events: {
            OnAlertTriggered: scheduleRefresh,
            OnAlertResolved: scheduleRefresh,
            OnAlertAcknowledged: scheduleRefresh,
            OnActiveAlertCountChanged: scheduleRefresh
        },
        // After a (re)connect the count may have moved while no events arrived
        snapshot: async function () {
            const summary = await DashboardHub.getActiveAlertCount();
            const root = tabRoot();
            if (!summary || !root || typeof summary.activeCount !== 'number') return;
            if (summary.activeCount !== (parseInt(root.dataset.activeCount, 10) || 0)) scheduleRefresh();
        }
    };

    // ------------------------------------------------------------ lifecycle

    const tab = {
        init: async function (hours) {
            this.destroy();
            const root = tabRoot();
            if (!root) return;

            const form = document.getElementById('alertThresholdForm');
            state.form = form;
            if (form) {
                listen(form, 'input', function (e) {
                    const row = e.target.closest && e.target.closest('tr[data-metric]');
                    if (!row || e.target.dataset.field === 'enabled') return;
                    recordEdit(row);
                    updateCurrentValue(row);
                    syncSaveControls();
                    // A field already marked wrong clears as soon as it is right; a new problem waits for blur
                    if (row.querySelector('[aria-invalid="true"]')) validateRowElement(row);
                });
                listen(form, 'change', function (e) {
                    const row = e.target.closest && e.target.closest('tr[data-metric]');
                    if (!row) return;
                    recordEdit(row);
                    syncSaveControls();
                    if (e.target.dataset.field !== 'enabled') validateRowElement(row);
                });
                listen(form, 'submit', function (e) {
                    e.preventDefault();
                    save(form);
                });
            }

            listen(root, 'click', function (e) {
                const one = e.target.closest && e.target.closest('[data-acknowledge-incident]');
                if (one) {
                    acknowledge(one);
                    return;
                }
                const all = e.target.closest && e.target.closest('[data-acknowledge-all]');
                if (all) acknowledgeAll(all);
            });

            reapplyEdits();
            initAlertFrequencyChart();
            state.isInitialized = true;
        },

        destroy: function () {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.listeners.forEach(off => off());
            state.listeners = [];
            clearTimeout(state.refreshTimer);
            state.refreshTimer = null;
            state.form = null;
            // state.edits stays: edits survive leaving the tab
            state.isInitialized = false;
        },

        hasUnsavedChanges: hasUnsavedChanges,
        live: live,
        _state: state
    };

    return { pure: pure, tab: tab };
});
