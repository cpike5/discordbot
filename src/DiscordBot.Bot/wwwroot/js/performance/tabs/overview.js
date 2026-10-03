/**
 * Performance Dashboard - Overview Tab Module
 * Command throughput chart, plus live latency, CPU and memory from the performance hub group.
 *
 * There is no response-time-over-time chart here: the server keeps per-command aggregates, not a
 * time series, so any line would be invented. Response times by command are on the Commands tab.
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};
    window.Performance.Tabs = window.Performance.Tabs || {};

    const state = {
        charts: [],
        isInitialized: false
    };

    const ChartUtils = window.Performance.ChartUtils;
    const TimeRange = window.Performance.TimeRange;

    function setText(id, value) {
        if (typeof animateValueChange === 'function') {
            animateValueChange(id, value);
            return;
        }
        const el = document.getElementById(id);
        if (el) el.textContent = String(value);
    }

    function barClass(percent, warn, error) {
        return percent < warn ? 'progress-bar-healthy' : percent < error ? 'progress-bar-warning' : 'progress-bar-error';
    }

    function setBar(id, percent, warn, error) {
        const bar = document.getElementById(id);
        if (!bar) return;
        const clamped = Math.max(0, Math.min(100, percent));
        bar.style.width = clamped.toFixed(0) + '%';
        bar.classList.remove('progress-bar-healthy', 'progress-bar-warning', 'progress-bar-error');
        bar.classList.add(barClass(clamped, warn, error));
    }

    /** Applies a HealthMetricsUpdate (or the same shape from the hub snapshot). */
    function applyHealth(data) {
        if (!data) return;
        if (typeof data.latencyMs === 'number') {
            const connected = !data.connectionState || String(data.connectionState).toLowerCase() === 'connected';
            setText('overviewLatency', connected ? data.latencyMs : '\u2014');
        }

        if (typeof data.cpuUsagePercent === 'number') {
            setText('overviewCpuUsageText', data.cpuUsagePercent.toFixed(1) + '%');
            setBar('overviewCpuProgressBar', data.cpuUsagePercent, 50, 80);
        }

        if (typeof data.workingSetMB === 'number') {
            const text = document.getElementById('overviewMemoryText');
            const match = text ? /\/\s*([\d,.]+)\s*MB/.exec(text.textContent) : null;
            const max = match ? parseFloat(match[1].replace(/,/g, '')) : 0;
            if (max > 0) {
                setText('overviewMemoryText', data.workingSetMB + ' MB / ' + match[1] + ' MB');
                setBar('overviewMemoryBar', (data.workingSetMB * 100) / max, 60, 80);
            }
        }

        if (data.connectionState) {
            const label = document.getElementById('overviewBotHealth');
            if (label) {
                const connected = String(data.connectionState).toLowerCase() === 'connected';
                const connecting = String(data.connectionState).toLowerCase() === 'connecting';
                label.textContent = connected ? 'Healthy' : connecting ? 'Connecting' : 'Disconnected';
                label.classList.remove('text-success', 'text-warning', 'text-error');
                label.classList.add(connected ? 'text-success' : connecting ? 'text-warning' : 'text-error');
            }
        }
    }

    async function initThroughputChart(hours) {
        const canvas = document.getElementById('overviewThroughputChart');
        if (!canvas) return;

        const granularity = ChartUtils.getGranularity(hours);
        try {
            const data = await ApiClient.get(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`);
            if (!document.body.contains(canvas)) return; // the tab changed while this loaded

            const rows = Array.isArray(data) ? data : [];
            const subtitle = document.getElementById('overviewThroughputSubtitle');
            if (subtitle) {
                subtitle.textContent = `Commands per ${granularity} (${TimeRange.getLabel()})`;
            }

            if (rows.length === 0 || rows.every(d => !d.count)) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No commands in this period',
                    description: 'Throughput appears here once the bot has handled commands.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = rows.map(d => ChartUtils.formatLabel(d.timestamp, hours));
            const values = rows.map(d => d.count || 0);
            const chart = ChartUtils.createBarChart(canvas, labels, [{
                label: 'Commands',
                data: values,
                themeColors: { backgroundColor: c => c.secondary },
                borderRadius: 4
            }], {
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Commands per ${granularity}, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Commands', data: values }],
                firstColumn: granularity === 'day' ? 'Day' : 'Hour'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load throughput chart:', error);
            ChartUtils.showChartError(canvas, null, () => initThroughputChart(hours));
        }
    }

    const Overview = {
        init: async function(hours) {
            this.destroy();
            await initThroughputChart(hours || TimeRange.get());
            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        },

        // The overview shares the performance group with Health and Commands.
        live: {
            group: 'performance',
            events: { HealthMetricsUpdate: applyHealth },
            snapshot: async function() {
                applyHealth(await DashboardHub.getCurrentPerformanceMetrics());
            }
        },

        applyHealth: applyHealth
    };

    window.Performance.Tabs.Overview = Overview;
})();
