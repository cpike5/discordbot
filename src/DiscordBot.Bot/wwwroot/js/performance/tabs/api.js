/**
 * Performance Dashboard - API Tab Module
 * Discord API latency over time. Not live: the numbers cover the chosen range.
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

    function getServerData() {
        const container = document.querySelector('[data-tab="api"]');
        if (!container) return { totalRequests: 0 };

        return {
            totalRequests: parseInt(container.dataset.totalRequests, 10) || 0
        };
    }

    async function loadChartData(hours) {
        const canvas = document.getElementById('apiLatencyChart');
        if (!canvas) return;

        try {
            const data = await ApiClient.get(`/api/metrics/api/latency?hours=${hours}`);
            if (!document.body.contains(canvas)) return; // the tab changed while this loaded

            const samples = data && Array.isArray(data.samples) ? data.samples : [];
            if (samples.length === 0) {
                ChartUtils.showChartEmpty(canvas, {
                    title: 'No latency samples yet',
                    description: 'API latency is sampled while the bot talks to Discord.'
                });
                return;
            }

            ChartUtils.clearChartState(canvas);
            const labels = samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const avgData = samples.map(s => s.avgLatencyMs);
            const p95Data = samples.map(s => s.p95LatencyMs);

            const chart = ChartUtils.createLineChart(canvas, labels, [
                {
                    label: 'Average Latency',
                    data: avgData,
                    themeColors: {
                        borderColor: c => c.secondary,
                        backgroundColor: c => c.alpha('accent-blue', 0.1)
                    },
                    fill: true,
                    tension: 0.4,
                    pointRadius: 2,
                    pointHoverRadius: 5
                },
                {
                    label: 'P95 Latency',
                    data: p95Data,
                    themeColors: { borderColor: c => c.warning },
                    backgroundColor: 'transparent',
                    fill: false,
                    tension: 0.4,
                    pointRadius: 2,
                    pointHoverRadius: 5,
                    borderDash: [5, 5]
                }
            ], {
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20 } } },
                scales: {
                    y: { beginAtZero: true, ticks: { callback: value => value + ' ms' } },
                    x: { ticks: { maxRotation: 45, minRotation: 0 } }
                }
            });
            state.charts.push(chart);
            ChartUtils.describeChart(canvas, {
                caption: `Discord API latency, ${TimeRange.getLabel()}`,
                labels,
                datasets: [{ label: 'Average (ms)', data: avgData }, { label: 'P95 (ms)', data: p95Data }],
                unit: 'ms'
            });

            const subtitle = document.getElementById('apiLatencySubtitle');
            if (subtitle) subtitle.textContent = `Discord API response times (${TimeRange.getLabel()})`;
        } catch (error) {
            if (error && error.name === 'AbortError') return;
            console.error('Failed to load API latency chart data:', error);
            ChartUtils.showChartError(canvas, null, () => loadChartData(hours));
        }
    }

    const Api = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            if (getServerData().totalRequests > 0) {
                await loadChartData(hours);
            }

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        }
    };

    window.Performance.Tabs.Api = Api;
})();
