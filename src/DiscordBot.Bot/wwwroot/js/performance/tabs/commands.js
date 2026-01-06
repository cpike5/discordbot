/**
 * Performance Dashboard - Commands Tab Module
 * Displays command performance metrics including response times, throughput, and error rates
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

    function getServerData() {
        const container = document.querySelector('[data-tab="commands"]');
        if (!container) return { totalCommands: 0 };

        return {
            totalCommands: parseInt(container.dataset.totalCommands, 10) || 0
        };
    }

    async function initResponseTimeChart(hours) {
        try {
            const granularity = ChartUtils.getGranularity(hours);
            const [throughputRes, aggregatesRes] = await Promise.all([
                fetch(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`),
                fetch(`/api/metrics/commands/performance?hours=${hours}`)
            ]);

            if (!throughputRes.ok || !aggregatesRes.ok) {
                throw new Error(`HTTP ${throughputRes.status || aggregatesRes.status}`);
            }

            const throughputData = await throughputRes.json();
            const aggregates = await aggregatesRes.json();

            const labels = throughputData.map(t => ChartUtils.formatLabel(t.timestamp, hours));

            // Use aggregate values distributed across time buckets
            const avgData = new Array(labels.length).fill(aggregates.length > 0 ? aggregates[0].avgMs : 0);
            const p95Data = new Array(labels.length).fill(aggregates.length > 0 ? aggregates[0].p95Ms : 0);
            const p99Data = new Array(labels.length).fill(aggregates.length > 0 ? aggregates[0].p99Ms : 0);

            const ctx = document.getElementById('commandsResponseTimeChart');
            if (!ctx) return;

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'Average',
                            data: avgData,
                            borderColor: ChartUtils.colors.primary,
                            backgroundColor: 'transparent',
                            tension: 0.4,
                            pointRadius: 2,
                            pointHoverRadius: 5
                        },
                        {
                            label: 'P95',
                            data: p95Data,
                            borderColor: ChartUtils.colors.warning,
                            backgroundColor: 'transparent',
                            tension: 0.4,
                            pointRadius: 2,
                            pointHoverRadius: 5,
                            borderDash: [5, 5]
                        },
                        {
                            label: 'P99',
                            data: p99Data,
                            borderColor: ChartUtils.colors.error,
                            backgroundColor: 'transparent',
                            tension: 0.4,
                            pointRadius: 2,
                            pointHoverRadius: 5,
                            borderDash: [2, 2]
                        }
                    ]
                },
                options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                boxWidth: 12,
                                padding: 20
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function(value) {
                                    return value + ' ms';
                                }
                            }
                        }
                    }
                })
            });

            state.charts.push(chart);
        } catch (error) {
            console.error('Failed to load response time chart:', error);
            ChartUtils.showChartError('commandsResponseTimeChart', error.message);
        }
    }

    async function initThroughputChart(hours) {
        try {
            const granularity = ChartUtils.getGranularity(hours);
            const response = await fetch(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            const labels = data.map(t => ChartUtils.formatLabel(t.timestamp, hours));

            const ctx = document.getElementById('commandsThroughputChart');
            if (!ctx) return;

            const chart = ChartUtils.createBarChart(ctx, labels, [{
                label: 'Commands',
                data: data.map(t => t.count),
                backgroundColor: ChartUtils.colors.secondary,
                borderRadius: 4
            }], {
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true } }
            });

            state.charts.push(chart);

            // Update subtitle
            const subtitle = document.getElementById('commandsThroughputSubtitle');
            if (subtitle) {
                subtitle.textContent = `Commands executed per ${hours <= 24 ? 'hour' : 'day'}`;
            }
        } catch (error) {
            console.error('Failed to load throughput chart:', error);
            ChartUtils.showChartError('commandsThroughputChart', error.message);
        }
    }

    async function initErrorRateChart(hours) {
        try {
            const granularity = ChartUtils.getGranularity(hours);
            const [throughputRes, aggregatesRes] = await Promise.all([
                fetch(`/api/metrics/commands/throughput?hours=${hours}&granularity=${granularity}`),
                fetch(`/api/metrics/commands/performance?hours=${hours}`)
            ]);

            if (!throughputRes.ok || !aggregatesRes.ok) {
                throw new Error(`HTTP ${throughputRes.status || aggregatesRes.status}`);
            }

            const throughputData = await throughputRes.json();
            const aggregates = await aggregatesRes.json();

            const labels = throughputData.map(t => ChartUtils.formatLabel(t.timestamp, hours));

            // Use overall error rate distributed across time buckets
            const totalCommands = aggregates.reduce((sum, a) => sum + a.executionCount, 0);
            const errorRate = totalCommands > 0
                ? aggregates.reduce((sum, a) => sum + (a.executionCount * a.errorRate / 100.0), 0) / totalCommands * 100
                : 0;

            const errorRateData = new Array(labels.length).fill(errorRate);

            const ctx = document.getElementById('commandsErrorRateChart');
            if (!ctx) return;

            const chart = ChartUtils.createLineChart(ctx, labels, [{
                label: 'Error Rate',
                data: errorRateData,
                borderColor: ChartUtils.colors.error,
                backgroundColor: 'rgba(239, 68, 68, 0.1)',
                fill: true,
                tension: 0.4,
                pointRadius: 3,
                pointHoverRadius: 5
            }], {
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                return context.parsed.y.toFixed(1) + '%';
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: Math.ceil(Math.max(5, errorRate + 1)),
                        ticks: {
                            callback: function(value) {
                                return value.toFixed(1) + '%';
                            }
                        }
                    }
                }
            });

            state.charts.push(chart);
        } catch (error) {
            console.error('Failed to load error rate chart:', error);
            ChartUtils.showChartError('commandsErrorRateChart', error.message);
        }
    }

    const Commands = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            TimestampUtils.convertTimestamps();

            const serverData = getServerData();
            if (serverData.totalCommands > 0) {
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
    window.initCommandsTab = function(hours) { Commands.init(hours); };
    window.destroyCommandsTab = function() { Commands.destroy(); };
})();
