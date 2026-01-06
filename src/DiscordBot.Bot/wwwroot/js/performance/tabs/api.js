/**
 * Performance Dashboard - API Tab Module
 * Displays Discord API latency metrics
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
        const container = document.querySelector('[data-tab="api"]');
        if (!container) return { totalRequests: 0 };

        return {
            totalRequests: parseInt(container.dataset.totalRequests, 10) || 0
        };
    }

    async function loadChartData(hours) {
        try {
            const url = `/api/metrics/api/latency?hours=${hours}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();

            if (!data.samples || data.samples.length === 0) {
                console.warn('No latency samples available');
                return;
            }

            const labels = data.samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const avgData = data.samples.map(s => s.avgLatencyMs);
            const p95Data = data.samples.map(s => s.p95LatencyMs);

            const ctx = document.getElementById('apiLatencyChart');
            if (!ctx) return;

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'Average Latency',
                            data: avgData,
                            borderColor: ChartUtils.colors.primary,
                            backgroundColor: 'rgba(9, 142, 207, 0.1)',
                            fill: true,
                            tension: 0.4,
                            pointRadius: 2,
                            pointHoverRadius: 5
                        },
                        {
                            label: 'P95 Latency',
                            data: p95Data,
                            borderColor: ChartUtils.colors.warning,
                            backgroundColor: 'transparent',
                            fill: false,
                            tension: 0.4,
                            pointRadius: 2,
                            pointHoverRadius: 5,
                            borderDash: [5, 5]
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
                        },
                        x: {
                            ticks: {
                                maxRotation: 45,
                                minRotation: 0
                            }
                        }
                    }
                })
            });

            state.charts.push(chart);

            // Update subtitle
            const subtitle = document.getElementById('apiLatencySubtitle');
            if (subtitle) {
                subtitle.textContent = `Discord API response times (${TimeRange.getLabel()})`;
            }

        } catch (error) {
            console.error('Failed to load API latency chart data:', error);
            ChartUtils.showChartError('apiLatencyChart', error.message);
        }
    }

    const Api = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            const serverData = getServerData();
            if (serverData.totalRequests > 0) {
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
    window.initApiTab = function(hours) { Api.init(hours); };
    window.destroyApiTab = function() { Api.destroy(); };
    window.initApiMetricsTab = window.initApiTab;
    window.destroyApiMetricsTab = window.destroyApiTab;
})();
