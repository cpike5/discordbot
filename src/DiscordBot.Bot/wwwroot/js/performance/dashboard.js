/**
 * Performance Dashboard - shell orchestrator.
 *
 * The one place the six tabs (overview, health, commands, api, system, alerts) are loaded and
 * switched. tab-panel.js owns the tablist (arrow keys, focus, announcements) and raises
 * `tabchange`; this file loads the content for the tab, keeps the time range, the 5-minute cache,
 * the URL, the hub subscription and the freshness line.
 *
 * - The tab and range live in the query string: /Admin/Performance?tab=health&hours=168. The old
 *   #health hash still opens the right tab and is rewritten to the query form. Back and Forward
 *   restore the tab.
 * - Content comes from /Admin/Performance?handler=Partial&tabId=…&hours=…, through ApiClient.
 * - A tab module (Performance.Tabs.X) has init(hours), destroy() and optionally `live`
 *   (see live.js). Only a tab with `live` is called Live; every other tab says when it was last
 *   loaded, and a cached tab shows the age of its data.
 * - Exposed as Performance.Dashboard (and window.PerformanceTabs for older callers).
 */
(function (root, factory) {
    const api = factory(root);
    if (typeof module === 'object' && module.exports) {
        module.exports = api.pure;
    } else {
        root.Performance = root.Performance || {};
        root.Performance.Dashboard = api.dashboard;
        root.PerformanceTabs = api.dashboard; // for older callers
        if (typeof document !== 'undefined') {
            const start = function () {
                if (document.querySelector('[data-performance-tabs]')) api.dashboard.init();
            };
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', start);
            } else {
                start();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    const TAB_IDS = ['overview', 'health', 'commands', 'api', 'system', 'alerts'];
    const DEFAULT_TAB = 'overview';
    const ALLOWED_HOURS = [24, 168, 720];
    const DEFAULT_HOURS = 24;
    const CACHE_MS = 5 * 60 * 1000;
    const SKELETON_DELAY_MS = 200;
    const LABELS = {
        overview: 'Overview',
        health: 'Health Metrics',
        commands: 'Commands',
        api: 'API & Rate Limits',
        system: 'System Health',
        alerts: 'Alerts'
    };
    // Tabs that do not use the time range hide the selector.
    const TABS_WITHOUT_RANGE = ['alerts'];

    // Solid icons for the active tab (tab-panel.js swaps outline and solid from these data attributes).
    const SOLID_ICONS = {
        overview: 'M2 11a1 1 0 011-1h2a1 1 0 011 1v5a1 1 0 01-1 1H3a1 1 0 01-1-1v-5zm6-4a1 1 0 011-1h2a1 1 0 011 1v9a1 1 0 01-1 1H9a1 1 0 01-1-1V7zm6-3a1 1 0 011-1h2a1 1 0 011 1v12a1 1 0 01-1 1h-2a1 1 0 01-1-1V4z',
        health: 'M3.172 5.172a4 4 0 015.656 0L12 8.343l3.172-3.171a4 4 0 115.656 5.656L12 19.657l-8.828-8.829a4 4 0 010-5.656z',
        commands: 'M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z',
        api: 'M14.447 3.027a.75.75 0 01.527.92l-4.5 16.5a.75.75 0 01-1.448-.394l4.5-16.5a.75.75 0 01.921-.526zM16.72 6.22a.75.75 0 011.06 0l5.25 5.25a.75.75 0 010 1.06l-5.25 5.25a.75.75 0 11-1.06-1.06L21.44 12l-4.72-4.72a.75.75 0 010-1.06zm-9.44 0a.75.75 0 010 1.06L2.56 12l4.72 4.72a.75.75 0 11-1.06 1.06L.97 12.53a.75.75 0 010-1.06l5.25-5.25a.75.75 0 011.06 0z',
        system: 'M4.5 3A1.5 1.5 0 003 4.5v4A1.5 1.5 0 004.5 10h11a1.5 1.5 0 001.5-1.5v-4A1.5 1.5 0 0015.5 3h-11zm0 11A1.5 1.5 0 003 15.5v4A1.5 1.5 0 004.5 21h11a1.5 1.5 0 001.5-1.5v-4a1.5 1.5 0 00-1.5-1.5h-11zM13 7a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2zm6 11a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2z',
        alerts: 'M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z'
    };

    // ---------------------------------------------------------------- pure helpers

    /** A known tab id (case-insensitive) or null. */
    function normalizeTab(value) {
        const id = String(value || '').trim().toLowerCase();
        return TAB_IDS.indexOf(id) >= 0 ? id : null;
    }

    /** Clamps to a supported range: up to 24 is 24, up to 168 is 168, longer is 720. Not a number: null. */
    function normalizeHours(value) {
        const n = parseInt(value, 10);
        if (!isFinite(n)) return null;
        if (n <= 24) return 24;
        if (n <= 168) return 168;
        return 720;
    }

    /**
     * Where the page should open: the tab from ?tab= (or the legacy #hash), the range from ?hours=.
     * `fromHash` tells the caller to rewrite the address into the query form.
     */
    function parseLocation(search, hash) {
        const params = new URLSearchParams(search || '');
        const queryTab = normalizeTab(params.get('tab'));
        const hashTab = normalizeTab(String(hash || '').replace(/^#/, ''));
        return {
            tab: queryTab || hashTab || null,
            hours: params.has('hours') ? normalizeHours(params.get('hours')) : null,
            fromHash: !queryTab && !!hashTab
        };
    }

    /** The address for a tab and range; other query values are kept, the hash is dropped. */
    function buildUrl(pathname, search, tab, hours) {
        const params = new URLSearchParams(search || '');
        params.set('tab', tab);
        if (hours && hours !== DEFAULT_HOURS) {
            params.set('hours', String(hours));
        } else {
            params.delete('hours');
        }
        return pathname + '?' + params.toString();
    }

    /** Whether a cache entry can still be shown: same range and under five minutes old. */
    function isFresh(entry, hours, now) {
        if (!entry || entry.hours !== hours) return false;
        return ((now === undefined ? Date.now() : now) - entry.fetchedAt) < CACHE_MS;
    }

    const pure = { normalizeTab, normalizeHours, parseLocation, buildUrl, isFresh, CACHE_MS, TAB_IDS, LABELS };

    // ---------------------------------------------------------------- the dashboard

    const dashboard = {
        state: {
            activeTab: null,
            hours: DEFAULT_HOURS,
            cache: new Map(),       // tabId -> { html, hours, fetchedAt }
            request: null,          // AbortController of the load in flight
            skeleton: null,
            updatedAt: null,
            initialized: false,
            programmatic: false     // tab-panel is being driven by us: ignore its tabchange
        },

        init: function () {
            const self = this;
            if (this.state.initialized) return;
            const panel = this.panel();
            if (!panel) return;
            this.state.initialized = true;

            const loc = parseLocation(window.location.search, window.location.hash);
            const initialTab = loc.tab || normalizeTab(panel.dataset.initialTab) || DEFAULT_TAB;

            // Range: the address wins, then what this browser last chose, then 24 hours.
            const TimeRange = root.Performance && root.Performance.TimeRange;
            const requested = loc.hours || normalizeHours(panel.dataset.initialHours);
            if (requested && TimeRange && TimeRange.apply) {
                TimeRange.apply(requested);
            }
            this.state.hours = requested || (TimeRange ? TimeRange.get() : DEFAULT_HOURS);

            this.prepareTabs();
            this.syncRangeButtons();

            // Align the tablist with the tab we are about to show (a legacy #hash, or a ?tab= the
            // server could not see).
            const container = this.tablistContainer();
            const current = container && container.querySelector('.tab-panel-tab.active');
            if (!current || current.dataset.tabId !== initialTab) {
                this.activateInTablist(initialTab);
            }
            if (loc.fromHash) {
                this.writeUrl(initialTab, 'replace');
            }

            document.addEventListener('tabchange', function (e) {
                if (self.state.programmatic) return;
                const id = e.detail && e.detail.tabId;
                if (id && id !== self.state.activeTab && normalizeTab(id)) {
                    self.show(id, { history: 'push' });
                }
            });

            document.addEventListener('click', function (e) {
                const link = e.target.closest && e.target.closest('[data-tab-link]');
                if (!link || e.defaultPrevented || e.button > 0 || e.metaKey || e.ctrlKey || e.shiftKey) return;
                const id = normalizeTab(link.dataset.tabLink);
                if (!id) return;
                e.preventDefault();
                self.goTo(id);
            });

            document.querySelectorAll('.time-range-btn').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    const hours = normalizeHours(btn.dataset.hours);
                    if (hours) self.changeTimeRange(hours);
                });
            });

            const refresh = document.getElementById('perfRefresh');
            if (refresh) {
                refresh.addEventListener('click', function () { self.refresh(); });
            }

            window.addEventListener('popstate', function () {
                const back = parseLocation(window.location.search, window.location.hash);
                const id = back.tab || DEFAULT_TAB;
                const hours = back.hours || (TimeRange ? TimeRange.get() : DEFAULT_HOURS);
                if (hours !== self.state.hours) {
                    self.state.hours = hours;
                    self.syncRangeButtons();
                    self.state.cache.clear();
                }
                self.activateInTablist(id);
                self.show(id, { history: 'none', force: true });
            });

            window.addEventListener('beforeunload', function (e) {
                const alerts = root.Performance && root.Performance.Tabs && root.Performance.Tabs.Alerts;
                if (alerts && typeof alerts.hasUnsavedChanges === 'function' && alerts.hasUnsavedChanges()) {
                    e.preventDefault();
                    e.returnValue = '';
                }
            });

            const Live = root.Performance && root.Performance.Live;
            if (Live) {
                Live.onChange(function () { self.renderStatus(); });
                Live.onUpdate(function (when) {
                    self.state.updatedAt = when;
                    self.renderStatus();
                });
            }

            this.show(initialTab, { history: 'none' });
        },

        // ------------------------------------------------------------ elements

        panel: function () {
            return document.getElementById('tabContent');
        },

        tablistContainer: function () {
            return document.querySelector('[data-panel-id="performanceTabs"]');
        },

        // ------------------------------------------------------------ tabs

        /**
         * Gives each tab what tab-panel.js needs to swap its icon (outline to solid) and points
         * aria-controls at the one real panel.
         */
        prepareTabs: function () {
            const container = this.tablistContainer();
            if (!container) return;
            container.querySelectorAll('.tab-panel-tab').forEach(function (tab) {
                const id = tab.dataset.tabId;
                tab.setAttribute('aria-controls', 'tabContent');
                const path = tab.querySelector('.tab-icon path');
                if (path && SOLID_ICONS[id] && !tab.dataset.iconOutline) {
                    tab.dataset.iconOutline = path.getAttribute('d');
                    tab.dataset.iconSolid = SOLID_ICONS[id];
                    // The server rendered the outline; the active tab should show the solid one.
                    if (tab.classList.contains('active')) {
                        path.setAttribute('d', SOLID_ICONS[id]);
                        const svg = path.closest('svg');
                        if (svg) {
                            svg.setAttribute('fill', 'currentColor');
                            svg.removeAttribute('stroke');
                        }
                        path.removeAttribute('stroke-linecap');
                        path.removeAttribute('stroke-linejoin');
                        path.removeAttribute('stroke-width');
                    }
                }
            });
        },

        /** Makes tab-panel.js show `tabId` as selected without that counting as a user switch. */
        activateInTablist: function (tabId) {
            const container = this.tablistContainer();
            if (!container || !root.TabPanel || typeof root.TabPanel.activateTab !== 'function') return;
            this.state.programmatic = true;
            try {
                root.TabPanel.activateTab(container, tabId, 'none');
            } finally {
                this.state.programmatic = false;
            }
        },

        /** Opens a tab as if its button had been pressed (status cards, "View all" links). */
        goTo: function (tabId) {
            const container = this.tablistContainer();
            if (container && root.TabPanel && typeof root.TabPanel.activateTab === 'function') {
                // Raises tabchange, which calls show() with a history entry.
                root.TabPanel.activateTab(container, tabId, 'none');
            } else {
                this.show(tabId, { history: 'push' });
            }
            const button = document.getElementById('performanceTabs-tab-' + tabId);
            if (button && typeof button.focus === 'function') {
                // Keep keyboard users where the action took them: on the tab they chose.
                button.focus({ preventScroll: true });
            }
        },

        /**
         * Shows a tab: from the cache when it is under five minutes old and for the same range,
         * otherwise from the server.
         * @param {string} tabId
         * @param {{history?: 'push'|'replace'|'none', force?: boolean}} [opts]
         */
        show: function (tabId, opts) {
            const o = opts || {};
            const previous = this.state.activeTab;
            this.state.activeTab = tabId;

            if (previous && previous !== tabId) {
                this.noteAlertEdits(previous);
            }
            if (o.history === 'push' || o.history === 'replace') {
                this.writeUrl(tabId, o.history);
            }

            const panel = this.panel();
            if (panel) {
                panel.setAttribute('aria-labelledby', 'performanceTabs-tab-' + tabId);
            }
            const rangeGroup = document.getElementById('timeRangeGroup');
            if (rangeGroup) rangeGroup.hidden = TABS_WITHOUT_RANGE.indexOf(tabId) >= 0;

            const cached = this.state.cache.get(tabId);
            if (!o.force && isFresh(cached, this.state.hours)) {
                this.render(tabId, cached.html, cached.fetchedAt);
                return Promise.resolve();
            }
            return this.load(tabId);
        },

        /** Fetches the tab's partial view and shows it. A newer request replaces an older one. */
        load: async function (tabId) {
            const panel = this.panel();
            if (!panel) return;
            const hours = this.state.hours;

            this.cancelRequest();
            const controller = new AbortController();
            this.state.request = controller;
            this.setBusy(true);
            if (root.Skeleton) {
                this.state.skeleton = root.Skeleton.show(panel, {
                    kind: 'card',
                    type: 'stats',
                    delay: SKELETON_DELAY_MS,
                    label: 'Loading ' + (LABELS[tabId] || tabId)
                });
            }

            try {
                const url = '/Admin/Performance?handler=Partial&tabId=' + encodeURIComponent(tabId) + '&hours=' + hours;
                const html = await root.ApiClient.getHtml(url, { signal: controller.signal });
                if (this.state.request !== controller || this.state.activeTab !== tabId) return;
                const fetchedAt = Date.now();
                this.state.cache.set(tabId, { html: html, hours: hours, fetchedAt: fetchedAt });
                this.clearSkeleton();
                this.render(tabId, html, fetchedAt);
            } catch (error) {
                if (error && error.name === 'AbortError') return;
                if (this.state.request !== controller) return;
                this.clearSkeleton();
                this.showError(tabId, error);
            } finally {
                if (this.state.request === controller) {
                    this.state.request = null;
                    this.setBusy(false);
                }
            }
        },

        /** Puts tab content in the panel and starts the tab's module. */
        render: function (tabId, html, fetchedAt) {
            const panel = this.panel();
            if (!panel) return;
            this.destroyAll();

            panel.innerHTML = html;
            this.setBusy(false);
            this.state.updatedAt = new Date(fetchedAt);

            if (root.Format && typeof root.Format.scan === 'function') root.Format.scan(panel);
            if (root.PreviewPopup && typeof root.PreviewPopup.init === 'function') root.PreviewPopup.init();
            this.updateAlertsBadge(panel);

            const mod = this.module(tabId);
            if (mod && typeof mod.init === 'function') {
                try {
                    const result = mod.init(this.state.hours);
                    if (result && typeof result.catch === 'function') {
                        result.catch(function (e) { console.error('Performance tab failed to start', tabId, e); });
                    }
                } catch (e) {
                    console.error('Performance tab failed to start', tabId, e);
                }
            }

            const Live = root.Performance && root.Performance.Live;
            if (Live) {
                Live.subscribe(mod && mod.live ? mod.live : null)
                    .catch(function (e) { console.error('Performance live updates failed to start', e); });
            }
            this.renderStatus();
            this.announce((LABELS[tabId] || tabId) + ' tab loaded');
        },

        module: function (tabId) {
            const tabs = root.Performance && root.Performance.Tabs;
            if (!tabs) return null;
            const name = tabId.charAt(0).toUpperCase() + tabId.slice(1); // tabId is an ASCII id from the page, not user text
            return tabs[name] || null;
        },

        /** Tears down every tab module (charts, listeners). Edits in progress are kept by their module. */
        destroyAll: function () {
            TAB_IDS.forEach(function (id) {
                const mod = dashboard.module(id);
                if (mod && typeof mod.destroy === 'function') {
                    try { mod.destroy(); } catch (e) { console.error('Performance tab failed to stop', id, e); }
                }
            });
            const panel = this.panel();
            if (panel && root.Chart && typeof root.Chart.getChart === 'function') {
                panel.querySelectorAll('canvas').forEach(function (canvas) {
                    const chart = root.Chart.getChart(canvas);
                    if (chart) chart.destroy();
                });
            }
        },

        showError: function (tabId, error) {
            const panel = this.panel();
            if (!panel) return;
            this.destroyAll();
            const Live = root.Performance && root.Performance.Live;
            if (Live) Live.unsubscribe().catch(function (e) { console.error('Performance live updates failed to stop', e); });
            const message = error && error.name === 'ApiClientError' && error.message
                ? error.message
                : 'Something went wrong while loading. Check your connection and try again.';
            root.EmptyState.error(panel, {
                title: 'Could not load ' + (LABELS[tabId] || 'this tab'),
                description: message,
                headingLevel: 2,
                onRetry: function () { dashboard.refresh(); }
            });
            this.announce('Could not load ' + (LABELS[tabId] || tabId) + '. ' + message);
            this.renderStatus();
        },

        refresh: function () {
            if (!this.state.activeTab) return Promise.resolve();
            this.state.cache.delete(this.state.activeTab);
            return this.load(this.state.activeTab);
        },

        retryCurrentTab: function () {
            return this.refresh();
        },

        getActiveTab: function () { return this.state.activeTab; },
        getCurrentHours: function () { return this.state.hours; },

        // ------------------------------------------------------------ range

        changeTimeRange: function (hours) {
            if (hours === this.state.hours) return;
            this.state.hours = hours;
            const TimeRange = root.Performance && root.Performance.TimeRange;
            if (TimeRange) TimeRange.set(hours);
            this.state.cache.clear();
            this.syncRangeButtons();
            this.writeUrl(this.state.activeTab, 'replace');
            this.load(this.state.activeTab);
        },

        syncRangeButtons: function () {
            const hours = this.state.hours;
            document.querySelectorAll('.time-range-btn').forEach(function (btn) {
                const on = parseInt(btn.dataset.hours, 10) === hours;
                btn.classList.toggle('active', on);
                btn.setAttribute('aria-pressed', on ? 'true' : 'false');
            });
        },

        // ------------------------------------------------------------ address

        writeUrl: function (tabId, mode) {
            const url = buildUrl(window.location.pathname, window.location.search, tabId, this.state.hours);
            const state = { tab: tabId, hours: this.state.hours };
            try {
                if (mode === 'push') {
                    history.pushState(state, '', url);
                } else {
                    history.replaceState(state, '', url);
                }
            } catch (e) {
                // History can be blocked in sandboxed frames; the page works without it.
            }
        },

        // ------------------------------------------------------------ status and announcements

        setBusy: function (busy) {
            const panel = this.panel();
            if (!panel) return;
            panel.classList.toggle('is-loading', busy);
            if (busy) {
                panel.setAttribute('aria-busy', 'true');
            } else {
                panel.removeAttribute('aria-busy');
            }
            const refresh = document.getElementById('perfRefresh');
            if (refresh) refresh.disabled = busy;
        },

        clearSkeleton: function () {
            if (this.state.skeleton) {
                this.state.skeleton.hide();
                this.state.skeleton = null;
            }
        },

        cancelRequest: function () {
            this.clearSkeleton();
            if (this.state.request) {
                this.state.request.abort();
                this.state.request = null;
            }
        },

        /** Live chip (only on a subscribed tab with the hub up) and the "Updated … ago" line. */
        renderStatus: function () {
            const Live = root.Performance && root.Performance.Live;
            const status = Live ? Live.status() : 'none';
            const chip = document.getElementById('perfLive');
            const text = chip ? chip.querySelector('[data-perf-live-text]') : null;
            if (chip) {
                chip.classList.toggle('hidden', status === 'none');
                chip.classList.toggle('paused', status === 'paused');
                if (text) text.textContent = status === 'paused' ? 'Paused' : 'Live';
            }
            const updated = document.getElementById('perfUpdated');
            const time = document.getElementById('perfUpdatedTime');
            if (updated && time) {
                const show = status !== 'live' && !!this.state.updatedAt;
                updated.hidden = !show;
                if (show) {
                    time.setAttribute('data-relative-time', this.state.updatedAt.toISOString());
                    if (root.Format && typeof root.Format.scan === 'function') root.Format.scan(updated);
                }
            }
        },

        announce: function (message) {
            const line = document.getElementById('perfStatusLine');
            if (!line) return;
            line.textContent = '';
            setTimeout(function () { line.textContent = message; }, 50);
        },

        // ------------------------------------------------------------ alerts tab extras

        updateAlertsBadge: function (panel) {
            const marker = panel.querySelector('[data-tab="alerts"][data-active-count]');
            if (!marker) return;
            this.setAlertsBadge(parseInt(marker.dataset.activeCount, 10) || 0);
        },

        setAlertsBadge: function (count) {
            const tab = document.getElementById('performanceTabs-tab-alerts');
            if (!tab) return;
            let badge = tab.querySelector('.tab-badge');
            if (count <= 0) {
                if (badge) badge.remove();
                return;
            }
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'tab-badge tab-badge-warning';
                tab.appendChild(badge);
            }
            badge.textContent = String(count);
        },

        /** Edits to alert thresholds survive leaving the tab; say so, so they are not forgotten. */
        noteAlertEdits: function (leaving) {
            if (leaving !== 'alerts') return;
            const alerts = root.Performance && root.Performance.Tabs && root.Performance.Tabs.Alerts;
            if (alerts && typeof alerts.hasUnsavedChanges === 'function' && alerts.hasUnsavedChanges() && root.toast) {
                root.toast.info('Your alert threshold edits are kept. Return to Alerts to save them.', { key: 'perf-alert-edits' });
            }
        }
    };

    return { dashboard: dashboard, pure: pure };
});
