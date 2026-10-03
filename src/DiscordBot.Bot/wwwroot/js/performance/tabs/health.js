/**
 * Performance Dashboard - Health Tab Module
 * Gauges and latency history, live from the performance hub group.
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};
    window.Performance.Tabs = window.Performance.Tabs || {};

    const state = {
        charts: [],
        gauges: { latency: null, memory: null, cpu: null },
        sparkline: null,
        sparklineData: [],
        isInitialized: false
    };

    const ChartUtils = window.Performance.ChartUtils;
    const TimeRange = window.Performance.TimeRange;

    // Gauge scales. Colours come from the theme tokens (success, warning, error) by threshold.
    const GAUGES = {
        latency: { id: 'healthLatencyGauge', max: 500, thresholds: [0, 100, 200], label: 'Heartbeat latency', unit: 'ms' },
        memory: { id: 'healthMemoryGauge', max: 1024, thresholds: [0, 512, 768], label: 'Memory use', unit: 'MB' },
        cpu: { id: 'healthCpuGauge', max: 100, thresholds: [0, 50, 80], label: 'CPU use', unit: '%' }
    };
    const SPARKLINE_MAX_POINTS = 20;

    function getServerData() {
        const container = document.querySelector('[data-tab="health"]');
        if (!container) return null;

        let samples = [];
        try {
            samples = JSON.parse(container.dataset.latencySamples || '[]');
        } catch (e) {
            samples = [];
        }
        return {
            initialLatency: parseInt(container.dataset.initialLatency, 10) || 0,
            workingSetMB: parseInt(container.dataset.workingSetMb, 10) || 0,
            cpuPercent: parseFloat(container.dataset.cpuPercent) || 0,
            latencySamples: samples
        };
    }

    function setText(id, value) {
        if (typeof animateValueChange === 'function') {
            animateValueChange(id, value);
            return;
        }
        const el = document.getElementById(id);
        if (el) el.textContent = String(value);
    }

    function createGauge(key, value) {
        const g = GAUGES[key];
        const canvas = document.getElementById(g.id);
        if (!canvas) return;
        const chart = ChartUtils.createGaugeChart(canvas, value, g.max, g.thresholds);
        if (chart) {
            state.gauges[key] = chart;
            state.charts.push(chart);
        }
        ChartUtils.describeGauge(canvas, { label: g.label, value: Math.round(value), unit: g.unit, max: g.max });
    }

    function moveGauge(key, value) {
        const g = GAUGES[key];
        ChartUtils.updateGauge(state.gauges[key], value, g.max, g.thresholds);
        ChartUtils.describeGauge(g.id, { label: g.label, value: Math.round(value), unit: g.unit, max: g.max });
    }

    function sparklineColors(samples) {
        return c => samples.map(v => v < 100 ? c.alpha('success', 0.8) : v < 200 ? c.alpha('warning', 0.8) : c.alpha('error', 0.8));
    }

    function initSparklineChart(samples) {
        const canvas = document.getElementById('healthLatencySparkline');
        if (!canvas || !samples || samples.length === 0) return;

        state.sparklineData = samples.slice(-SPARKLINE_MAX_POINTS);
        const labels = state.sparklineData.map((_, i) => i + 1);
        const dataset = {
            label: 'Latency (ms)',
            data: state.sparklineData,
            themeColors: { backgroundColor: sparklineColors(state.sparklineData) },
            borderRadius: 2
        };
        const chart = ChartUtils.createChart(canvas, {
            type: 'bar',
            data: { labels, datasets: [dataset] },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        borderWidth: 1,
                        callbacks: {
                            title: () => '',
                            label: (context) => context.parsed.y + ' ms'
                        }
                    }
                },
                scales: {
                    x: { display: false },
                    y: { display: false, beginAtZero: true }
                }
            }
        });
        state.sparkline = chart;
        state.charts.push(chart);
        describeSparkline();
    }

    function describeSparkline() {
        const canvas = document.getElementById('healthLatencySparkline');
        if (!canvas) return;
        ChartUtils.describeChart(canvas, {
            caption: `Last ${state.sparklineData.length} heartbeats`,
            labels: state.sparklineData.map((_, i) => '#' + (i + 1)),
            datasets: [{ label: 'Latency (ms)', data: state.sparklineData }],
            unit: 'ms',
            firstColumn: 'Heartbeat'
        });
    }

    function pushSparkline(latencyMs) {
        if (!state.sparkline || typeof latencyMs !== 'number') return;
        state.sparklineData.push(latencyMs);
        if (state.sparklineData.length > SPARKLINE_MAX_POINTS) state.sparklineData.shift();
        const dataset = state.sparkline.data.datasets[0];
        state.sparkline.data.labels = state.sparklineData.map((_, i) => i + 1);
        dataset.data = state.sparklineData;
        dataset.themeColors = { backgroundColor: sparklineColors(state.sparklineData) };
        ChartUtils.applyThemeColors(dataset);
        state.sparkline.update('none');
        describeSparkline();
    }

    async function initLatencyHistoryChart(hours) {
        const canvas = document.getElementById('healthLatencyHistoryChart');
        if (!canvas) return;

        // Replace the previous history chart when the range changes
        const existing = window.Chart && Chart.getChart ? Chart.getChart(canvas) : null;
        if (existing) {
            state.charts = state.charts.filter(c => c !== existing);
            existing.destroy();
        }

        try {
            const data = await ApiClient.get(`/api/metrics/health/latency?hours=${hours}`);
            if (!document.body.contains(canvas)) return; // the tab changed while this loaded

            const samples = data && Array.isArray(data.samples) ? data.samples : [];
            if (samples.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No latency samples yet',
                    description: 'Heartbeat latency is recorded while the bot is connected to Discord.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const latencies = samples.map(s => s.latencyMs);

            const chart = ChartUtils.createLineChart(canvas, labels, [{
                label: 'Latency (ms)',
                data: latencies,
                themeColors: {
                    borderColor: c => c.secondary,
                    backgroundColor: c => c.alpha('accent-blue', 0.1)
                },
                fill: true,
                tension: 0.4,
                pointRadius: 2,
                pointHoverRadius: 5
            }], {
                plugins: { legend: { display: false } },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: { callback: value => value + ' ms' }
                    }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: 'Heartbeat latency over time',
                labels,
                datasets: [{ label: 'Latency (ms)', data: latencies }],
                unit: 'ms'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load latency history:', error);
            ChartUtils.showChartError(canvas, null, () => initLatencyHistoryChart(hours));
        }
    }

    function bindRangeSelect(hours) {
        const select = document.getElementById('healthLatencyTimeRange');
        if (!select) return;
        // Start on the shell's range; the select can narrow it to an hour or six.
        select.value = String(hours);
        if (select.value !== String(hours)) select.value = '24';
        select.addEventListener('change', function() {
            initLatencyHistoryChart(parseInt(select.value, 10));
        });
    }

    function badge(id, text, cls) {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = text;
        el.classList.remove('status-badge-connected', 'status-badge-warning', 'status-badge-error');
        el.classList.add(cls);
    }

    function statusFor(value, warn, error) {
        return value < warn ? ['Normal', 'status-badge-connected'] : value < error ? ['Elevated', 'status-badge-warning'] : ['High', 'status-badge-error'];
    }

    /** Applies a HealthMetricsUpdate (the hub push, or the same shape from the snapshot). */
    function applyHealth(data) {
        if (!data) return;

        if (typeof data.latencyMs === 'number') {
            setText('healthCurrentLatency', data.latencyMs);
            moveGauge('latency', data.latencyMs);
            pushSparkline(data.latencyMs);
        }
        if (typeof data.workingSetMB === 'number') {
            setText('healthMemoryUsage', data.workingSetMB);
            setText('healthWorkingSetText', data.workingSetMB);
            moveGauge('memory', data.workingSetMB);
            const [text, cls] = statusFor(data.workingSetMB, 512, 768);
            badge('healthMemoryBadge', text, cls);
        }
        if (typeof data.cpuUsagePercent === 'number') {
            setText('healthCpuUsage', data.cpuUsagePercent.toFixed(1));
            moveGauge('cpu', data.cpuUsagePercent);
            const [text, cls] = statusFor(data.cpuUsagePercent, 50, 80);
            badge('healthCpuBadge', text, cls);
        }
        if (typeof data.threadCount === 'number') setText('healthThreadCount', data.threadCount);

        if (data.connectionState) {
            const label = document.getElementById('healthConnectionState');
            const icon = document.getElementById('healthConnectionIcon');
            const connectionState = String(data.connectionState);
            const cls = connectionState.toLowerCase() === 'connected' ? 'text-success'
                : connectionState.toLowerCase() === 'connecting' ? 'text-warning' : 'text-error';
            [label, icon].forEach(el => {
                if (!el) return;
                el.classList.remove('text-success', 'text-warning', 'text-error');
                el.classList.add(cls);
            });
            if (label && label.textContent !== connectionState) label.textContent = connectionState;
        }
    }

    const Health = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            const serverData = getServerData();
            if (!serverData) return; // the tab shows its "could not be loaded" state instead

            createGauge('latency', serverData.initialLatency);
            createGauge('memory', serverData.workingSetMB);
            createGauge('cpu', serverData.cpuPercent);
            initSparklineChart(serverData.latencySamples);
            bindRangeSelect(hours);
            await initLatencyHistoryChart(parseInt(document.getElementById('healthLatencyTimeRange')?.value || hours, 10));

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.gauges = { latency: null, memory: null, cpu: null };
            state.sparkline = null;
            state.sparklineData = [];
            state.isInitialized = false;
        },

        live: {
            group: 'performance',
            events: { HealthMetricsUpdate: applyHealth },
            snapshot: async function() {
                applyHealth(await DashboardHub.getCurrentPerformanceMetrics());
            }
        },

        applyHealth: applyHealth
    };

    window.Performance.Tabs.Health = Health;
})();
