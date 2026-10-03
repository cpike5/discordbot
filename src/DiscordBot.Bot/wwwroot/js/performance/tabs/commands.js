/**
 * Performance Dashboard - Commands Tab Module
 * Response time and error rate by command, and throughput over time.
 *
 * The server keeps per-command aggregates and a throughput series, not response times or error
 * rates over time, so those two charts compare commands rather than draw a line through time.
 * The tab is not live: the hub's command update covers a fixed 24 hours, not the chosen range.
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
    const TOP_COMMANDS = 10;

    function getServerData() {
        const container = document.querySelector('[data-tab="commands"]');
        if (!container) return { totalCommands: 0 };

        return {
            totalCommands: parseInt(container.dataset.totalCommands, 10) || 0
        };
    }

    function inDocument(canvas) {
        return !!canvas && document.body.contains(canvas);
    }

    // One request feeds both per-command charts; a failed one is not remembered, so Retry refetches.
    let aggregatesRequest = null;

    function loadAggregates(hours) {
        if (!aggregatesRequest) {
            aggregatesRequest = ApiClient.get(`/api/metrics/commands/performance?hours=${hours}`)
                .then(data => Array.isArray(data) ? data : [])
                .catch(error => {
                    aggregatesRequest = null;
                    throw error;
                });
        }
        return aggregatesRequest;
    }

    async function initResponseTimeChart(hours) {
        const canvas = document.getElementById('commandsResponseTimeChart');
        if (!canvas) return;

        try {
            const aggregates = await loadAggregates(hours);
            if (!inDocument(canvas)) return;

            const busiest = aggregates
                .filter(a => a.executionCount > 0)
                .sort((a, b) => b.executionCount - a.executionCount)
                .slice(0, TOP_COMMANDS);
            if (busiest.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No response times yet',
                    description: 'Response times by command appear once commands have run.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = busiest.map(a => '/' + a.commandName);
            const avg = busiest.map(a => Math.round(a.avgMs));
            const p95 = busiest.map(a => Math.round(a.p95Ms));
            const chart = ChartUtils.createBarChart(canvas, labels, [
                {
                    label: 'Average',
                    data: avg,
                    themeColors: { backgroundColor: c => c.secondary },
                    borderRadius: 4
                },
                {
                    label: 'P95',
                    data: p95,
                    themeColors: { backgroundColor: c => c.warning },
                    borderRadius: 4
                }
            ], {
                indexAxis: 'y',
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20 } } },
                scales: {
                    x: { beginAtZero: true, ticks: { callback: value => value + ' ms' } },
                    y: { grid: { display: false } }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Response time by command, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Average (ms)', data: avg }, { label: 'P95 (ms)', data: p95 }],
                unit: 'ms',
                firstColumn: 'Command'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load response time chart:', error);
            ChartUtils.showChartError(canvas, null, () => initResponseTimeChart(hours));
        }
    }

    async function initThroughputChart(hours) {
        const canvas = document.getElementById('commandsThroughputChart');
        if (!canvas) return;

        const granularity = ChartUtils.getGranularity(hours);
        try {
            const data = await ApiClient.get(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`);
            if (!inDocument(canvas)) return;

            const subtitle = document.getElementById('commandsThroughputSubtitle');
            if (subtitle) subtitle.textContent = `Commands executed per ${granularity}`;

            const rows = Array.isArray(data) ? data : [];
            if (rows.length === 0 || rows.every(t => !t.count)) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No throughput yet',
                    description: 'Commands per period appear once commands have run.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = rows.map(t => ChartUtils.formatLabel(t.timestamp, hours));
            const values = rows.map(t => t.count || 0);
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

    async function initErrorRateChart(hours) {
        const canvas = document.getElementById('commandsErrorRateChart');
        if (!canvas) return;

        try {
            const aggregates = await loadAggregates(hours);
            if (!inDocument(canvas)) return;

            const failing = aggregates
                .filter(a => a.errorRate > 0)
                .sort((a, b) => b.errorRate - a.errorRate)
                .slice(0, TOP_COMMANDS);
            if (failing.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No command errors',
                    description: 'Every command completed without an error in this period.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = failing.map(a => '/' + a.commandName);
            const rates = failing.map(a => Math.round(a.errorRate * 10) / 10);
            const chart = ChartUtils.createBarChart(canvas, labels, [{
                label: 'Error rate (%)',
                data: rates,
                themeColors: { backgroundColor: c => c.error },
                borderRadius: 4
            }], {
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: context => context.parsed.x.toFixed(1) + '%' } }
                },
                scales: {
                    x: { beginAtZero: true, max: 100, ticks: { callback: value => value + '%' } },
                    y: { grid: { display: false } }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Error rate by command, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Error rate (%)', data: rates }],
                unit: '%',
                firstColumn: 'Command'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load error rate chart:', error);
            ChartUtils.showChartError(canvas, null, () => initErrorRateChart(hours));
        }
    }

    const Commands = {
        init: async function(hours) {
            this.destroy();
            aggregatesRequest = null;
            hours = hours || TimeRange.get();

            if (getServerData().totalCommands > 0) {
                await Promise.all([
                    initResponseTimeChart(hours),
                    initThroughputChart(hours),
                    initErrorRateChart(hours)
                ]);
            }

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        }
    };

    window.Performance.Tabs.Commands = Commands;
})();
