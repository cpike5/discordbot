/**
 * Performance Dashboard - Overview Tab Module
 * Displays overview charts for response time and command throughput
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
    const TimestampUtils = window.Performance.TimestampUtils;
    const TimeRange = window.Performance.TimeRange;

    async function initResponseTimeChart(hours) {
        const ctx = document.getElementById('overviewResponseTimeChart');
        if (!ctx) return;

        try {
            const granularity = hours <= 24 ? 'hour' : 'day';
            const [performanceRes, throughputRes] = await Promise.all([
                fetch(`/api/metrics/commands/performance?hours=${hours}`),
                fetch(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`)
            ]);

            const performanceData = await performanceRes.json();
            const throughputData = await throughputRes.json();

            const labels = throughputData.map(d => ChartUtils.formatLabel(d.timestamp, hours));
            const avgMs = performanceData.length > 0
                ? performanceData.reduce((sum, d) => sum + (d.avgDurationMs || d.avgMs || 0), 0) / performanceData.length
                : 0;
            const responseValues = new Array(labels.length).fill(avgMs);

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels,
                    datasets: [{
                        label: 'Avg Response (ms)',
                        data: responseValues,
                        borderColor: ChartUtils.colors.primary,
                        backgroundColor: 'rgba(9, 142, 207, 0.1)',
                        fill: true,
                        tension: 0.4,
                        pointRadius: 3,
                        pointHoverRadius: 5
                    }]
                },
                options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                    plugins: { legend: { display: false } },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { callback: v => v + ' ms' }
                        }
                    }
                })
            });

            state.charts.push(chart);

            // Update subtitle
            const subtitle = document.getElementById('overviewResponseTimeSubtitle');
            if (subtitle) {
                subtitle.textContent = `Average command response time (${TimeRange.getLabel()})`;
            }
        } catch (error) {
            console.error('Failed to init response time chart:', error);
            ChartUtils.showChartError('overviewResponseTimeChart', error.message);
        }
    }

    async function initThroughputChart(hours) {
        const ctx = document.getElementById('overviewThroughputChart');
        if (!ctx) return;

        try {
            const granularity = hours <= 24 ? 'hour' : 'day';
            const response = await fetch(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`);
            const data = await response.json();

            const labels = data.map(d => ChartUtils.formatLabel(d.timestamp, hours));
            const values = data.map(d => d.commandCount || d.count || 0);

            const chart = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Commands',
                        data: values,
                        backgroundColor: ChartUtils.colors.secondary,
                        borderRadius: 4
                    }]
                },
                options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true } }
                })
            });

            state.charts.push(chart);

            // Update subtitle
            const subtitle = document.getElementById('overviewThroughputSubtitle');
            if (subtitle) {
                const period = granularity === 'day' ? 'day' : 'hour';
                subtitle.textContent = `Commands per ${period} (${TimeRange.getLabel()})`;
            }
        } catch (error) {
            console.error('Failed to init throughput chart:', error);
            ChartUtils.showChartError('overviewThroughputChart', error.message);
        }
    }

    const Overview = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            TimestampUtils.convertTimestamps();

            await Promise.all([
                initResponseTimeChart(hours),
                initThroughputChart(hours)
            ]);

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        }
    };

    window.Performance.Tabs.Overview = Overview;
    window.initOverviewTab = function(hours) { Overview.init(hours); };
    window.destroyOverviewTab = function() { Overview.destroy(); };
})();
