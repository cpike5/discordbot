/**
 * Performance Dashboard - System Tab Module
 * Database, background services, cache and memory, live from the system-health hub group.
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};
    window.Performance.Tabs = window.Performance.Tabs || {};

    const state = {
        charts: [],
        isInitialized: false,
        onClick: null
    };

    const ChartUtils = window.Performance.ChartUtils;
    const TimeRange = window.Performance.TimeRange;

    // ------------------------------------------------------------ slow query rows

    /** Shows or hides a slow query's full text. The toggle is a real button: keyboard and screen reader friendly. */
    function toggleQueryDetails(button) {
        const index = button.dataset.queryToggle;
        const details = document.getElementById(`queryDetails-${index}`);
        const chevron = document.getElementById(`queryChevron-${index}`);
        if (!details) return;
        const open = details.classList.toggle('hidden') === false;
        button.setAttribute('aria-expanded', open ? 'true' : 'false');
        button.setAttribute('aria-label', open ? 'Hide full query' : 'Show full query');
        if (chevron) chevron.style.transform = open ? 'rotate(90deg)' : 'rotate(0deg)';
    }

    // ------------------------------------------------------------ charts

    async function initQueryTimeChart(hours) {
        const canvas = document.getElementById('systemQueryTimeChart');
        if (!canvas) return;

        try {
            const data = await ApiClient.get(`/api/metrics/system/history/database?hours=${hours}`);
            if (!document.body.contains(canvas)) return; // the tab changed while this loaded

            const samples = data && Array.isArray(data.samples) ? data.samples : [];
            if (samples.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No query history yet',
                    description: 'Database timings are sampled periodically. Check back in a few minutes.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const avgQueryTimeData = samples.map(s => s.avgQueryTimeMs);
            const showPoints = hours <= 6;

            const chart = ChartUtils.createLineChart(canvas, labels, [{
                label: 'Avg Query Time',
                data: avgQueryTimeData,
                themeColors: {
                    borderColor: c => c.secondary,
                    backgroundColor: c => c.alpha('accent-blue', 0.1)
                },
                fill: true,
                tension: 0.4,
                pointRadius: showPoints ? 3 : 0,
                pointHoverRadius: 5
            }], {
                plugins: { legend: { display: false } },
                scales: {
                    y: { beginAtZero: true, ticks: { callback: value => value + ' ms' } }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Average database query time, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Avg query time (ms)', data: avgQueryTimeData }],
                unit: 'ms'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load database metrics:', error);
            ChartUtils.showChartError(canvas, null, () => initQueryTimeChart(hours));
        }
    }

    async function initMemoryChart(hours) {
        const canvas = document.getElementById('systemMemoryChart');
        if (!canvas) return;

        try {
            const data = await ApiClient.get(`/api/metrics/system/history/memory?hours=${hours}`);
            if (!document.body.contains(canvas)) return;

            const samples = data && Array.isArray(data.samples) ? data.samples : [];
            if (samples.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No memory history yet',
                    description: 'Memory is sampled periodically. Check back in a few minutes.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const workingSetData = samples.map(s => s.workingSetMB);
            const heapData = samples.map(s => s.heapSizeMB);
            const showPoints = hours <= 6;

            const chart = ChartUtils.createLineChart(canvas, labels, [
                {
                    label: 'Working Set',
                    data: workingSetData,
                    themeColors: { borderColor: c => c.secondary },
                    backgroundColor: 'transparent',
                    tension: 0.4,
                    pointRadius: showPoints ? 2 : 0,
                    pointHoverRadius: 5
                },
                {
                    label: 'Heap Size',
                    data: heapData,
                    themeColors: { borderColor: c => c.success },
                    backgroundColor: 'transparent',
                    tension: 0.4,
                    pointRadius: showPoints ? 2 : 0,
                    pointHoverRadius: 5
                }
            ], {
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20 } },
                    tooltip: {
                        callbacks: {
                            label: context => context.dataset.label + ': ' + context.parsed.y.toFixed(1) + ' MB'
                        }
                    }
                },
                scales: {
                    y: { beginAtZero: false, ticks: { callback: value => value.toFixed(0) + ' MB' } }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Process memory, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Working set (MB)', data: workingSetData }, { label: 'Heap size (MB)', data: heapData }],
                unit: 'MB'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load memory metrics:', error);
            ChartUtils.showChartError(canvas, null, () => initMemoryChart(hours));
        }
    }

    // ------------------------------------------------------------ live updates

    function setText(id, value) {
        if (typeof animateValueChange === 'function') {
            animateValueChange(id, value);
            return;
        }
        const el = document.getElementById(id);
        if (el) el.textContent = String(value);
    }

    function swapClass(el, remove, add) {
        if (!el) return;
        el.classList.remove(...remove);
        el.classList.add(add);
    }

    const SERVICE_DOT = { RUNNING: 'bg-success', STARTING: 'bg-warning', STOPPED: 'bg-border-primary' };
    const SERVICE_TEXT = { RUNNING: 'text-success', STARTING: 'text-warning', STOPPED: 'text-text-tertiary' };

    function updateService(row, service) {
        const status = String(service.status || '');
        const key = status.toUpperCase();

        const dot = row.querySelector('.service-status-dot');
        if (dot) {
            swapClass(dot, ['bg-success', 'bg-warning', 'bg-error', 'bg-border-primary', 'animate-pulse'], SERVICE_DOT[key] || 'bg-error');
            if (key === 'RUNNING') dot.classList.add('animate-pulse');
        }
        const text = row.querySelector('.service-status-text');
        if (text) {
            text.textContent = status;
            swapClass(text, ['text-success', 'text-warning', 'text-text-tertiary', 'text-error'], SERVICE_TEXT[key] || 'text-error');
        }
        const heartbeat = row.querySelector('.service-heartbeat');
        if (heartbeat && service.lastHeartbeat) {
            heartbeat.textContent = '';
            const time = document.createElement('time');
            time.setAttribute('data-relative-time', new Date(service.lastHeartbeat).toISOString());
            heartbeat.appendChild(time);
            if (window.Format) window.Format.scan(heartbeat);
        }
        const error = row.querySelector('.service-error');
        if (error) {
            error.textContent = service.lastError || '';
            error.classList.toggle('hidden', !service.lastError);
        }
    }

    function updateCache(cacheStats) {
        if (!cacheStats) return;
        const stats = Object.entries(cacheStats);
        let hits = 0, misses = 0, size = 0;
        stats.forEach(([prefix, s]) => {
            hits += s.hits || 0;
            misses += s.misses || 0;
            size += s.size || 0;
            const row = Array.from(document.querySelectorAll('[data-cache-name]')).find(r => r.dataset.cacheName === prefix);
            if (!row || typeof s.hitRate !== 'number') return;
            const rate = row.querySelector('.cache-hit-rate');
            const tone = s.hitRate >= 90 ? 'success' : s.hitRate >= 70 ? 'warning' : 'error';
            if (rate) {
                rate.textContent = s.hitRate.toFixed(1) + '%';
                swapClass(rate, ['text-success', 'text-warning', 'text-error'], 'text-' + tone);
            }
            const bar = row.querySelector('.progress-bar-fill');
            if (bar) {
                bar.style.width = s.hitRate.toFixed(1) + '%';
                swapClass(bar, ['progress-bar-healthy', 'progress-bar-warning', 'progress-bar-error'],
                    tone === 'success' ? 'progress-bar-healthy' : 'progress-bar-' + tone);
            }
            const counts = row.querySelector('.cache-hits');
            if (counts) counts.textContent = (s.hits || 0).toLocaleString() + ' hits / ' + (s.misses || 0).toLocaleString() + ' misses';
            const sizeText = row.querySelector('.cache-size');
            if (sizeText) sizeText.textContent = 'Size: ' + (window.Format ? window.Format.plural(s.size || 0, 'item') : (s.size || 0) + ' items');
        });
        if (stats.length > 0) {
            setText('totalCacheHits', hits.toLocaleString());
            setText('totalCacheMisses', misses.toLocaleString());
            setText('totalCacheItems', size.toLocaleString());
        }
    }

    /** Applies a SystemMetricsUpdate (the hub push, or the same shape from the snapshot). */
    function applySystem(data) {
        if (!data) return;
        if (typeof data.avgQueryTimeMs === 'number') setText('avgQueryTime', data.avgQueryTimeMs.toFixed(0));
        if (typeof data.totalQueries === 'number') setText('totalQueries', data.totalQueries.toLocaleString());
        if (typeof data.queriesPerSecond === 'number') setText('queriesPerSecond', data.queriesPerSecond.toFixed(1));
        if (typeof data.slowQueryCount === 'number') {
            setText('slowQueryCount', data.slowQueryCount);
            const box = document.getElementById('slowQueryCount');
            const parent = box ? box.parentElement : null;
            swapClass(parent, ['text-error', 'text-warning', 'text-success'],
                data.slowQueryCount > 10 ? 'text-error' : data.slowQueryCount > 0 ? 'text-warning' : 'text-success');
        }

        (data.backgroundServices || []).forEach(service => {
            const row = Array.from(document.querySelectorAll('[data-service-name]')).find(r => r.dataset.serviceName === service.serviceName);
            if (row) updateService(row, service);
        });
        updateCache(data.cacheStats);
    }

    const System = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            state.onClick = function(e) {
                const button = e.target.closest && e.target.closest('[data-query-toggle]');
                if (button) toggleQueryDetails(button);
            };
            const root = document.querySelector('[data-tab="system"]');
            if (root) root.addEventListener('click', state.onClick);

            await Promise.all([initQueryTimeChart(hours), initMemoryChart(hours)]);
            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            // The tab's markup is replaced on every load, so its click listener goes with it.
            state.onClick = null;
            state.isInitialized = false;
        },

        live: {
            group: 'system-health',
            events: { SystemMetricsUpdate: applySystem },
            snapshot: async function() {
                applySystem(await DashboardHub.getCurrentSystemHealth());
            }
        },

        applySystem: applySystem
    };

    window.Performance.Tabs.System = System;
})();
