/**
 * Performance Dashboard - Health Tab Module
 * Displays health metrics with gauges and latency history
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

    // Gauge configurations
    const latencyThresholds = [0, 100, 200];
    const latencyColors = ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)'];
    const memoryThresholds = [0, 512, 768];
    const memoryColors = ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)'];
    const cpuThresholds = [0, 50, 80];
    const cpuColors = ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)'];

    function getServerData() {
        const container = document.querySelector('[data-tab="health"]');
        if (!container) return {};

        return {
            initialLatency: parseInt(container.dataset.initialLatency, 10) || 0,
            workingSetMB: parseInt(container.dataset.workingSetMb, 10) || 0,
            cpuPercent: parseFloat(container.dataset.cpuPercent) || 0,
            latencySamples: JSON.parse(container.dataset.latencySamples || '[]')
        };
    }

    function initSparklineChart(samples) {
        const ctx = document.getElementById('healthLatencySparkline');
        if (!ctx || !samples || samples.length === 0) return;

        const chart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: samples.map((_, i) => i + 1),
                datasets: [{
                    data: samples,
                    backgroundColor: samples.map(v => {
                        if (v < 100) return 'rgba(16, 185, 129, 0.8)';
                        if (v < 200) return 'rgba(245, 158, 11, 0.8)';
                        return 'rgba(239, 68, 68, 0.8)';
                    }),
                    borderRadius: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#2f3336',
                        titleColor: '#d7d3d0',
                        bodyColor: '#a8a5a3',
                        borderColor: '#3f4447',
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

        state.charts.push(chart);
    }

    async function initLatencyHistoryChart(hours) {
        try {
            const response = await fetch(`/api/metrics/health/latency?hours=${hours}`);
            const data = await response.json();

            const ctx = document.getElementById('healthLatencyHistoryChart');
            if (!ctx) return;

            const labels = data.samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const latencies = data.samples.map(s => s.latencyMs);

            const chart = ChartUtils.createLineChart(ctx, labels, [{
                label: 'Latency (ms)',
                data: latencies,
                borderColor: ChartUtils.colors.primary,
                backgroundColor: 'rgba(9, 142, 207, 0.1)',
                fill: true,
                tension: 0.4,
                pointRadius: 2,
                pointHoverRadius: 5
            }], {
                plugins: { legend: { display: false } },
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
            });

            state.charts.push(chart);

            // Time range selector
            const timeRangeSelect = document.getElementById('healthLatencyTimeRange');
            if (timeRangeSelect) {
                const hoursToOption = { 24: '24', 168: '168', 720: '168' };
                if (hoursToOption[hours]) {
                    timeRangeSelect.value = hoursToOption[hours];
                }

                timeRangeSelect.addEventListener('change', function(e) {
                    initLatencyHistoryChart(parseInt(e.target.value));
                });
            }
        } catch (error) {
            console.error('Failed to load latency history:', error);
            ChartUtils.showChartError('healthLatencyHistoryChart', error.message);
        }
    }

    const Health = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            const serverData = getServerData();

            // Create gauges
            const latencyGauge = ChartUtils.createGaugeChart(
                document.getElementById('healthLatencyGauge'),
                serverData.initialLatency, 500, latencyThresholds, latencyColors
            );
            if (latencyGauge) state.charts.push(latencyGauge);

            const memoryGauge = ChartUtils.createGaugeChart(
                document.getElementById('healthMemoryGauge'),
                serverData.workingSetMB, 1024, memoryThresholds, memoryColors
            );
            if (memoryGauge) state.charts.push(memoryGauge);

            const cpuGauge = ChartUtils.createGaugeChart(
                document.getElementById('healthCpuGauge'),
                serverData.cpuPercent, 100, cpuThresholds, cpuColors
            );
            if (cpuGauge) state.charts.push(cpuGauge);

            initSparklineChart(serverData.latencySamples);
            await initLatencyHistoryChart(hours);

            TimestampUtils.convertTimestamps();

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        }
    };

    window.Performance.Tabs.Health = Health;
    window.initHealthTab = function(hours) { Health.init(hours); };
    window.destroyHealthTab = function() { Health.destroy(); };
})();
